// 多Mod加载器 - 修改自ExternalResourceProvider
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Crc;
using Ionic.Zip;
using UnityEngine;
using Heluo;
using Heluo.Resource;
using Newtonsoft.Json;
using System.Text;
using HarmonyLib;

namespace PathOfWuxia
{
    public class ModResourceProvider : ObjectResourceProvider
    {
        public ModResourceProvider(ICoroutineRunner runner, string modBaseDir, string[] modPaths) : base(runner)
        {
            for (int i = 0; i < modPaths.Length; ++i)
            {
                string dir = Path.Combine(modBaseDir, modPaths[i]);
                if (Directory.Exists(dir))
                {
                    Console.WriteLine("检测到Mod路径: " + dir);
                    ModDirectories.Add(dir);

                    string zip = Path.Combine(dir, "Config.zip");
                    if (File.Exists(zip))
                    {
                        Console.WriteLine("检测到压缩格式Mod档案: " + zip);
                        ZipFile zipFile = ZipFile.Read(zip);
                        ZipEntries.Add(dir, zipFile.ToDictionary((ZipEntry x) => x.FileName.ToLower(), (ZipEntry x) => x));
                    }

                    // 扩展AssetBundleSheet
                    string extraSheetFile = Path.Combine(dir, "AssetBundleSheet.sheet");
                    if (File.Exists(extraSheetFile))
                    {
                        Console.WriteLine("检测到扩展AssetBundleSheet: " + extraSheetFile);
                        byte[] array = File.ReadAllBytes(extraSheetFile);
                        AssetBundleSheet modSheet = JsonConvert.DeserializeObject<AssetBundleSheet>(Encoding.UTF8.GetString(array));
                        AssetBundleSheet sourceSheet = Traverse.Create(BundleManagerBySheet.Instance()).Field("bundleSheet").GetValue<AssetBundleSheet>();
                        Console.WriteLine(string.Format("扩展Sheet bundle={0}, file={1}", modSheet.BundleList.Count, modSheet.FilesInfo.Count));
                        Console.WriteLine(string.Format("原始Sheet bundle={0}, file={1}", sourceSheet.BundleList.Count, sourceSheet.FilesInfo.Count));
                        foreach (var bundle in modSheet.BundleList)
                        {
                            sourceSheet.BundleList.Add(bundle.Key, bundle.Value);
                        }
                        foreach (var file in modSheet.FilesInfo)
                        {
                            sourceSheet.FilesInfo.Add(file.Key, file.Value);
                        }
                        Console.WriteLine(string.Format("合并Sheet bundle={0}, file={1}", sourceSheet.BundleList.Count, sourceSheet.FilesInfo.Count));
                    }
                }
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

            foreach (string dir in ModDirectories)
            {
                string modPath = Path.Combine(dir, path);
                if (!File.Exists(modPath))
                {
                    modPath = modPath.Replace("/" + GameConfig.Language, "");
                    if (!File.Exists(modPath))
                        continue;
                }
                // 文件找到且符合要求：只检查第一个找到的文件，满足则返回，不满足不继续找
                if (typeFromHandle == typeof(AudioClip))
                {
                    string fullPath = Path.GetFullPath(modPath);
                    WWW www = new WWW(fullPath);
                    if (www.error != null)
                    {
                        Console.WriteLine("www " + www.error);
                        return default(T);
                    }
                    while (!www.isDone)
                    {
                    }
                    AudioClip audioClip = www.GetAudioClip();
                    if (audioClip == null)
                    {
                        Console.WriteLine("Failed!! file://" + fullPath);
                        return default(T);
                    }
                    audioClip.LoadAudioData();
                    return audioClip as T;
                }
                else if (typeFromHandle == typeof(Texture2D) || typeFromHandle == typeof(Sprite))
                {
                    byte[] data = File.ReadAllBytes(modPath);
                    Texture2D texture2D = new Texture2D(2, 2);
                    texture2D.LoadImage(data);
                    if (typeFromHandle == typeof(Sprite))
                    {
                        Rect rect = new Rect(0f, 0f, texture2D.width, texture2D.height);
                        Vector2 pivot = new Vector2((texture2D.width / 2), (texture2D.height / 2));
                        return Sprite.Create(texture2D, rect, pivot) as T;
                    }
                    return texture2D as T;
                }
            }
            return default(T);
        }

        // 指定路径读取
        public byte[] LoadBytesFromDir(string dir, string path)
        {
            if (ZipEntries.ContainsKey(dir) && ZipEntries[dir].ContainsKey(path))
                return GetDataFromZip(ZipEntries[dir][path]);

            string modPath = Path.Combine(dir, path);
            if (!File.Exists(modPath))
            {
                modPath = modPath.Replace("/" + GameConfig.Language, "");
                if (!File.Exists(modPath))
                    return null;
            }
            return GetDataFromFile(modPath);
        }

        public override byte[] LoadBytes(string path)
        {
            foreach (string dir in ModDirectories)
            {
                byte[] result = LoadBytesFromDir(dir, path);
                if (result != null)
                    return result;
            }
            return null;
        }

        private byte[] GetDataFromFile(string filename)
        {
            return File.ReadAllBytes(filename);
        }

        private byte[] GetDataFromZip(ZipEntry zipEntry)
        {
            byte[] array = new byte[zipEntry.UncompressedSize];
            using (CrcCalculatorStream crcCalculatorStream = zipEntry.OpenReader())
            {
                if (crcCalculatorStream.Read(array, 0, array.Length) != array.Length)
                {
                    Console.WriteLine("Zip file size not equal !! " + zipEntry.FileName, "GetDataFromZip", "C:\\PathOfWuxia\\PathOfWuxia\\Assets\\Scripts\\Resource\\Provider\\ExternalResourceProvider.cs", 119);
                }
            }
            return array;
        }

        public readonly List<string> ModDirectories = new List<string>();
        private readonly Dictionary<string, Dictionary<string, ZipEntry>> ZipEntries = new Dictionary<string, Dictionary<string, ZipEntry>>();
    }
}
