using System;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using HarmonyLib;
using BepInEx;
using Heluo;
using Heluo.Data;
using System.Reflection;
using Ninject.Activation;

namespace PathOfWuxia
{
    // 独特物品系统
    public class HookUniqueItem : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            var resolver = HeluoResolver.Instance;
            var fi = resolver.GetType().GetField("resolvers", BindingFlags.Static | BindingFlags.NonPublic);
            fi.SetValue(null, new IFormatterResolver[]
            {
                PropsEffectResolver.Instance,   // 强行加入之
                NativeDateTimeResolver.Instance,
                ContractlessStandardResolver.Instance
            });
        }

        public void OnUpdate()
        {
        }

        public static ModExtensionSaveData exData = new ModExtensionSaveData();

        // 1 更换 DataManager 为 ModDataManager
        [HarmonyPostfix, HarmonyPatch(typeof(GlobalBindingModule), "OnCreateGameData", new Type[] { typeof(IContext) })]
        public static void ModPatch_RebindData(GlobalBindingModule __instance)
        {
            // 重定向... 暂时在这里拿到Kernal 很方便
            System.Reflection.PropertyInfo propInfo = typeof(Game).GetProperty("Data");
            MethodInfo mi = propInfo.GetSetMethod(true);
            mi.Invoke(null, new object[] { new ModDataManager() });
            __instance.Rebind<IDataProvider>().To<ModDataManager>().InSingletonScope();
        }

        //// 2 额外数据读取存储
        [HarmonyPostfix, HarmonyPatch(typeof(GameDataHepler), "SaveFile", new Type[] { typeof(GameData) })]
        public static void SavePatch_SaveUnique(GameData data, ref byte[] __result)
        {
            if (exData != null)
            {
                // debug
                //Console.WriteLine("尝试存储: ");
                //string s = LZ4MessagePackSerializer.ToJson(exData, HeluoResolver.Instance);
                //Console.WriteLine(s);

                MemoryStream memoryStream = new MemoryStream();
                LZ4MessagePackSerializer.Serialize(memoryStream, exData, HeluoResolver.Instance);
                __result = __result.AddRangeToArray(memoryStream.ToArray());
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(GameDataHepler), "LoadFile", new Type[] { typeof(StreamReader) })]
        public static void SavePatch_LoadUnique(StreamReader sr, ref GameData __result)
        {
            long pos = sr.BaseStream.Position;
            if (!sr.EndOfStream)
            {
                //Console.WriteLine("尝试读取: ");
                sr.BaseStream.Position = pos;
                exData = LZ4MessagePackSerializer.Deserialize<ModExtensionSaveData>(sr.BaseStream, HeluoResolver.Instance, true);
            }
        }
    }
}
