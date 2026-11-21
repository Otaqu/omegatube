using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaTube.Util
{
    // DeArrow (sponsor.ajay.app): crowdsourced, de-clickbaited titles. One small GET per video id, cached as a
    // file (empty = "use YouTube's title") so a card costs nothing after its first view.
    public static class DeArrow
    {
        private static string dir;
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(4);
        private static readonly Dictionary<string, string> memo = new Dictionary<string, string>();
        private static readonly HashSet<string> inflight = new HashSet<string>();

        public static void Init(string dataDir)
        {
            dir = Path.Combine(dataDir, "dearrow");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = null;
            }
        }

        // Custom title when the crowd prefers one; null means "use YouTube's". Returns instantly from the cache,
        // starts a background fetch on a miss.
        public static string Title(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || dir == null)
            {
                return null;
            }
            lock (memo)
            {
                if (memo.TryGetValue(videoId, out string hit))
                {
                    return hit.Length == 0 ? null : hit;
                }
            }
            string path = Path.Combine(dir, videoId + ".txt");
            try
            {
                if (File.Exists(path))
                {
                    string t = File.ReadAllText(path).Trim();
                    lock (memo)
                    {
                        memo[videoId] = t;
                    }
                    return t.Length == 0 ? null : t;
                }
            }
            catch
            {
            }
            lock (inflight)
            {
                if (!inflight.Add(videoId))
                {
                    return null;
                }
            }
            Task.Run(async () =>
            {
                await gate.WaitAsync();
                string title = "";
                try
                {
                    string body = await http.GetStringAsync(
                        "https://sponsor.ajay.app/api/branding?videoID=" + Uri.EscapeDataString(videoId));
                    title = PickTitle(body) ?? "";
                    File.WriteAllText(path, title);
                }
                catch
                {
                    // offline / API down: try again next time
                }
                finally
                {
                    lock (inflight)
                    {
                        inflight.Remove(videoId);
                    }
                    gate.Release();
                }
                lock (memo)
                {
                    memo[videoId] = title;
                }
            });
            return null;
        }

        // titles[]: first entry that is not the original title and not heavily downvoted.
        private static string PickTitle(string body)
        {
            try
            {
                object root = Json.Parse(body);
                List<object> titles = Json.Arr(root, "titles");
                if (titles == null)
                {
                    return null;
                }
                foreach (object t in titles)
                {
                    if (Json.Bool(t, "original", true) || Json.Bool(t, "removed", false))
                    {
                        continue;
                    }
                    if (Json.Num(t, "votes", 0) < -1)
                    {
                        continue;
                    }
                    string title = Json.Str(t, "title", "").Trim();
                    if (title.Length > 0)
                    {
                        return title;
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
