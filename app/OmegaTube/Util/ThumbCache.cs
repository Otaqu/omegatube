using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaTube.Util
{
    // Thumbnails through one keep-alive HttpClient into a disk cache: NUI's own URL loader opens a fresh
    // connection per image, which is what made grids of thumbnails crawl. Cards then load file paths.
    public static class ThumbCache
    {
        public static event Action<string> Ready;   // a url just landed on disk

        private static string dir;
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(6);
        private static readonly HashSet<string> inflight = new HashSet<string>();

        public static void Init(string dataDir)
        {
            dir = Path.Combine(dataDir, "thumbs");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // read-only data dir: every Get falls back to the plain URL
            }
        }

        private static string PathFor(string url)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return Path.Combine(dir, sb + ".jpg");
            }
        }

        // The feed URLs carry a size token; ask for the biggest still (maxres, up to 1920x1080).
        public static string Hq(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }
            return url.Replace("/hqdefault.", "/maxresdefault.")
                      .Replace("/mqdefault.", "/maxresdefault.")
                      .Replace("/sddefault.", "/maxresdefault.")
                      .Replace("/hq720.", "/maxresdefault.")
                      .Replace("/default.", "/maxresdefault.");
        }

        // Local path when cached; otherwise starts a background download and returns null.
        public static string Get(string url)
        {
            if (string.IsNullOrEmpty(url) || dir == null)
            {
                return null;
            }
            string path = PathFor(url);
            if (File.Exists(path))
            {
                return path;
            }
            lock (inflight)
            {
                if (!inflight.Add(url))
                {
                    return null;
                }
            }
            Task.Run(async () =>
            {
                await gate.WaitAsync();
                try
                {
                    byte[] bytes = await http.GetByteArrayAsync(url);
                    if (bytes.Length > 512)
                    {
                        File.WriteAllBytes(path, bytes);
                    }
                }
                catch
                {
                    // network error: the card keeps the plain URL
                }
                finally
                {
                    lock (inflight)
                    {
                        inflight.Remove(url);
                    }
                    gate.Release();
                }
                try
                {
                    Ready?.Invoke(url);
                }
                catch
                {
                }
            });
            return null;
        }
    }
}
