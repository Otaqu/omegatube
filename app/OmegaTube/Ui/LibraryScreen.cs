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
    // The signed-in account's lists: Watch Later, Liked videos and Liked Music (the YouTube "Library").
    //  - ▲▼ move, OK opens the list as a playlist page, Back leaves.
    //  The lists are the account's private playlists: WL / LL / LM, read with the access token.
    public class LibraryScreen : Screen
    {
        private const int ListX = 120, ListY = 250, ItemW = 640, ItemH = 84, ItemGap = 16;

        private class Entry
        {
            public string Title, Icon, PlaylistId;
            public Action Open;
            public Widgets.PillButton Button;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly View listHost;
        private readonly TextLabel hint;
        private int focus;

        public LibraryScreen(App app) : base(app)
        {
            Root.BackgroundColor = Theme.Glorious ? Color.Transparent : Theme.Background;

            var title = Widgets.Label(T("Library"), Theme.TitleSize + 12, Theme.Text, 1200, 70);
            title.Position = new Position(ListX, 64);
            Root.Add(title);
            var subtitle = Widgets.Label(T("Your lists: Watch Later, liked videos and liked music."),
                                         Theme.BodySize + 2, Theme.TextDim, 1600);
            subtitle.Position = new Position(ListX, 140);
            Root.Add(subtitle);

            MusicListScreen.Prefetch();   // Liked Music loads in the background: opening it shows the songs at once
            listHost = new View { Size = new Size(ItemW, 1080) };
            listHost.Position = new Position(ListX, ListY);
            Root.Add(listHost);

            hint = Widgets.Label("", Theme.SmallSize + 1, Theme.TextDim, 1600, 40);
            hint.Position = new Position(ListX, 1000);
            Root.Add(hint);

            bool signedIn = YouTube.SignedIn();
            if (signedIn)
            {
                AddAction(T("Subscriptions"), "subscriptions",
                          () => App.Push(new FeedScreen(App, T("Subscriptions"), c => YouTube.Browse("FEsubscriptions", c))));
                AddAction(T("History"), "history",
                          () => App.Push(new FeedScreen(App, T("History"), c => YouTube.Browse("FEhistory", c))));
                Add(T("Watch Later"), "view_list", "WL");
                Add(T("Liked videos"), "thumb_up", "LL");
                Add(T("Liked Music"), "music", "LM");
                hint.Text = T("OK open    Back leave");
                FocusEntry(0);
            }
            else
            {
                var msg = Widgets.Label(T("Sign in to your account (Account menu) to see Watch Later and your liked lists."),
                                        Theme.BodySize + 2, Theme.TextDim, 1500, 80);
                msg.Position = new Position(0, 0);
                listHost.Add(msg);
                hint.Text = T("Back leave");
            }
        }

        private void Add(string title, string icon, string playlistId)
        {
            var button = new Widgets.PillButton(title, icon, ItemW, ItemH);
            button.Position = new Position(0, entries.Count * (ItemH + ItemGap));
            listHost.Add(button);
            entries.Add(new Entry { Title = title, Icon = icon, PlaylistId = playlistId, Button = button });
        }

        private void AddAction(string title, string icon, Action open)
        {
            var button = new Widgets.PillButton(title, icon, ItemW, ItemH);
            button.Position = new Position(0, entries.Count * (ItemH + ItemGap));
            listHost.Add(button);
            entries.Add(new Entry { Title = title, Icon = icon, Open = open, Button = button });
        }

        private void FocusEntry(int i)
        {
            focus = Math.Max(0, Math.Min(entries.Count - 1, i));
            for (int k = 0; k < entries.Count; k++)
            {
                entries[k].Button.SetFocused(k == focus);
            }
        }

        private void Open(Entry entry)
        {
            if (entry.Open != null)
            {
                entry.Open();
                return;
            }
            if (entry.PlaylistId == "LM")
            {
                App.Push(new MusicListScreen(App));   // text list + shuffle, like the YT Music queue
                return;
            }
            var list = new VideoItem
            {
                Kind = "playlist",
                PlaylistId = entry.PlaylistId,
                Title = entry.Title,
                Channel = "",
            };
            App.Push(new PlaylistScreen(App, list));
        }

        public override bool OnKey(string key)
        {
            switch (key)
            {
                case "Up":
                    FocusEntry(focus - 1);
                    return true;
                case "Down":
                    FocusEntry(focus + 1);
                    return true;
                case "Return":
                    if (entries.Count > 0)
                    {
                        Open(entries[focus]);
                    }
                    return true;
            }
            return false;
        }
    }
}
