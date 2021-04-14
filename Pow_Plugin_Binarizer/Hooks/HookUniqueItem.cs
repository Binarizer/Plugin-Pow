using System;
using System.IO;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;
using HarmonyLib;
using System.Reflection;
using Ninject.Activation;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Utility;
using Heluo.Flow;
using Heluo.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfWuxia
{
    // 独特物品系统(装备重铸、随机词条等等)
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

            reforgeKey = plugin.Config.Bind("扩展功能", "切换重铸功能", KeyCode.R, "切换装备重铸功能，需要进入锻造菜单后按对应按键");
        }
        static ConfigEntry<KeyCode> reforgeKey;

        static private bool isReforgeMode = false;

        public void OnUpdate()
        {
            var uiforge = Game.UI.Get<UIForge>();
            if (Input.GetKeyDown(reforgeKey.Value) && uiforge != null && uiforge.isActiveAndEnabled)
            {
                isReforgeMode = !isReforgeMode;
                uiforge.Show();
            }
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

        // 2 额外数据读取存储
        [HarmonyPostfix, HarmonyPatch(typeof(GameDataHepler), "SaveFile", new Type[] { typeof(GameData) })]
        public static void SavePatch_SaveUnique(GameData data, ref byte[] __result)
        {
            if (ModExtensionSaveData.Instance != null)
            {
                // debug
                //Console.WriteLine("尝试存储: ");
                //string s = LZ4MessagePackSerializer.ToJson(exData, HeluoResolver.Instance);
                //Console.WriteLine(s);

                MemoryStream memoryStream = new MemoryStream();
                LZ4MessagePackSerializer.Serialize(memoryStream, ModExtensionSaveData.Instance, HeluoResolver.Instance);
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
                ModExtensionSaveData.Instance = LZ4MessagePackSerializer.Deserialize<ModExtensionSaveData>(sr.BaseStream, HeluoResolver.Instance, true);
            }
        }

        // 3 锻造菜单刷新
        private static int GetReforgePrice(Props item)
        {
            int.TryParse(item.Remark, out int value);
            int result = (int)Math.Log(item.Price + 1) * 20 + 40;
            result = (int)(result * Math.Pow(1 + value * 0.01f, 5));
            return result;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlForge), "OnShow")]
        public static void Reforge_OnShow(CtrlForge __instance)
        {
            if (isReforgeMode)
            {
                var array = Traverse.Create(__instance).Field("sort").Field("array").GetValue() as List<ForgeInfo>[];
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] != null)
                        array[i].Clear();
                    else
                        array[i] = new List<ForgeInfo>();
                }
                List<string> showed = new List<string>();
                Game.GameData.Inventory.GetDictionary();
                foreach (var key in Game.GameData.Inventory.Keys)
                {
                    Props props = Game.Data.Get<Props>(key);
                    if (props == null)
                        continue;
                    PropsCategory propsCategory = props.PropsCategory;
                    int index = -1;
                    switch (propsCategory)
                    {
                        case PropsCategory.Fist:
                        case PropsCategory.Leg:
                            index = 0;
                            break;
                        case PropsCategory.Sword:
                        case PropsCategory.Blade:
                            index = 1;
                            break;
                        case PropsCategory.Long:
                        case PropsCategory.Short:
                            index = 2;
                            break;
                        case PropsCategory.DualWielding:
                        case PropsCategory.Special:
                            index = 3;
                            break;
                        case PropsCategory.Armor:
                            index = 4;
                            break;
                    }
                    if (index >= 0)
                    {
                        ForgeInfo forgeInfo = new ForgeInfo
                        {
                            Id = key,
                            IsConditionPass = true
                        };
                        Traverse.Create(forgeInfo).Property("Item").SetValue(new Forge()
                        {
                            Id = key,
                            PropsId = key,
                        });
                        Traverse.Create(forgeInfo).Property("Equip").SetValue(props);
                        array[index].Add(forgeInfo);
                    }
                }
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlForge), "UpdateTabIsNew")]
        public static bool Reforge_UpdateNew(CtrlForge __instance)
        {
            if (isReforgeMode)
            {
                var array = Traverse.Create(__instance).Field("sort").Field("array").GetValue() as List<ForgeInfo>[];
                for (int i = 0; i < 5; i++)
                {
                    Traverse.Create(__instance).Field("view").Method("UpdateTabNotice", i, false).GetValue();//this.view.HideTabBtn(k);
                }
                return false;
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGEquipIntroduction), "UpdateWidget", new Type[] { typeof(EquipIntroductionInfo) })]
        public static void Reforge_UpdateWidget(WGEquipIntroduction __instance, EquipIntroductionInfo equipIntroductionInfo)
        {
            if (isReforgeMode && equipIntroductionInfo != null)
            {
                Traverse.Create(__instance).Field("propsName").Property("Text").SetValue("重铸 "+equipIntroductionInfo.PropsName);
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlForge), "UpdateEquipBtn", new Type[] { typeof(IScrollItme) })]
        public static bool Reforge_UpdateEquipBtn(CtrlForge __instance, IScrollItme item)
        {
            if (isReforgeMode)
            {
                var t = Traverse.Create(__instance);
                int categoryIndex = t.Field("categoryIndex").GetValue<int>();
                var array = t.Field("sort").Field("array").GetValue() as List<ForgeInfo>[];
                ForgeInfo forgeInfo = array[categoryIndex][item.Index];
                Forge item2 = forgeInfo.Item;
                Props equip = forgeInfo.Equip;
                forgeInfo.Dialog.Clear();
                int price = GetReforgePrice(equip);
                if (Game.GameData.Money < price)
                {
                    forgeInfo.IsConditionPass = false;
                    for (int j = 1; j < 3; j++)
                    {
                        forgeInfo.Dialog.Add(string.Format("SecondaryInterface031{0}", j));
                    }
                }
                InventoryData inventoryData = Game.GameData.Inventory[equip.Id];
                if (forgeInfo.Dialog.Count <= 0)
                {
                    forgeInfo.IsConditionPass = true;
                    for (int l = 1; l < 3; l++)
                    {
                        forgeInfo.Dialog.Add(string.Format("SecondaryInterface034{0}", l));
                    }
                }
                ForgeScrollInfo forgeScrollInfo = new ForgeScrollInfo
                {
                    ItemName = equip.Name,
                    ItemIcon = Game.Resource.Load<Sprite>(string.Format(GameConfig.PropsCategoryPath, (int)equip.PropsCategory)),
                    Remark = price.ToString(),
                    IsNew = false,
                    IsShowSign = item2.IsSpecial,
                    IsConditionPass = forgeInfo.IsConditionPass
                };
                if (item != null)
                {
                    item.UpdateWidget(new object[]
                    {
                        forgeScrollInfo
                    });
                }
                return false;
            }
            return true;
        }

        // 4 锻造功能重写
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlForge), "ConfirmForge")]
        public static bool Reforge_Execute(CtrlForge __instance)
        {
            if (isReforgeMode)
            {
                var t = Traverse.Create(__instance);
                int categoryIndex = t.Field("categoryIndex").GetValue<int>();
                int equipIndex = t.Field("equipIndex").GetValue<int>();
                var array = t.Field("sort").Field("array").GetValue() as List<ForgeInfo>[];
                ForgeInfo forgeInfo = array[categoryIndex][equipIndex];

                Console.WriteLine("重铸");
                string id = forgeInfo.Equip.Id;
                Props item = Game.Data.Get<Props>(id);
                int price = GetReforgePrice(item);

                Console.WriteLine("id=" + id);
                string sourceId = ModExtensionSaveData.GetUniqueSourceId(id);
                Console.WriteLine("sourceId="+sourceId);
                Props source = Game.Data.Get<Props>(sourceId);
                Console.WriteLine("sourceName=" + source.Name);

                int.TryParse(item.Remark, out int value);
                value = Math.Max(5, value + UnityEngine.Random.Range(-10, 20));
                Props newProps = RewardUniqueEquipByValue.CreateUniquePropsByValue(sourceId, value);
                newProps.Name = source.Name + "+";  // mark as reforged
                newProps.Remark = value.ToString();   // use remark to save value
                Game.GameData.Inventory.Add(newProps.Id, 1);
                Game.GameData.Inventory.Remove(id, 1);
                if (id != sourceId)
                {
                    ModExtensionSaveData.RemoveUniqueItem<Props>(id);
                }
                Game.GameData.Money -= price;

                ForgeInfo newForgeInfo = new ForgeInfo { Id = newProps.Id, IsConditionPass = true};
                Traverse.Create(newForgeInfo).Property("Item").SetValue(new Forge()
                {
                    Id = newProps.Id,
                    PropsId = newProps.Id,
                });
                Traverse.Create(newForgeInfo).Property("Equip").SetValue(newProps);
                InventoryData inventoryData = Game.GameData.Inventory[id];
                if (inventoryData != null)
                {
                    array[categoryIndex].Add(newForgeInfo);
                }
                else
                {
                    array[categoryIndex][equipIndex] = newForgeInfo;
                }

                t.Field("view").GetValue<UIForge>().UpdateEquip(array[categoryIndex].Count, false, true);
                t.Field("view").GetValue<UIForge>().UpdateMoney(Game.GameData.Money.ToString());
                return false;
            }
            return true;
        }
    }
}
