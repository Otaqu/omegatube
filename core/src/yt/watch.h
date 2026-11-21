#pragma once

#include <atomic>
#include <cstdint>
#include <functional>
#include <string>
#include <thread>

namespace omega::yt {

// Watch history: the playback-statistics pings YouTube's own players send, so a video watched here
// lands in the account's history and its "watched" bar / resume point. Ported from youtube-tv /
// ytps5-sponsorblock src/yt/watch.cpp (C) 2026 SponsorBlock for PS5 contributors, GPL-3.0-or-later,
// which follows yt-dlp's _mark_watched and SmartTube.
//
// Runs on its own thread for one playback: fetches the account's tracking URLs (TV client + Bearer),
// sends the playback ping once, then a watch-time ping every 20 s of continuous playback.
class WatchReporter {
public:
    using PositionFn = std::function<int64_t()>;  // current playback position in ms (-1: not playing)

    // fallback_playback / fallback_watchtime: the tracking URLs of the player response that resolved the
    // playback itself (non-empty when the TV-client fetch below comes back without any).
    WatchReporter(std::string video_id, int64_t length_ms, std::string fallback_playback,
                  std::string fallback_watchtime, PositionFn position);
    ~WatchReporter();

private:
    void run();

    std::string video_id_;
    int64_t length_ms_;
    std::string fallback_playback_, fallback_watchtime_;
    PositionFn position_;
    std::atomic<bool> stop_{false};
    std::thread thread_;
};

}  // namespace omega::yt
