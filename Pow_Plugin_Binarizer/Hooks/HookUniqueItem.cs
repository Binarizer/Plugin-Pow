using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Flow;
using Heluo.Utility;
using System.Reflection.Emit;
using System.Reflection;
using Ninject.Activation;

namespace PathOfWuxia
{
    // 独特物品系统
    public class HookUniqueItem : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
        }

        public void OnUpdate()
        {
        }

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
    }
}
