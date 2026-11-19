using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // The AI summary of a video, over the current screen: a scrolling text panel.
    //  - ▲▼ scroll, Back closes. Shown at once with "Summarizing…", filled when the model answers.
    public class SummaryDialog : View
    {
        private const int CardW = 1400, CardH = 820, Pad = 40, ScrollStep = 140;

        private readonly TextLabel text;
        private readonly View host;
        private readonly Action close;
        private Animation scroll;
        private int offset, contentHeight;

        public SummaryDialog(string title, Action close)
        {
            this.close = close;
            Size = new Size(1920, 1080);
            BackgroundColor = Theme.Scrim;

            var card = Widgets.Panel(Theme.Panel, CardW, CardH, Theme.PanelRadius);
            card.Position = new Position((1920 - CardW) / 2, (1080 - CardH) / 2);
            Add(card);

            var label = Widgets.Label(title, Theme.TitleSize - 4, Theme.Text, CardW - 2 * Pad, 64);
            label.Position = new Position(Pad, 28);
            card.Add(label);

            var hint = Widgets.Label(T("▲▼ scroll    Back close"), Theme.SmallSize + 1, Theme.TextDim, CardW - 2 * Pad, 34);
            hint.Position = new Position(Pad, CardH - 56);
            card.Add(hint);

            var clip = new View
            {
                Size = new Size(CardW - 2 * Pad, CardH - 190),
                Position = new Position(Pad, 110),
                ClippingMode = ClippingModeType.ClipChildren,
            };
            card.Add(clip);
            host = new View();
            clip.Add(host);
            text = Widgets.Label(T("Summarizing…"), Theme.BodySize + 2, Theme.Text, CardW - 2 * Pad, CardH - 200);
            text.MultiLine = true;
            host.Add(text);
        }

        public void Show(string content, string error)
        {
            text.Text = error != null ? T("⚠ {0}", error) : content;
            // Rough line count at ~52 characters a line: enough to clamp the scroll.
            int lines = Math.Max(1, (text.Text ?? "").Length / 52 + 1);
            contentHeight = lines * 34;
            offset = 0;
            scroll = Widgets.Slide(host, 0, 0, scroll);
        }

        public void OnKey(string key)
        {
            switch (key)
            {
                case "Up":
                    ScrollTo(offset - ScrollStep);
                    break;
                case "Down":
                    ScrollTo(offset + ScrollStep);
                    break;
                case "XF86Back":
                case "Escape":
                    close();
                    break;
            }
        }

        private void ScrollTo(int y)
        {
            int max = Math.Max(0, contentHeight - (CardH - 200));
            offset = Math.Max(0, Math.Min(max, y));
            scroll = Widgets.Slide(host, 0, -offset, scroll, 160);
        }
    }
}
