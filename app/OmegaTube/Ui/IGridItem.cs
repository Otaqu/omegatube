using OmegaTube.Data;
using Tizen.NUI.BaseComponents;

namespace OmegaTube.Ui
{
    // What FeedGrid needs from a cell: the thumbnail card, or the plain list row (Settings: list layout).
    public interface IGridItem
    {
        VideoItem Item { get; }
        bool Filled { get; }
        View View { get; }
        void Fill();
        void Rebind(VideoItem item);
        void SetShown(bool shown);
        void SetFocused(bool focused);
        void UpdateProgress();
    }
}
