using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaTube.Data;
using OmegaTube.Util;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // A video card in one of three looks:
    //  - classic (Omega): rounded 16:9 thumbnail with an info box under it that lights up when focused;
    //  - glass (Glorious rows 2+): a bare rounded thumbnail with a soft shadow. Focused, a dark panel with the title
    //    (one line, scrolling when it is longer) appears on the thumbnail, the channel / views / age line under it,
    //    and a hairline outline runs round the card. A short animated preview can play over the thumbnail;
    //  - featured (thumbOnly, Glorious row 1): a bare rounded thumbnail with the same shadow and outline, no text:
    //    the title and details live on the hero backdrop.
    // Duration badge bottom right and a red progress bar for started videos in all of them.
    // Built in two steps for smooth scrolling: the constructor only makes the cheap frame (thumbnail, info box);
    // Fill() adds the texts and starts the thumbnail, when the card is about to be seen (UiWork). Parts a card may
    // never need (outline, animation layers, scrolling title) are created on first use.
    public class Card : View, IGridItem
    {
        public VideoItem Item { get; private set; }
        public View View => this;
        public bool Filled { get; private set; }
        public bool Shown { get; private set; } = true;   // managed mirror of Visibility (rows show / hide far cards)

        private const int ShadowMargin = 36, ShadowDrop = 10;   // card_shadow9.9: the blur margin is baked into the image
        private const int InfoInset = 8, InfoHeight = 54, MetaRowH = 44, TitlePad = 14;
        private const int MarqueePauseMs = 1100, MarqueePxPerSec = 70;

        private readonly int width, thumbH;
        private readonly bool thumbOnly;   // featured: no text at all
        private readonly bool glass;       // glass or featured: shadow and outline
        private readonly ImageView thumb;
        private readonly ImageView info;   // classic: the box under the thumbnail; glass: the title panel on it
        private readonly View infoTopPatch;
        private readonly View progress;
        private View ring;                 // hairline outline of the focused card
        private ImageView anim, corners;   // animated preview over the thumbnail, and the frame that rounds its corners
        private View titleClip;
        private TextLabel title, meta, marquee;   // marquee: the title at its full width, scrolled while focused
        private float marqueeOverflow;
        private bool noMarquee;            // the title fits: nothing to scroll
        private Animation fadeInfo, fadeAnim, marqueeAnim, zoom;
        private bool focused;
        private bool Rich => glass && !thumbOnly;   // glass look with texts

        public Card(VideoItem item, int width = Theme.CardWidth, bool thumbOnly = false, bool glass = false)
        {
            Item = item;
            this.width = width;
            this.thumbOnly = thumbOnly;
            this.glass = glass;
            thumbH = Theme.ThumbHeight(width);
            Size = new Size(width, thumbOnly ? thumbH : glass ? thumbH + MetaRowH : Theme.CardHeight(width));
            // Scale around the centre so neighbours are overlapped evenly.
            PivotPoint = Tizen.NUI.PivotPoint.Center;
            PositionUsesPivotPoint = false;  // positions stay top-left based

            // Rounding on Tizen 5.5: the firmware's NUI has no CornerRadius, so we round with image techniques that
            // it does support. The thumbnail is clipped by a rounded-rect alpha mask; the classic info box is a white
            // rounded-rect nine-patch tinted to the panel colour (fixed corners at any width). Without a classic info
            // box under it the thumbnail rounds all four corners; with one it extends a radius under the box (the
            // "glued" look) and a square patch hides the box's rounded top corners.
            if (glass)
            {
                // Soft shadow under the thumbnail (no BoxShadow on 5.5: a pre-blurred nine-patch, drawn first = behind).
                Add(new ImageView
                {
                    ResourceUrl = Theme.Image("card_shadow9.9"),
                    Size = new Size(width + 2 * ShadowMargin, thumbH + 2 * ShadowMargin),
                    Position = new Position(-ShadowMargin, -ShadowMargin + ShadowDrop),
                });
            }
            thumb = new ImageView { Size = new Size(width, thumbOnly || glass ? thumbH : thumbH + Theme.CardRadius) };
            Add(thumb);

            if (Rich)
            {
                info = new ImageView
                {
                    Position = new Position(InfoInset, thumbH - InfoHeight - InfoInset),
                    Size = new Size(width - 2 * InfoInset, InfoHeight),
                    ResourceUrl = Theme.Image("card_info9.9"),
                    Opacity = 0f,   // fades in while focused
                };
                info.Hide();
                Add(info);
            }
            else if (!thumbOnly)
            {
                info = new ImageView
                {
                    Position = new Position(0, thumbH),
                    Size = new Size(width, Theme.CardInfoHeight),
                    ResourceUrl = Theme.Image("round9.9"),
                    Color = Theme.CardInfo,
                };
                Add(info);
                infoTopPatch = Widgets.Box(Theme.CardInfo, width, Theme.CardRadius);
                info.Add(infoTopPatch);
            }

            // On top of the info box's edge: the watched-fraction bar sits on the seam.
            progress = Widgets.Box(Theme.Accent, 0, 5);
            progress.Position = new Position(0, thumbH - 5);
            Add(progress);
            UpdateProgress();
        }

        // Called by Show / Hide of the row: remembers the state without a native read.
        public void SetShown(bool shown)
        {
            if (shown != Shown)
            {
                Shown = shown;
                Widgets.SetVisible(this, shown);
            }
        }

        // Texts, duration badge and thumbnail. Text labels are what costs (shaping on the UI thread).
        public void Fill()
        {
            if (Filled)
            {
                return;
            }
            Filled = true;
            string badge = Item.Live ? T("LIVE") : Item.Duration;
            if (!string.IsNullOrEmpty(badge))
            {
                var duration = Widgets.Label(" " + badge + " ", Theme.SmallSize - 2, Theme.Text);
                duration.BackgroundColor = Item.Live ? Theme.Accent : new Color(0, 0, 0, 0.82f);
                Widgets.Round(duration, 4);
                duration.ParentOrigin = Tizen.NUI.ParentOrigin.TopLeft;
                duration.PivotPoint = Tizen.NUI.PivotPoint.BottomRight;
                duration.PositionUsesPivotPoint = true;
                duration.Position = new Position(width - 8, thumbH - 10);
                thumb.Add(duration);
            }

            if (Rich)
            {
                // The title: one line with an ellipsis (a longer one scrolls while the card is focused).
                int titleW = width - 2 * InfoInset - 2 * TitlePad;
                titleClip = new View { Position = new Position(TitlePad, 6), Size = new Size(titleW, 30),
                                       ClippingMode = ClippingModeType.ClipChildren };
                title = Widgets.Label(Item.Title, Theme.CardTitleSize, Theme.CardText, titleW, 30);
                ApplyDeArrow(title);
                titleClip.Add(title);
                info.Add(titleClip);
                // Channel, views and age on one line under the card.
                meta = Widgets.Label(MetaLine(Item), Theme.SmallSize, Theme.CardTextDim, width - 12);
                meta.Position = new Position(6, thumbH + 10);
                meta.Opacity = 0f;
                meta.Hide();
                Add(meta);
                if (focused)
                {
                    FadeInfo(true);   // focused before it was filled (the first card of a row): show what it has now
                }
            }
            else if (!thumbOnly)
            {
                title = Widgets.Label(Item.Title, Theme.CardTitleSize, Theme.CardText, width - 24, 60);
                ApplyDeArrow(title);
                title.MultiLine = true;
                title.Position = new Position(12, 2);
                info.Add(title);

                meta = Widgets.Label(MetaLine(Item), Theme.CardMetaSize, Theme.CardTextDim, width - 24);
                meta.Position = new Position(12, 62);
                info.Add(meta);
                ApplyFocusColors();
            }

            if (!string.IsNullOrEmpty(Item.Thumbnail))
            {
                // Disk-cached thumbnails load instantly; a miss paints the plain URL and upgrades when the
                // background download (ThumbCache) lands. The feed's own size (maxres looked wrong on 4:3).
                string cached = ThumbCache.Get(Item.Thumbnail);
                Widgets.MaskedImage(thumb, cached ?? Item.Thumbnail, Theme.Image("round_mask"));
                if (cached == null && subscribedThumb != Item.Thumbnail)
                {
                    if (subscribedThumb != null)
                    {
                        ThumbCache.Ready -= OnThumbReady;
                    }
                    subscribedThumb = Item.Thumbnail;
                    ThumbCache.Ready += OnThumbReady;
                }
            }
        }

        // DeArrow on/off (mirrors the config; set at startup and from the Settings toggle).
        public static bool DeArrowOn = true;

        // DeArrow: swap in the crowdsourced title when there is one (cached after the first look).
        private void ApplyDeArrow(TextLabel label)
        {
            if (!DeArrowOn || label == null || string.IsNullOrEmpty(Item.Id))
            {
                return;
            }
            string id = Item.Id;
            Task.Run(() =>
            {
                string t = DeArrow.Title(id);
                if (t != null)
                {
                    UiGuard.Post(() => { label.Text = t; });
                }
            });
        }

        // The same video in a fresher page (network after cache): keeps the card and its loaded thumbnail.
        public void Rebind(VideoItem item)
        {
            Item = item;
            if (Filled && !thumbOnly)
            {
                title.Text = item.Title;
                meta.Text = MetaLine(item);
                DropMarquee();   // measured for the old title
            }
            UpdateProgress();
        }

        private static string MetaLine(VideoItem item)
        {
            string line = item.Channel;
            if (!string.IsNullOrEmpty(item.Views))
            {
                line += " • " + CompactViews(item.Views);
            }
            if (!string.IsNullOrEmpty(item.Published))
            {
                line += " • " + CompactAge(item.Published);
            }
            return line;
        }

        // "571K views" / "571 B görüntüleme" -> "571k"; "1.2M" -> "1.2M".
        private static string CompactViews(string v)
        {
            var m = System.Text.RegularExpressions.Regex.Match(v, @"^([\d.,]+)\s*([A-Za-zÇĞİÖŞÜçğıöşü]{0,3})");
            if (!m.Success)
            {
                return v;
            }
            string num = m.Groups[1].Value;
            string u = m.Groups[2].Value.ToLowerInvariant();
            string unit = "";
            if (u.Length > 0)
            {
                if (u[0] == 'b') unit = "k";        // bin / B
                else if (u[0] == 'k') unit = "k";
                else if (u[0] == 'm' || u[0] == 'g') unit = "M";   // milyon / Mn / M
            }
            return num + unit;
        }

        // "2 months ago" / "2 ay önce" -> "2ay"; "3 weeks ago" -> "3hf".
        private static string CompactAge(string p)
        {
            var m = System.Text.RegularExpressions.Regex.Match(p, @"^(\d+)\s+([A-Za-zÇĞİÖŞÜçğıöşü]+)");
            if (!m.Success)
            {
                return p;
            }
            string u = m.Groups[2].Value.ToLowerInvariant();
            string shortUnit;
            if (u.StartsWith("s")) shortUnit = u.StartsWith("sn") ? "sn" : "s";          // seconds / saniye / s
            else if (u.StartsWith("d") && u.StartsWith("dk")) shortUnit = "dk";
            else if (u.StartsWith("m") && u.StartsWith("mo")) shortUnit = "mo";
            else if (u.StartsWith("m")) shortUnit = "dk";                                 // minute / dakika
            else if (u.StartsWith("h") && u.StartsWith("hf")) shortUnit = "hf";
            else if (u.StartsWith("h")) shortUnit = "sa";                                 // hour / saat
            else if (u.StartsWith("g")) shortUnit = "g";                                  // gün
            else if (u.StartsWith("w")) shortUnit = "hf";
            else if (u.StartsWith("a")) shortUnit = "ay";                                 // ay
            else if (u.StartsWith("y") || u.StartsWith("yıl")) shortUnit = "yıl";
            else shortUnit = u.Substring(0, Math.Min(2, u.Length));
            return m.Groups[1].Value + shortUnit;
        }

        public void UpdateProgress()
        {
            // Local position (watched here) or YouTube's (watched on another device), whichever is further.
            double p = Math.Max(LocalStore.Progress(Item.Id), Item.WatchedPercent / 100.0);
            progress.SizeWidth = (float)(width * p);
            Widgets.SetVisible(progress, p > 0.01);
        }

        // ------------------------------------------------------------------ animated preview (glass cards)

        public bool CanAnimate => Rich && Filled;

        // Shows an animated preview over the thumbnail: the sheet is loaded unscaled (Desired size = its real size,
        // or the toolkit shrinks it to the view and the cells no longer line up); frames are picked with PixelArea.
        public void BeginAnimation(string sheetPath, int sheetW, int sheetH, RelativeVector4 cell)
        {
            if (!Rich)
            {
                return;
            }
            if (anim == null)
            {
                // Over the thumbnail, under the title panel: a decoded sheet cell, and a page-coloured frame with
                // rounded holes over it that hides the sheet's square corners (a sprite sheet cannot be masked).
                anim = new ImageView { Size = new Size(width, thumbH), Opacity = 0f };
                Add(anim);
                corners = new ImageView { Size = new Size(width, thumbH), Color = Theme.PageTint };
                Add(corners);
                info.RaiseToTop();   // (added last, so lifted back above the new layers)
                progress.RaiseToTop();
                ring?.RaiseToTop();
            }
            var map = new PropertyMap();
            map.Add((int)Visual.Property.Type, new PropertyValue((int)Visual.Type.Image));
            map.Add((int)ImageVisualProperty.URL, new PropertyValue(sheetPath));
            map.Add((int)ImageVisualProperty.DesiredWidth, new PropertyValue(sheetW));
            map.Add((int)ImageVisualProperty.DesiredHeight, new PropertyValue(sheetH));
            anim.Image = map;
            anim.PixelArea = cell;
            corners.ResourceUrl = Theme.Image("round_hole9.9");
            corners.Show();
            anim.Opacity = 0f;
            anim.Show();
            fadeAnim?.Stop();
            fadeAnim = new Animation(250);
            fadeAnim.AnimateTo(anim, "Opacity", 1f, Widgets.EaseOut);
            fadeAnim.Play();
        }

        public void ShowAnimationFrame(RelativeVector4 cell)
        {
            if (anim != null)
            {
                anim.PixelArea = cell;
            }
        }

        public void EndAnimation()
        {
            if (anim == null)
            {
                return;
            }
            fadeAnim?.Stop();
            anim.Hide();
            anim.Opacity = 0f;
            anim.ResourceUrl = "";        // release the sheet's texture
            corners.Hide();
            corners.ResourceUrl = "";
        }

        // ------------------------------------------------------------------ scrolling title (glass cards)

        // A long title scrolls to its end and starts over, after a short pause each way; a title that fits stays put.
        private void StartMarquee()
        {
            if (titleClip == null || noMarquee)
            {
                return;
            }
            if (marquee == null)
            {
                // Measured once, on first focus: the same title at its natural width.
                TextLabel m = Widgets.Label(Item.Title, Theme.BodySize, Theme.CardText);
                titleClip.Add(m);
                float overflow = 0f;
                try
                {
                    overflow = m.NaturalSize2D.Width - titleClip.SizeWidth;
                }
                catch (Exception)
                {
                }
                if (overflow <= 2f)
                {
                    titleClip.Remove(m);
                    m.Dispose();
                    noMarquee = true;
                    return;
                }
                m.Hide();
                marquee = m;
                marqueeOverflow = overflow;
            }
            int move = (int)(marqueeOverflow / MarqueePxPerSec * 1000f) + 200;
            title.Hide();
            marquee.PositionX = 0;
            marquee.Show();
            marqueeAnim?.Stop();
            marqueeAnim = new Animation(MarqueePauseMs + move + MarqueePauseMs) { Looping = true };
            marqueeAnim.AnimateTo(marquee, "PositionX", -(marqueeOverflow + 12f), MarqueePauseMs, MarqueePauseMs + move, Widgets.Linear);
            marqueeAnim.Play();
        }

        private void StopMarquee()
        {
            marqueeAnim?.Stop();
            marqueeAnim = null;
            if (marquee != null)
            {
                marquee.Hide();
                marquee.PositionX = 0;
                title.Show();
            }
        }

        private void DropMarquee()
        {
            StopMarquee();
            if (marquee != null)
            {
                titleClip.Remove(marquee);
                marquee.Dispose();
                marquee = null;
            }
            noMarquee = false;
            if (focused)
            {
                StartMarquee();
            }
        }

        // ------------------------------------------------------------------ focus

        public void SetFocused(bool focused)
        {
            if (focused == this.focused)
            {
                return;   // grids refresh every card's state after a page: no animation for unchanged ones
            }
            this.focused = focused;
            if (glass)
            {
                ShowRing(focused);
                if (Rich && Filled)
                {
                    FadeInfo(focused);
                }
            }
            ApplyFocusColors();
            zoom?.Stop();
            zoom = new Animation(140);
            float s = focused ? Theme.FocusScale : 1f;
            zoom.AnimateTo(this, "Scale", new Vector3(s, s, 1f), Widgets.EaseOut);
            zoom.Play();
            if (focused)
            {
                RaiseToTop();
            }
        }

        // The hairline outline, created the first time the card is focused (most cards never are).
        private void ShowRing(bool on)
        {
            if (ring == null)
            {
                if (!on)
                {
                    return;
                }
                ring = new ImageView { ResourceUrl = Theme.Image("card_ring9.9"), Size = new Size(width, thumbH) };
                Add(ring);   // last: over the thumbnail, the title panel and the progress bar
            }
            Widgets.SetVisible(ring, on);
        }

        // Title panel and channel line fade in with the focus and fade out (and leave the render list) without it.
        private void FadeInfo(bool on)
        {
            if (on)
            {
                info.Show();
                meta.Show();
                StartMarquee();
            }
            else
            {
                StopMarquee();
            }
            fadeInfo?.Stop();
            fadeInfo = new Animation(160);
            fadeInfo.AnimateTo(info, "Opacity", on ? 1f : 0f, Widgets.EaseOut);
            fadeInfo.AnimateTo(meta, "Opacity", on ? 1f : 0f, Widgets.EaseOut);
            if (!on)
            {
                fadeInfo.Finished += (s, e) => UiGuard.Run("Card.fade", () =>
                {
                    if (!focused)
                    {
                        info.Hide();
                        meta.Hide();
                    }
                });
            }
            fadeInfo.Play();
        }

        private void ApplyFocusColors()
        {
            if (thumbOnly || glass)
            {
                return;   // featured: no info box. Glass: dark panel with fixed colours
            }
            Color infoColor = focused ? Theme.CardInfoFocused : Theme.CardInfo;
            info.Color = infoColor;   // tints the white nine-patch
            infoTopPatch.BackgroundColor = infoColor;
            if (Filled)
            {
                title.TextColor = focused ? Theme.CardTextFocused : Theme.Text;
                meta.TextColor = focused ? Theme.CardTextFocused : Theme.TextDim;
            }
        }

        private string subscribedThumb;

        private void OnThumbReady(string url)
        {
            if (url != subscribedThumb)
            {
                return;
            }
            UiGuard.Post(() =>
            {
                try
                {
                    string p = ThumbCache.Get(url);
                    if (p != null)
                    {
                        Widgets.MaskedImage(thumb, p, Theme.Image("round_mask"));
                    }
                }
                catch
                {
                    // the card went away while the download finished
                }
            });
        }

        // The focus outline is a child: a card going away must not take the shared preview ring (Hero's) with it.
        protected override void Dispose(DisposeTypes type)
        {
            if (type == DisposeTypes.Explicit)
            {
                ThumbCache.Ready -= OnThumbReady;
                subscribedThumb = null;
            }
            if (type == DisposeTypes.Explicit)
            {
                foreach (View child in new List<View>(Children))
                {
                    if (child is PreviewRing)
                    {
                        Remove(child);
                    }
                }
            }
            base.Dispose(type);
        }
    }
}
