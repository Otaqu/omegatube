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
    // Liked Music as a text list, like the YT Music queue: no thumbnails (a 2k-entry list loads and scrolls fast),
    // one line per song (title, channel, duration). Shuffle starts a random song of the loaded page.
    //  - ▲▼ move, OK plays the song, Back leaves.
    public class MusicListScreen : Screen, IHoldActions
    {
        private const int RowH = 64, RowGap = 4, ListX = 120, ListTop = 250, ListW = 1560, VisibleRows = 10;
        private const int ShuffleX = 1560, ShuffleY = 64;

        private class Row
        {
            public VideoItem Item;
            public View View;
            public TextLabel Title, Channel, Duration;
            public View Highlight;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly View clip, rowsHost;
        private readonly TextLabel countLabel, hint;
        private readonly Widgets.PillButton shuffle;
        private Animation scroll;
        private static FeedPage cachedPage;
        private static DateTime cachedAt;
        private static bool prefetching;
        private string continuation = "";
        private int focus;
        private bool loading, onShuffle;
        private static List<VideoItem> allItems = new List<VideoItem>();
        private static bool allLoaded, loadingAll;

        // Starts the list download while the user is still on the Library screen: opening Liked Music then shows
        // the songs at once instead of "Loading…" for seconds.
        public static void Prefetch()
        {
            if (prefetching || (cachedPage != null && (DateTime.Now - cachedAt).TotalMinutes < 10))
            {
                return;
            }
            prefetching = true;
            UiGuard.Fire("MusicListScreen.Prefetch", async () =>
            {
                try
                {
                    FeedPage p = await YouTube.PlaylistPage("LM", "");
                    if (p.Error == null)
                    {
                        cachedPage = p;
                        cachedAt = DateTime.Now;
                    }
                }
                finally
                {
                    prefetching = false;
                }
            });
        }

        public MusicListScreen(App app) : base(app)
        {
            Root.BackgroundColor = Theme.Glorious ? Color.Transparent : Theme.Background;

            var title = Widgets.Label(T("Liked Music"), Theme.TitleSize + 12, Theme.Text, 1200, 70);
            title.Position = new Position(ListX, 64);
            Root.Add(title);
            countLabel = Widgets.Label(T("Loading…"), Theme.BodySize + 2, Theme.TextDim, 1200, 40);
            countLabel.Position = new Position(ListX + 2, 140);
            Root.Add(countLabel);

            shuffle = new Widgets.PillButton(T("Random"), "repeat", 280, 76);
            shuffle.Position = new Position(ShuffleX, ShuffleY);
            Root.Add(shuffle);

            clip = new View
            {
                Size = new Size(ListW, VisibleRows * (RowH + RowGap)),
                Position = new Position(ListX, ListTop),
                ClippingMode = ClippingModeType.ClipChildren,
            };
            Root.Add(clip);
            rowsHost = new View();
            clip.Add(rowsHost);

            hint = Widgets.Label("", Theme.SmallSize + 1, Theme.TextDim, 1600, 40);
            hint.Position = new Position(ListX, 1010);
            Root.Add(hint);

            scroll = new Animation(180);
        }

        public override void OnShow()
        {
            if (rows.Count == 0 && !loading)
            {
                if (cachedPage != null && (DateTime.Now - cachedAt).TotalMinutes < 10)
                {
                    FillFromCache();
                    return;
                }
                UiGuard.Fire("MusicListScreen.Load", () => Load());
            }
        }

        private void FillFromCache()
        {
            continuation = cachedPage.Continuation ?? "";
            foreach (VideoItem item in cachedPage.Items)
            {
                item.PlaylistId = "LM";
                AddRow(item);
            }
            countLabel.Text = T("{0} songs", rows.Count);
            hint.Text = T("OK play    ▲▼ move    Back leave");
            FocusRow(0);
        }

        private async Task Load()
        {
            loading = true;
            try
            {
                FeedPage page = await YouTube.PlaylistPage("LM", continuation ?? "");
                if (page.Error != null && rows.Count == 0)
                {
                    countLabel.Text = page.Error;
                    return;
                }
                continuation = page.Continuation ?? "";
                cachedPage = page;
                cachedAt = DateTime.Now;
                foreach (VideoItem item in page.Items)
                {
                    item.PlaylistId = "LM";
                    AddRow(item);
                }
                countLabel.Text = T("{0} songs", rows.Count);
                hint.Text = T("OK play    ▲▼ move    Back leave");
                if (rows.Count > 0 && countLabel.Text != null)
                {
                    FocusRow(focus);
                }
            }
            finally
            {
                loading = false;
            }
        }

        private void AddRow(VideoItem item)
        {
            int y = rows.Count * (RowH + RowGap);
            var view = new View { Size = new Size(ListW, RowH), Position = new Position(0, y) };
            var highlight = new View { Size = new Size(ListW, RowH), Position = new Position(0, 0) };
            Widgets.Round(highlight, RowH / 2f);
            highlight.BackgroundColor = Theme.Accent;
            highlight.Opacity = 0f;
            view.Add(highlight);

            var title = Widgets.Label(item.Title ?? "", Theme.BodySize + 2, Theme.Text, 740, RowH);
            title.Position = new Position(28, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            view.Add(title);

            string meta = item.Channel ?? "";
            if (!string.IsNullOrEmpty(item.Views))
            {
                meta += (meta != "" ? "  ·  " : "") + item.Views;
            }
            if (!string.IsNullOrEmpty(item.Published))
            {
                meta += (meta != "" ? "  ·  " : "") + item.Published;
            }
            var channel = Widgets.Label(meta, Theme.SmallSize + 1, Theme.TextDim, 560, RowH);
            channel.Position = new Position(800, 0);
            channel.VerticalAlignment = VerticalAlignment.Center;
            view.Add(channel);

            var duration = Widgets.Label(item.Duration ?? "", Theme.SmallSize + 2, Theme.TextDim, 140, RowH);
            duration.Position = new Position(1380, 0);
            duration.VerticalAlignment = VerticalAlignment.Center;
            duration.HorizontalAlignment = HorizontalAlignment.End;
            view.Add(duration);

            rowsHost.Add(view);
            rows.Add(new Row { Item = item, View = view, Title = title, Channel = channel, Duration = duration, Highlight = highlight });
        }

        private void FocusRow(int i)
        {
            if (rows.Count == 0)
            {
                return;
            }
            focus = Math.Max(0, Math.Min(rows.Count - 1, i));
            for (int k = 0; k < rows.Count; k++)
            {
                rows[k].Highlight.Opacity = k == focus ? 1f : 0f;
            }
            // Keep the focused row inside the clip window.
            int top = focus * (RowH + RowGap);
            int windowTop = (int)-rowsHost.Position.Y;
            int windowH = VisibleRows * (RowH + RowGap);
            if (top < windowTop)
            {
                ScrollTo(top);
            }
            else if (top + RowH > windowTop + windowH)
            {
                ScrollTo(top + RowH - windowH);
            }
            // Near the end of the loaded rows: fetch the next page.
            if (focus >= rows.Count - 3 && continuation != "" && !loading)
            {
                UiGuard.Fire("MusicListScreen.More", () => Load());
            }
        }

        private void ScrollTo(int y)
        {
            scroll = Widgets.Slide(rowsHost, 0, -Math.Max(0, y), scroll, 140);
        }

        public override bool OnKey(string key)
        {
            switch (key)
            {
                case "Left":
                case "Right":
                    onShuffle = !onShuffle;
                    shuffle.SetFocused(onShuffle);
                    return true;
                case "Up":
                    if (onShuffle)
                    {
                        return true;
                    }
                    if (focus == 0)
                    {
                        onShuffle = true;
                        shuffle.SetFocused(true);
                    }
                    else
                    {
                        FocusRow(focus - 1);
                    }
                    return true;
                case "Down":
                    if (onShuffle)
                    {
                        onShuffle = false;
                        shuffle.SetFocused(false);
                        return true;
                    }
                    FocusRow(focus + 1);
                    return true;
                case "Return":
                    if (onShuffle)
                    {
                        if (allLoaded && allItems.Count > 0)
                        {
                            VideoItem pick = allItems[new Random().Next(allItems.Count)];
                            pick.PlaylistId = "LM";
                            App.Play(pick);
                        }
                        else
                        {
                            if (rows.Count > 0)
                            {
                                int pick = new Random().Next(rows.Count);
                                FocusRow(pick);
                                App.Play(rows[pick].Item);
                            }
                            if (!loadingAll)
                            {
                                UiGuard.Fire("MusicListScreen.All", LoadAll);   // walk the whole list meanwhile
                            }
                        }
                    }
                    else if (rows.Count > 0)
                    {
                        App.Play(rows[focus].Item);
                    }
                    return true;
            }
            return false;
        }

        // Walks every page of Liked Music so Random can pick from the whole list, not the loaded page.
        private static async Task LoadAll()
        {
            if (loadingAll)
            {
                return;
            }
            loadingAll = true;
            try
            {
                var all = new List<VideoItem>();
                string cont = "";
                for (int page = 0; page < 60; page++)
                {
                    FeedPage p = await YouTube.PlaylistPage("LM", cont);
                    if (p.Error != null)
                    {
                        break;
                    }
                    all.AddRange(p.Items);
                    if (string.IsNullOrEmpty(p.Continuation))
                    {
                        allItems = all;
                        allLoaded = true;
                        break;
                    }
                    cont = p.Continuation;
                }
            }
            finally
            {
                loadingAll = false;
            }
        }

        // IHoldActions: long-press OK on a row.
        public VideoItem FocusedItem => onShuffle || rows.Count == 0 ? null : rows[focus].Item;

        public void ShowActionsForFocused()
        {
            VideoItem item = FocusedItem;
            if (item != null)
            {
                App.ShowActions(item, "LM", T("Remove from Liked Music"));
            }
        }
    }
}
