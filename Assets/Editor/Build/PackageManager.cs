using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hypocycloid.Editor
{
    // Managing build functions
    public class PackageManager : EditorWindow
    {
        static readonly string configPath = Application.dataPath + "/Configs/";
        static readonly string resPath = Application.dataPath + "/Assets/AssetBundles";
        static readonly string exportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/AssetBundles"));

        [MenuItem(EditorCommons.CTX + "Export Config")]
        static void ExportConfigData()
        {
            if (!Directory.Exists(configPath))
            {
                Debug.LogError("Missing config path!");
                return;
            }

            if (!Directory.Exists(exportPath))
            {
                Debug.LogError("Missing AB path!");
                return;
            }

            using FileStream fs = new(configPath + "config.txt", FileMode.Create);
            StringBuilder sb = new();
            string[] files = Directory.GetFiles(exportPath);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                string md5 = EditorHelper.GetMD5ToFile(files[i]);
                string fileData = fileName + "\t" + md5;
                sb.Append(fileData);
                if (i != files.Length - 1)
                    sb.AppendLine();
            }

            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            fs.Write(data, 0, data.Length);
            Debug.Log("Export Done. path = " + exportPath);
        }

        [MenuItem("Tools/Export Individual AssetBundle")]
        static void ExportAssetBuidle()
        {
            if (!Directory.Exists(resPath))
                Directory.CreateDirectory(resPath);
            BuildPipeline.BuildAssetBundles(
                resPath,
                BuildAssetBundleOptions.None,
                BuildTarget.StandaloneWindows64
            );
            AssetDatabase.Refresh();
        }

    }
}
