using OmegaTube.Data;

namespace OmegaTube.Ui
{
    // A screen whose focused item can take the long-press OK menu (App routes the hold to it).
    public interface IHoldActions
    {
        VideoItem FocusedItem { get; }
        void ShowActionsForFocused();
    }
}
