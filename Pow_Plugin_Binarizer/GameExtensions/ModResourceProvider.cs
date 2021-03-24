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
    // Token: 0x020009BE RID: 2494
    public class ModResourceProvider : ObjectResourceProvider
    {
        // Token: 0x060038D7 RID: 14551 RVA: 0x00110FB4 File Offset: 0x0010F1B4
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

        // Token: 0x060038D8 RID: 14552
        public override T Load<T>(string path)
        {
            Type typeFromHandle = typeof(T);
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

        // Token: 0x060038D9 RID: 14553
        public override byte[] LoadBytes(string path)
        {
            if (string.IsNullOrEmpty(this.ExternalDirectory))
            {
                return null;
            }
            return this.GetDataFromPath(path) ?? this.GetDataFromZip(path);
        }

        // Token: 0x060038DA RID: 14554 RVA: 0x0011120C File Offset: 0x0010F40C
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

        // Token: 0x060038DB RID: 14555 RVA: 0x00111240 File Offset: 0x0010F440
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

        // Token: 0x0400305A RID: 12378
        private string ExternalDirectory = string.Empty;

        // Token: 0x0400305C RID: 12380
        private ZipFile zip;

        // Token: 0x0400305D RID: 12381
        private Dictionary<string, ZipEntry> allEntry;
    }
}
