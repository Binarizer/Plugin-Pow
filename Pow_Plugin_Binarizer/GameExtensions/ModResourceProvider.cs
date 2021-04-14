// Mod 加载器 - 修改自ExternalResourceProvider
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Crc;
using Ionic.Zip;
using UnityEngine;
using Heluo;
using Heluo.Resource;

namespace PathOfWuxia
{
    public class ModResourceProvider : ObjectResourceProvider
    {
        public ModResourceProvider(ICoroutineRunner runner, string modPath) : base(runner)
        {
            this.ExternalDirectory = modPath;
            string text = this.ExternalDirectory + "/Config.zip";
            if (File.Exists(text))
            {
                this.zip = ZipFile.Read(text);
                this.allEntry = this.zip.ToDictionary((ZipEntry x) => x.FileName.ToLower(), (ZipEntry x) => x);
            }
        }

        public override T Load<T>(string path)
        {
            Type typeFromHandle = typeof(T);
            if (typeFromHandle == typeof(GameObject))
            {
                // 读取绝对路径资源，官方资源带路径组合需要被忽略
                int start = path.IndexOf("@[");
                if (start != -1)
                {
                    int end = path.IndexOf("]");
                    string actualPath = path.Substring(start + 2, end - start - 2);
                    Console.WriteLine("检测到Mod绝对路径AssetBundle资源=" + actualPath);
                    var assetBundleProvider = Successor as AssetBundleResourceProvider;
                    return assetBundleProvider.Load<T>(actualPath);
                }
            }
            if (typeFromHandle != typeof(AudioClip) && typeFromHandle != typeof(Texture2D) && typeFromHandle != typeof(Sprite))
                return default(T);
            if (string.IsNullOrEmpty(this.ExternalDirectory))
                return default(T);
            string path2 = Path.Combine(this.ExternalDirectory, path);
            if (!File.Exists(path2))
            {
                path2 = path2.Replace("/" + GameConfig.Language, "");
                if (!File.Exists(path2))
                    return default(T);
            }
            if (typeFromHandle == typeof(AudioClip))
            {
                string fullPath = Path.GetFullPath(path2);
                WWW www = new WWW(fullPath);
                if (www.error != null)
                {
                    Debug.LogError("www " + www.error);
                    return default(T);
                }
                while (!www.isDone)
                {
                }
                AudioClip audioClip = www.GetAudioClip();
                if (audioClip == null)
                {
                    Debug.LogError("Failed!! file://" + fullPath);
                    return default(T);
                }
                audioClip.LoadAudioData();
                return audioClip as T;
            }
            else if (typeFromHandle == typeof(Texture2D) || typeFromHandle == typeof(Sprite))
            {
                byte[] data = File.ReadAllBytes(path2);
                Texture2D texture2D = new Texture2D(2, 2);
                texture2D.LoadImage(data);
                if (typeFromHandle == typeof(Sprite))
                {
                    Rect rect = new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height);
                    Vector2 pivot = new Vector2((float)(texture2D.width / 2), (float)(texture2D.height / 2));
                    return Sprite.Create(texture2D, rect, pivot) as T;
                }
                return texture2D as T;
            }
            return default(T);
        }

        public override byte[] LoadBytes(string path)
        {
            if (string.IsNullOrEmpty(this.ExternalDirectory))
            {
                return null;
            }
            return this.GetDataFromPath(path) ?? this.GetDataFromZip(path);
        }

        private byte[] GetDataFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            string path2 = Path.Combine(this.ExternalDirectory, path);
            if (!File.Exists(path2))
            {
                path2 = path2.Replace("/" + GameConfig.Language, "");
                if (!File.Exists(path2))
                    return null;
            }
            return File.ReadAllBytes(path2);
        }

        private byte[] GetDataFromZip(string path)
        {
            path = path.ToLower();
            if (this.allEntry == null)
            {
                return null;
            }
            if (!this.allEntry.ContainsKey(path))
            {
                return null;
            }
            ZipEntry zipEntry = this.allEntry[path];
            byte[] array = new byte[zipEntry.UncompressedSize];
            using (CrcCalculatorStream crcCalculatorStream = zipEntry.OpenReader())
            {
                if (crcCalculatorStream.Read(array, 0, array.Length) != array.Length)
                {
                    Heluo.Logger.LogError("Zip file size not equal !! " + path, "GetDataFromZip", "C:\\PathOfWuxia\\PathOfWuxia\\Assets\\Scripts\\Resource\\Provider\\ExternalResourceProvider.cs", 119);
                }
            }
            return array;
        }

        private string ExternalDirectory = string.Empty;
        private ZipFile zip;
        private Dictionary<string, ZipEntry> allEntry;
    }
}
