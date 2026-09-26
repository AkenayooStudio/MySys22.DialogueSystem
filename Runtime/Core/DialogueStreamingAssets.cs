using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MySys22.DialogueEngine.Core
{
    public static class DialogueStreamingAssets
    {

#if UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
        private const bool StreamingPlatform = true;
#else
        private const bool StreamingPlatform = false;
#endif

        public static bool ForceAsync;

        public static bool IsStreamingPlatform => StreamingPlatform;

        public static bool RequiresAsync => ForceAsync || (!Application.isEditor && StreamingPlatform);

        public static string Root => DialoguePaths.Root;

        public static string PathFor(string relative)
            => Path.Combine(DialoguePaths.Root, relative.Replace('/', Path.DirectorySeparatorChar));

        public static string UrlFor(string relative)
        {
            string combined = IsUrl(relative)
                ? relative.Replace('\\', '/')
                : DialoguePaths.Root.Replace('\\', '/') + "/" + relative.TrimStart('/');

            if (IsUrl(combined)) return combined;

#if UNITY_WEBGL
            return combined;
#elif UNITY_ANDROID
            return combined.StartsWith("jar:", StringComparison.OrdinalIgnoreCase)
                ? combined
                : "jar:file://" + combined;
#else
            return combined.StartsWith("/", StringComparison.Ordinal)
                ? "file://" + combined
                : "file:///" + combined;
#endif
        }

        private static bool IsUrl(string value)
            => !string.IsNullOrEmpty(value) &&
               (value.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https:", StringComparison.OrdinalIgnoreCase));

        public static bool TryReadTextDirect(string relative, out string text)
        {
            text = null;
            if (RequiresAsync) return false;

            try
            {
                string path = PathFor(relative);
                if (!File.Exists(path)) return false;
                text = File.ReadAllText(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string ReadText(string relative)
        {
            if (TryReadTextDirect(relative, out string text)) return text;
            DialogueLogger.LogWarning($"Direct read unavailable for '{relative}'.");
            return null;
        }

        public static async Task<string> ReadTextAsync(string relative)
        {
            if (TryReadTextDirect(relative, out string text)) return text;

            using (var request = UnityWebRequest.Get(UrlFor(relative)))
            {
                var operation = request.SendWebRequest();
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DialogueLogger.LogWarning($"Cannot read '{relative}': {request.error}");
                    return null;
                }
                return request.downloadHandler.text;
            }
        }

        public static bool Exists(string relative)
        {
            if (RequiresAsync) return false;

            try { return File.Exists(PathFor(relative)); }
            catch (Exception) { return false; }
        }

        public static string[] ListFiles(string relativeFolder, string pattern)
        {
            if (RequiresAsync || string.IsNullOrEmpty(relativeFolder)) return Array.Empty<string>();

            try
            {
                string path = PathFor(relativeFolder);
                if (!Directory.Exists(path)) return Array.Empty<string>();
                return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        public static string[] ListFolders(string relativeFolder)
        {
            if (RequiresAsync || string.IsNullOrEmpty(relativeFolder)) return Array.Empty<string>();

            try
            {
                string path = PathFor(relativeFolder);
                if (!Directory.Exists(path)) return Array.Empty<string>();
                return Directory.GetDirectories(path);
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }
}
