using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Hypocycloid.Utils
{
    public class ConfigReader
    {
        public static IEnumerator GetConfigFile(string path, Action<string> resp)
        {
            byte[] data = null;
            yield return Download(path, value => data = value);

            string content = data == null ? null : Encoding.UTF8.GetString(data);
            resp(content?.TrimStart('\uFEFF'));
        }

        public static IEnumerator GetConfigFile(string path, Action<byte[]> resp) =>
            Download(path, resp);

        static IEnumerator Download(string path, Action<byte[]> resp)
        {
            string url = GetStreamingAssetUrl(path);
            LogHelper.Log("IO", "Streaming asset URL -> " + url);

            using UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogHelper.LogError(
                    "IO",
                    $"Failed to load streaming asset '{path}' ({request.result}, HTTP {request.responseCode}): {request.error}");
                resp(default);
                yield break;
            }

            resp(request.downloadHandler.data);
        }

        static string GetStreamingAssetUrl(string path)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            string relativePath = path.Replace('\\', '/').TrimStart('/');
            string fullPath = $"{root}/{relativePath}";

            // Android and WebGL already expose URI-based paths (jar:file://, http://, etc.).
            // Desktop and iOS expose filesystem paths, which UnityWebRequest requires as file URIs.
            return fullPath.Contains("://") ? fullPath : new Uri(fullPath).AbsoluteUri;
        }

        /// <summary>
        /// custom config reader
        /// </summary>
        public static void ReadByLine(string content, Action<string> lineAction)
        {
            using StringReader reader = new(content);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.StartsWith("#") || line.Trim() == "") continue;

                lineAction(line);
            }
        }
    }
}
