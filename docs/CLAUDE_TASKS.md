# Claude Code task brief — OmegaTube fork (Tizen TV)

Repo: `/home/user1/projects/omegatube-fork` (fork of IIyn/omegatube). App: `org.omegatube.app` 1.6.0,
C# Tizen NUI (`app/OmegaTube/`) + C++ core `libomegacore.so` (`core/`). Target: Samsung Tizen 5.5 TV
(LS43DM702UUXUF), 1920x1080 layout, remote-control navigation.

Build (from repo root):

    export PATH=/home/user1/.dotnet:/home/user1/tizen-studio/tools/ide/bin:/home/user1/tizen-studio/tools:$PATH
    TIZEN_PROFILE=omegatube2 scripts/build-app.sh

Install to the TV (TV must be on; `sdb shell` is dead on this firmware — never rely on it):

    sdb connect 192.168.124.6:26101
    sdb -s 192.168.124.6:26101 install app/OmegaTube/bin/Release/tizen60/org.omegatube.app-1.6.0.tpk

Screenshots: only via phone photo of the TV. The app has a "stats for nerds" overlay (30 lines of the
in-app log) — ask the user for a photo of it when runtime diagnosis is needed.

Rules: do NOT commit `app/OmegaTube/Config/Secrets.cs` (gitignored; holds an OpenRouter key). No sudo.
Keep the C++ at C++17, no new third-party dependencies. Tizen 5.5 NUI has no backdrop blur and no
View.CornerRadius — rounded corners are done with nine-patch/mask images (`Theme.Image("round9.9")` etc).

---

## Task 1 (priority): write watch history to the YouTube account

Symptom: videos watched in the app do NOT appear in the account's YouTube watch history
(youtube.com → History). Everything else authed works: like/dislike, subscriptions, playlists
(youtubei POSTs with `Authorization: Bearer <oauth token>`). The OAuth token comes from the TV
device-code flow.

What the app does today (see `core/src/yt/watch.cpp`, `WatchReporter`):

1. It fetches `playbackTracking` URLs from a `youtubei/v1/player` response.
2. Pings go to `www.youtube.com/api/stats/playback` and `.../watchtime` with `Authorization: Bearer`
   plus the `cpn` of the playback session.
3. Logged results on the TV (verbatim):
   - `history: tracking via VISIONOS http 400`
   - `history: tracking via TVHTML5 status=UNPLAYABLE reason=The page needs to be reloaded.`
   - `history: tracking via ANDROID http 400`
   - `history: no account tracking (http 400) — pings will use the playback response's URLs`
   - `history: playback ping <cpn> -> ok`   ← HTTP 2xx, yet nothing lands in the account history
4. So: with a Bearer token attached, the player endpoint refuses (400) for VISIONOS/ANDROID and returns
   the anti-bot UNPLAYABLE wall for TVHTML5. The fallback URLs come from the *anonymous* playback
   response, so the pings succeed but are attributed to an anonymous session, not the account.

Reference point: SmartTube (yuliskov/SmartTube) had the same breakage around Aug 2026 (issue #6016)
and fixed it in 32.15 (Aug 29). We believe they get account-bound tracking URLs from an *authed*
playback response, which implies they play through a client+token combination the player endpoint
still accepts — possibly with a PO token / BotGuard involvement.

Question to answer (read the SmartTube source and, if useful, yt-dlp/newpipe discussions):

- Which exact client (`clientName`, `clientVersion`, headers, context fields) does SmartTube use for
  the authed `youtubei/v1/player` request that carries `playbackTracking`, and how does it authenticate?
- Does that request need a PO token (`serviceIntegrityDimensions.poToken`) or a BotGuard/visitor-data
  dance? If yes, what is the *minimal* viable implementation for a C++ app with no JS engine?
- Is there a cheaper path: e.g. reusing the token's own session by fetching the tracking URLs from a
  client whose player request still succeeds with Bearer (which one?), or writing history through
  another endpoint entirely?
- Deliverable: a concrete plan for our code — which client/headers/body to send, where the token comes
  from, what to change in `Client::player_tracking()` (`core/src/yt/innertube.cpp`), `WatchReporter`
  (`core/src/yt/watch.cpp`) and `core/src/core.cpp` (`start_youtube` wires the reporter). Keep it
  inside our existing `net::fetch` HTTP layer.

Acceptance: after a 1-minute watch in the app, the video appears in the account's history on
youtube.com; the stats overlay shows the tracking URLs came from an account-bound answer.

## Task 2: finish the light mode sweep

`app/OmegaTube/Ui/Theme.cs` has `SetLight(bool)` flipping Background/PageTint/Panel/PanelLine/Text/
TextDim/TextMuted; chrome that carries white PNG icons (rail `RailBg`, pills `OnControl`, card info
bars `CardText/CardTextDim`) stays dark on purpose. Settings toggle: General → "Light mode".

Problem: many screens still hardcode dark-theme colors (`Color.White`, dark rgba scrims, `Theme.Scrim`,
glass tints) or assume dark backgrounds, so with Light mode on some surfaces are unreadable.

Job: sweep `app/OmegaTube/Ui/*.cs` (HomeScreen, PlayerScreen overlays, SettingsScreen, LibraryScreen,
FeedScreen, ChannelScreen, SearchScreen, AccountScreen, MusicListScreen, PlaylistScreen, ActionMenu,
Widgets, Card, ListRow, FeedGrid, Rail) and route every color through Theme tokens or the
`Theme.Light` flag. Icons are white PNGs — where a surface flips light, either keep that control dark
(pill/rail pattern) or document the exception. Acceptance: toggle Light mode, walk every screen, all
text readable, icons visible. Do not change the dark theme's look.

## Task 3 (optional): paged menus for long settings lists

Settings choice rows with long option lists (YouTube language/region, subtitle language, audio track)
still cycle with Left/Right (wrapping). Convert them to the paged menu pattern used by the other choice
rows (`SettingsScreen.OnKey` → `ActionMenu` popup), with scrolling inside the popup. Keep it consistent
with the existing menu styling.

---

Context for testing: the TV is only reachable when it is powered on; coordinate with the user before
relying on it. A test build installs in seconds with the sdb command above. The app writes logs visible
in the stats overlay; `OLog`/`OLOG` in C++ and `NativeCore.Log` in C# feed it.
