using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo.UI;
using Heluo.Data;
using System.ComponentModel;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("物品过滤与排序")]
    [Description("物品过滤与排序")]
    class HookPropsFilterAndSort : IHook
    {
        enum SortMode
        {
            None,
            minToMax,
            maxToMin,
            minToMaxByType,
            maxToMinByType
        }
        private static ConfigEntry<bool> giftFilter;
        private static ConfigEntry<SortMode> giftSortMode;
        private static ConfigEntry<SortMode> equipSortMode;


        public void OnRegister(PluginBinarizer plugin)
        {
            giftFilter = plugin.Config.Bind("界面改进", "礼物过滤", false, "在送礼界面过滤礼物，只留下能增加当前角色好感度的礼物");
            giftSortMode = plugin.Config.Bind("界面改进", "礼物排序", SortMode.None, "在送礼界面对礼物进行排序 None-不排序 minToMax-按好感从小到大排序 maxToMin-按好感从大到小排序 minToMaxByType-先按类型排序，后按好感从小到大排序 maxToMinByType-先按类型排序，后按好感从大到小排序");
            equipSortMode = plugin.Config.Bind("界面改进", "装备排序", SortMode.None, "在装备界面对装备进行排序 None-不排序 minToMax-按加值从小到大排序 maxToMin-按加值从大到小排序 minToMaxByType-先按类型排序，后按加值从小到大排序 maxToMinByType-先按类型排序，后按加值从大到小排序");
        }


        
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlGiftInvertoryWindow), "SetSortInventory", new Type[] { typeof(InventoryWindowInfo) })]
        public static bool GiftInvertoryPatch_propsFilterAndSort(ref CtrlGiftInvertoryWindow __instance, ref InventoryWindowInfo inventoryWindowInfo)
        {

            if (giftFilter.Value)
            {
                List<PropsInfo> sort = inventoryWindowInfo.Sort;
                CharacterMapping mapping = inventoryWindowInfo.Mapping;

                List<PropsInfo> canAddExpGiftlist = new List<PropsInfo>();
                for (int index = 0; index < sort.Count; index++)
                {
                    Props item = sort[index].Item;

                    if (item.PropsEffect != null)
                    {
                        for (int i = 0; i < item.PropsEffect.Count; i++)
                        {
                            PropsEffect propsEffect = item.PropsEffect[i];
                            if (propsEffect is PropsFavorable)
                            {
                                PropsFavorable propsFavorable = propsEffect as PropsFavorable;
                                if (mapping != null && mapping.InfoId != null && !mapping.InfoId.Equals(string.Empty))
                                {
                                    if (propsFavorable.Npcid == mapping.InfoId)
                                    {
                                        canAddExpGiftlist.Add(sort[index]);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                sort.Clear();
                sort.AddRange(canAddExpGiftlist.ToArray());
                canAddExpGiftlist.Clear();

                GiftSortByCategory(sort, mapping.Id);
                GiftSortByNumber(sort, mapping.Id);
            }
            return true;
        }

        public static void GiftSortByCategory(List<PropsInfo> sort,string mappingId)
        {
            if(giftSortMode.Value != SortMode.None)
            {
                //反正物品总共也没多少，开销应该不大，选择排序就完事了
                //先按分类排序
                if(giftSortMode.Value == SortMode.maxToMinByType || giftSortMode.Value == SortMode.minToMaxByType)
                {
                    for (int i = 0; i < sort.Count; i++)
                    {
                        int minIndex = i;
                        PropsCategory minPropsCategory = getPropsCategory(sort, i);
                        for (int j = i + 1; j < sort.Count; j++)
                        {
                            PropsCategory jPropsCategory = getPropsCategory(sort, j);
                            if ((giftSortMode.Value == SortMode.minToMaxByType && minPropsCategory > jPropsCategory)
                                || (giftSortMode.Value == SortMode.maxToMinByType && minPropsCategory > jPropsCategory)
                            )
                            {
                                minIndex = j;
                                minPropsCategory = getPropsCategory(sort, j);
                            }
                        }
                        if (i != minIndex)
                        {
                            PropsInfo temp = sort[i];
                            sort[i] = sort[minIndex];
                            sort[minIndex] = temp;
                        }
                    }
                }
            }
        }
        public static void GiftSortByNumber(List<PropsInfo> sort, string mappingId)
        {
            if (giftSortMode.Value != SortMode.None)
            {
                //反正物品总共也没多少，开销应该不大，用选择排序就完事了

                //在按大小排序
                for (int i = 0; i < sort.Count; i++)
                {
                    PropsCategory iPropsCategory = getPropsCategory(sort, i);
                    int minIndex = i;
                    int minFavExp = getFavExp(sort, mappingId, i);
                    PropsCategory minPropsCategory = getPropsCategory(sort, i);
                    for (int j = i + 1; j < sort.Count; j++)
                    {
                        int jFavExp = getFavExp(sort, mappingId, j);
                        PropsCategory jPropsCategory = getPropsCategory(sort, j);
                        if ((giftSortMode.Value == SortMode.minToMax && minFavExp > jFavExp)
                            || (giftSortMode.Value == SortMode.maxToMin && minFavExp < jFavExp)
                            || (giftSortMode.Value == SortMode.minToMaxByType && iPropsCategory == jPropsCategory && minFavExp > jFavExp)
                            || (giftSortMode.Value == SortMode.maxToMinByType && iPropsCategory == jPropsCategory && minFavExp < jFavExp)
                        )
                        {
                            minIndex = j;
                            minFavExp = getFavExp(sort, mappingId, j);
                            minPropsCategory = getPropsCategory(sort, j);
                        }
                    }
                    if (i != minIndex)
                    {
                        PropsInfo temp = sort[i];
                        sort[i] = sort[minIndex];
                        sort[minIndex] = temp;
                    }
                }
            }
        }

        public static PropsCategory getPropsCategory(List<PropsInfo> sort,int index)
        {
            Props iItem = sort[index].Item;
            return iItem.PropsCategory;
        }
        public static int getFavExp(List<PropsInfo> sort, string mappingId, int index)
        {
            Props iItem = sort[index].Item;
            int favExp = 0;
            for (int i = 0; i < iItem.PropsEffect.Count; i++)
            {
                PropsEffect propsEffect = iItem.PropsEffect[i];
                if (propsEffect is PropsFavorable)
                {
                    PropsFavorable propsFavorable = propsEffect as PropsFavorable;
                    if (mappingId != null && !mappingId.Equals(string.Empty))
                    {
                        if (propsFavorable.Npcid == mappingId)
                        {
                            return propsFavorable.Value;
                        }
                    }
                }
            }
            return favExp;
        }
        
        //平时的装备界面
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlBattleFormInventory), "SetSortInventory", new Type[] { typeof(InventoryWindowInfo) })]
        public static bool BattleInvertoryPatch_propsFilterAndSort(ref CtrlBattleFormInventory __instance, ref InventoryWindowInfo inventoryWindowInfo)
        {
            List<PropsInfo> sort = inventoryWindowInfo.Sort;


            Console.WriteLine(string.Format("CtrlBattleFormInventory, Index: {0}", 1));
            EquipSortByCategory(sort);
            Console.WriteLine(string.Format("CtrlBattleFormInventory, Index: {0}", 2));
            EquipSortByAttack(sort);
            Console.WriteLine(string.Format("CtrlBattleFormInventory, Index: {0}", 3));
            return true;
        }

        //战斗中的装备界面
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlInventoryWindow), "SetSortInventory", new Type[] { typeof(InventoryWindowInfo) })]
        public static bool BattleInvertoryPatch2_propsFilterAndSort(ref CtrlInventoryWindow __instance, ref InventoryWindowInfo inventoryWindowInfo)
        {
            List<PropsInfo> sort = inventoryWindowInfo.Sort;


            Console.WriteLine(string.Format("CtrlInventoryWindow, Index: {0}", 1));
            EquipSortByCategory(sort);
            Console.WriteLine(string.Format("CtrlInventoryWindow, Index: {0}", 2));
            EquipSortByAttack(sort);
            Console.WriteLine(string.Format("CtrlInventoryWindow, Index: {0}", 3));
            return true;
        }

        public static void EquipSortByCategory(List<PropsInfo> sort)
        {
            if (equipSortMode.Value != SortMode.None)
            {
                //反正物品总共也没多少，开销应该不大，用选择排序就完事了
                //先按分类排序
                if (equipSortMode.Value == SortMode.maxToMinByType || equipSortMode.Value == SortMode.minToMaxByType)
                {
                    for (int i = 0; i < sort.Count; i++)
                    {
                        int minIndex = i;
                        PropsCategory minPropsCategory = getPropsCategory(sort, i);
                        for (int j = i + 1; j < sort.Count; j++)
                        {
                            PropsCategory jPropsCategory = getPropsCategory(sort, j);
                            if ((equipSortMode.Value == SortMode.minToMaxByType && minPropsCategory > jPropsCategory)
                                || (equipSortMode.Value == SortMode.maxToMinByType && minPropsCategory > jPropsCategory)
                            )
                            {
                                minIndex = j;
                                minPropsCategory = getPropsCategory(sort, j);
                            }
                        }
                        if (i != minIndex)
                        {
                            PropsInfo temp = sort[i];
                            sort[i] = sort[minIndex];
                            sort[minIndex] = temp;
                        }
                    }
                }
            }
        }
        public static void EquipSortByAttack(List<PropsInfo> sort)
        {
            if (equipSortMode.Value != SortMode.None)
            {
                //反正物品总共也没多少，开销应该不大，用选择排序就完事了

                //在按攻击力大小排序
                for (int i = 0; i < sort.Count; i++)
                {
                    PropsCategory iPropsCategory = getPropsCategory(sort, i);
                    int minIndex = i;
                    int minAttack = getAttack(sort, i);
                    PropsCategory minPropsCategory = getPropsCategory(sort, i);
                    for (int j = i + 1; j < sort.Count; j++)
                    {
                        int jAttack = getAttack(sort, j);
                        PropsCategory jPropsCategory = getPropsCategory(sort, j);
                        if ((equipSortMode.Value == SortMode.minToMax && minAttack > jAttack)
                            || (equipSortMode.Value == SortMode.maxToMin && minAttack < jAttack)
                            || (equipSortMode.Value == SortMode.minToMaxByType && iPropsCategory == jPropsCategory && minAttack > jAttack)
                            || (equipSortMode.Value == SortMode.maxToMinByType && iPropsCategory == jPropsCategory && minAttack < jAttack)
                        )
                        {
                            minIndex = j;
                            minAttack = getAttack(sort, j);
                            minPropsCategory = getPropsCategory(sort, j);
                        }
                    }
                    if (i != minIndex)
                    {
                        PropsInfo temp = sort[i];
                        sort[i] = sort[minIndex];
                        sort[minIndex] = temp;
                    }
                }
            }
        }
        public static int getAttack(List<PropsInfo> sort, int index)
        {
            Console.WriteLine(string.Format("getAttack, Index: {0}", 1));
            Props iItem = sort[index].Item;
            Console.WriteLine(string.Format("getAttack, iItem: {0}", iItem));
            int attack = 0;
            if(iItem.PropsEffect != null)
            {
                for (int i = 0; i < iItem.PropsEffect.Count; i++)
                {
                    PropsEffect propsEffect = iItem.PropsEffect[i];
                    Console.WriteLine(string.Format("getAttack, propsEffect: {0}", propsEffect));
                    if (propsEffect is PropsBattleProperty)
                    {
                        PropsBattleProperty propsBattleProperty = propsEffect as PropsBattleProperty;
                        Console.WriteLine(string.Format("getAttack, propsBattleProperty: {0}", propsBattleProperty));
                        if (propsBattleProperty.Property == CharacterProperty.Attack || propsBattleProperty.Property == CharacterProperty.Defense)
                        {
                            Console.WriteLine(string.Format("getAttack, propsBattleProperty.Property: {0}", propsBattleProperty.Property));
                            attack = propsBattleProperty.Value;
                            break;
                        }
                    }
                }
            }
            Console.WriteLine(string.Format("getAttack, attack: {0}", attack));
            return attack;
        }

        //当铺的装备界面
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlPawnshop), "Sort" )]
        public static void BattleInvertoryPatch3_propsFilterAndSort(ref CtrlPawnshop __instance)
        {
            List<PropsInfo> sort = Traverse.Create(__instance).Field("sort").GetValue<List<PropsInfo>>();
            UIPawnshop view = Traverse.Create(__instance).Field("view").GetValue<UIPawnshop>();

            EquipSortByCategory(sort);
            EquipSortByAttack(sort);


            view.UpdateProps(sort.Count, false);
        }
    }
}
