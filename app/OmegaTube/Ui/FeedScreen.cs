using System;
using System.Threading.Tasks;
using OmegaTube.Data;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using static OmegaTube.I18n;

namespace OmegaTube.Ui
{
    // A plain feed page: a title over a card grid, fed by any loader (used for Subscriptions and History
    // inside the Library). Back leaves.
    public class FeedScreen : Screen
    {
        private const int GridTop = 150;
        private readonly FeedGrid grid;

        public FeedScreen(App app, string title, Func<string, Task<FeedPage>> loader) : base(app)
        {
            Root.BackgroundColor = Theme.Glorious ? Color.Transparent : Theme.Background;
            var heading = Widgets.Label(title, Theme.TitleSize + 12, Theme.Text, 1600, 70);
            heading.Position = new Position(60, 44);
            Root.Add(heading);
            grid = new FeedGrid(1920 - 60, 1080 - GridTop) { Position = new Position(30, GridTop) };
            Root.Add(grid);
            grid.Show(loader, null, T("No videos"));
        }

        public override bool OnKey(string key)
        {
            if (key == "XF86Back" || key == "Escape")
            {
                App.Pop();
                return true;
            }
            if (key == "Return")
            {
                if (grid.FocusedItem != null)
                {
                    App.Play(grid.FocusedItem);
                }
                return true;
            }
            grid.Move(key);
            return true;
        }
    }
}
