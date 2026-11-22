using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaTube.Util
{
    // DeArrow (sponsor.ajay.app): crowdsourced, de-clickbaited titles and video frames. One small branding GET
    // per video id (cached as a JSON file), then the DeArrow thumbnail service for the frame. Everything is
    // cached on disk, so a card costs nothing after its first view. Any failure just leaves YouTube's data.
    public static class DeArrow
    {
        public static event Action<string> ThumbReady;   // a video id's DeArrow frame just landed on disk

        private static string dir;
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(4);
        private static readonly Dictionary<string, (string Title, double Time)> memo = new Dictionary<string, (string, double)>();
        private static readonly HashSet<string> inflightMeta = new HashSet<string>();
        private static readonly HashSet<string> inflightThumb = new HashSet<string>();

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

        // Custom title when the crowd prefers one; null means "use YouTube's". Instant from the cache,
        // starts a background branding fetch on a miss.
        public static string Title(string videoId)
        {
            (string Title, double Time)? meta = Meta(videoId);
            return meta != null && meta.Value.Title.Length > 0 ? meta.Value.Title : null;
        }

        // Local path of the DeArrow frame (JPEG) when it is already on disk; otherwise starts the download
        // (branding first when needed) and fires ThumbReady when it lands. Null: keep YouTube's thumbnail.
        public static string Thumbnail(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || dir == null)
            {
                return null;
            }
            string jpg = Path.Combine(dir, videoId + ".jpg");
            if (File.Exists(jpg))
            {
                return jpg;
            }
            (string Title, double Time)? meta = Meta(videoId);
            if (meta != null && meta.Value.Time >= 0)
            {
                StartThumb(videoId, meta.Value.Time, jpg);
            }
            return null;
        }

        // ---------------------------------------------------------------- internals

        private static (string Title, double Time)? Meta(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || dir == null)
            {
                return null;
            }
            lock (memo)
            {
                if (memo.TryGetValue(videoId, out (string, double) hit))
                {
                    return hit;
                }
            }
            string path = Path.Combine(dir, videoId + ".json");
            try
            {
                if (File.Exists(path))
                {
                    object root = Json.Parse(File.ReadAllText(path));
                    var got = (Json.Str(root, "t", ""), Json.Num(root, "u", -1));
                    lock (memo)
                    {
                        memo[videoId] = got;
                    }
                    return got;
                }
            }
            catch
            {
            }
            lock (inflightMeta)
            {
                if (!inflightMeta.Add(videoId))
                {
                    return null;
                }
            }
            Task.Run(async () =>
            {
                await gate.WaitAsync();
                string title = "";
                double time = -1;
                try
                {
                    string body = await http.GetStringAsync(
                        "https://sponsor.ajay.app/api/branding?videoID=" + Uri.EscapeDataString(videoId));
                    (title, time) = ParseBranding(body);
                    File.WriteAllText(path, Json.Write(new Dictionary<string, object> { ["t"] = title, ["u"] = time }));
                }
                catch
                {
                    // offline / API down: try again next time
                }
                finally
                {
                    lock (inflightMeta)
                    {
                        inflightMeta.Remove(videoId);
                    }
                    gate.Release();
                }
                lock (memo)
                {
                    memo[videoId] = (title, time);
                }
            });
            return null;
        }

        // titles[]: first entry that is not the original title and not heavily downvoted.
        // thumbnails[]: the community's frame timestamp; otherwise randomTime (DeArrow's own pick).
        private static (string Title, double Time) ParseBranding(string body)
        {
            string title = "";
            double time = -1;
            try
            {
                object root = Json.Parse(body);
                List<object> titles = Json.Arr(root, "titles");
                if (titles != null)
                {
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
                        string s = Json.Str(t, "title", "").Trim();
                        if (s.Length > 0)
                        {
                            title = s;
                            break;
                        }
                    }
                }
                List<object> thumbs = Json.Arr(root, "thumbnails");
                if (thumbs != null)
                {
                    foreach (object t in thumbs)
                    {
                        if (Json.Bool(t, "original", false))
                        {
                            continue;
                        }
                        double ts = Json.Num(t, "timestamp", -1);
                        if (ts >= 0)
                        {
                            time = ts;
                            break;
                        }
                    }
                }
                if (time < 0)
                {
                    double rnd = Json.Num(root, "randomTime", -1);
                    if (rnd > 0)
                    {
                        time = rnd;
                    }
                }
            }
            catch
            {
            }
            return (title, time);
        }

        private static void StartThumb(string videoId, double time, string jpg)
        {
            lock (inflightThumb)
            {
                if (!inflightThumb.Add(videoId))
                {
                    return;
                }
            }
            Task.Run(async () =>
            {
                await gate.WaitAsync();
                try
                {
                    string url = "https://dearrow-thumb.ajay.app/api/v1/getThumbnail?videoID=" +
                                 Uri.EscapeDataString(videoId) + "&time=" +
                                 time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    byte[] bytes = await http.GetByteArrayAsync(url);
                    if (bytes.Length > 512)
                    {
                        File.WriteAllBytes(jpg, bytes);
                        try
                        {
                            ThumbReady?.Invoke(videoId);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                    // not generated yet (404) or offline: the card keeps YouTube's thumbnail
                }
                finally
                {
                    lock (inflightThumb)
                    {
                        inflightThumb.Remove(videoId);
                    }
                    gate.Release();
                }
            });
        }
    }
}
