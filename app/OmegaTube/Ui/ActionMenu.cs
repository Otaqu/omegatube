using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace OmegaTube.Ui
{
    // Long-press OK menu over the home feed: what can be done to a video without opening it.
    public class ActionMenu : View
    {
        private const int CardW = 720, RowH = 76, RowGap = 14, Pad = 28;

        private readonly List<(string Label, Action Run)> items;
        private readonly List<Widgets.PillButton> buttons = new List<Widgets.PillButton>();
        private readonly Action close;
        private int focus;

        public ActionMenu(string title, List<(string Label, Action Run)> items, Action close)
        {
            this.items = items;
            this.close = close;
            Size = new Size(1920, 1080);
            BackgroundColor = Theme.Scrim;

            int h = 130 + items.Count * (RowH + RowGap) + 24;
            var card = Widgets.Panel(Theme.Panel, CardW, h, Theme.PanelRadius);
            card.Position = new Position((1920 - CardW) / 2, (1080 - h) / 2);
            Add(card);

            var label = Widgets.Label(title, Theme.TitleSize - 6, Theme.Text, CardW - 2 * Pad, 64);
            label.Position = new Position(Pad, 26);
            card.Add(label);

            int y = 108;
            foreach ((string text, Action _) in items)
            {
                var b = new Widgets.PillButton(text, null, CardW - 2 * Pad, RowH);
                b.Position = new Position(Pad, y);
                card.Add(b);
                buttons.Add(b);
                y += RowH + RowGap;
            }
            SetFocus(0);
        }

        private void SetFocus(int i)
        {
            focus = (i + items.Count) % items.Count;
            for (int k = 0; k < buttons.Count; k++)
            {
                buttons[k].SetFocused(k == focus);
            }
        }

        public void OnKey(string key)
        {
            switch (key)
            {
                case "Up":
                    SetFocus(focus - 1);
                    break;
                case "Down":
                    SetFocus(focus + 1);
                    break;
                case "Return":
                {
                    Action run = items[focus].Run;
                    close();
                    run?.Invoke();
                    break;
                }
                case "XF86Back":
                case "Escape":
                    close();
                    break;
            }
        }
    }
}
