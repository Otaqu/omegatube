using OmegaTube.Data;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace OmegaTube.Ui
{
    // The list layout's row: one line per video (title, channel · views · age, duration), no thumbnails.
    public class ListRow : View, IGridItem
    {
        public const int RowHeight = 56;

        public VideoItem Item { get; private set; }
        public bool Filled { get; private set; }
        public View View => this;

        private readonly int width;
        private readonly View highlight;
        private TextLabel title, meta, duration;

        public ListRow(VideoItem item, int width)
        {
            Item = item;
            this.width = width;
            Size = new Size(width, RowHeight);
            highlight = new View { Size = new Size(width, RowHeight) };
            Widgets.Round(highlight, RowHeight / 2f);
            highlight.BackgroundColor = Theme.Accent;
            highlight.Opacity = 0f;
            Add(highlight);
        }

        public void Fill()
        {
            if (Filled)
            {
                return;
            }
            Filled = true;
            title = Widgets.Label(Item.Title ?? "", Theme.BodySize + 1, Theme.Text, width - 700, RowHeight);
            title.Position = new Position(24, 0);
            title.VerticalAlignment = VerticalAlignment.Center;
            Add(title);
            meta = Widgets.Label(Meta(), Theme.SmallSize + 1, Theme.TextDim, 560, RowHeight);
            meta.Position = new Position(width - 660, 0);
            meta.VerticalAlignment = VerticalAlignment.Center;
            Add(meta);
            duration = Widgets.Label(Item.Duration ?? "", Theme.SmallSize + 2, Theme.TextDim, 120, RowHeight);
            duration.Position = new Position(width - 140, 0);
            duration.VerticalAlignment = VerticalAlignment.Center;
            duration.HorizontalAlignment = HorizontalAlignment.End;
            Add(duration);
        }

        private string Meta()
        {
            string m = Item.Channel ?? "";
            if (!string.IsNullOrEmpty(Item.Views))
            {
                m += (m != "" ? "  ·  " : "") + Item.Views;
            }
            if (!string.IsNullOrEmpty(Item.Published))
            {
                m += (m != "" ? "  ·  " : "") + Item.Published;
            }
            return m;
        }

        public void Rebind(VideoItem item)
        {
            Item = item;
            if (Filled)
            {
                title.Text = item.Title ?? "";
                meta.Text = Meta();
                duration.Text = item.Duration ?? "";
            }
        }

        public void SetShown(bool shown) => Widgets.SetVisible(this, shown);

        public void UpdateProgress()
        {
            // no watched-progress bar in the list layout
        }

        public void SetFocused(bool focused) => highlight.Opacity = focused ? 1f : 0f;
    }
}
