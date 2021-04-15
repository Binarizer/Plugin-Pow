using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Utility;
using UnityEngine.UI;
using Heluo.Flow;

namespace PathOfWuxia
{
    // 队伍管理
    public class HookTeamManage : IHook
    {
        static ConfigEntry<bool> teamManageOn;
        static ConfigEntry<int> teamMemberMax;
        static ConfigEntry<KeyCode> teamMemberAdd;
        static ConfigEntry<KeyCode> teamMemberRemove;
        static ConfigEntry<KeyCode> teamMemberRemoveAll;
        static ConfigEntry<KeyCode> communityRemove;

        public void OnRegister(BaseUnityPlugin plugin)
        {
            teamManageOn = plugin.Config.Bind("自由组队", "开启自由组队模式", false,
                new ConfigDescription("开启自由组队模式，用来调整队伍、通过剧情等，剑击江湖mod请打开", null, new ConfigurationManagerAttributes { Order = 2 }));
            if (teamManageOn.Value)
            {
                BindConfig(plugin);
            }

            teamManageOn.SettingChanged += (o, e) =>
            {
                if (teamManageOn.Value)
                {
                    BindConfig(plugin);
                }
                else
                {
                    plugin.Config.Remove(teamMemberMax.Definition);
                    plugin.Config.Remove(teamMemberAdd.Definition);
                    plugin.Config.Remove(teamMemberRemove.Definition);
                    plugin.Config.Remove(teamMemberRemoveAll.Definition);
                    plugin.Config.Remove(communityRemove.Definition);
                }
                UpdateTeamDisplay();
            };
        }

        static void BindConfig(BaseUnityPlugin plugin)
        {
            teamMemberMax = plugin.Config.Bind("自由组队", "最大队伍人数", 4,
                new ConfigDescription("最大队伍人数", new AcceptableValueRange<int>(4, 9), new ConfigurationManagerAttributes { Order = 1 }));
            teamMemberAdd = plugin.Config.Bind("自由组队", "队伍加入当前角色", KeyCode.F3, "加入队伍");
            teamMemberRemove = plugin.Config.Bind("自由组队", "队伍移除当前角色", KeyCode.F4, "移出队伍");
            teamMemberRemoveAll = plugin.Config.Bind("自由组队", "队伍移除全部队友", KeyCode.F5, "清空队伍");
            communityRemove = plugin.Config.Bind("自由组队", "队伍从社群移除", KeyCode.F6, "删除社群，之后可于行囊->任务->人物加入道具重新加回");
        }

        static GameObject TeamMemberObject;         // 队伍图标挂接点
        static List<UITeamMemberInfo> TeamMembers;

        public void OnUpdate()
        {
            if (teamManageOn.Value && TeamMemberObject != null && TeamMemberObject.activeInHierarchy)
            {
                var UIHome = Game.UI.Get<UIHome>();
                if (UIHome == null)
                    return;
                if (Input.GetKeyDown(teamMemberAdd.Value))
                {
                    CharacterMapping cm = GlobalLib.GetUICharacterMapping();
                    if (cm != null && cm.Id != GameConfig.Player && !Game.GameData.Party.Contains(cm.Id))
                    {
                        Game.GameData.Party.AddParty(cm.Id, false);
                        int count = Traverse.Create(UIHome).Property("controller").Field("characterMapping").Property("Count").GetValue<int>();
                        UIHome.UpdateCommunity(count);
                    }
                }
                if (Input.GetKeyDown(teamMemberRemove.Value))
                {
                    CharacterMapping cm = GlobalLib.GetUICharacterMapping();
                    if (cm != null && cm.Id != GameConfig.Player && Game.GameData.Party.Contains(cm.Id))
                    {
                        Game.GameData.Party.RemoveParty(cm.Id);
                        int count = Traverse.Create(UIHome).Property("controller").Field("characterMapping").Property("Count").GetValue<int>();
                        UIHome.UpdateCommunity(count);
                    }
                }
                if (Input.GetKeyDown(teamMemberRemoveAll.Value))
                {
                    foreach (var pi in Game.GameData.Party.GetRange(0, Game.GameData.Party.Count))
                    {
                        if (pi != GameConfig.Player)
                        {
                            Game.GameData.Party.RemoveParty(pi);
                        }
                    }
                    int count = Traverse.Create(UIHome).Property("controller").Field("characterMapping").Property("Count").GetValue<int>();
                    UIHome.UpdateCommunity(count);
                }
                if (Input.GetKeyDown(communityRemove.Value))
                {
                    CharacterMapping cm = GlobalLib.GetUICharacterMapping();
                    if (cm != null && cm.Id != GameConfig.Player)
                    {
                        Game.GameData.Community[cm.Id].isOpen = false;
                        Game.GameData.NurturanceOrder.CloseCommunityOrder(cm.Id);
                        if (Game.GameData.Party.Contains(cm.Id))
                            Game.GameData.Party.RemoveParty(cm.Id);
                        Traverse.Create(UIHome).Property("controller").Method("OnShow").GetValue();
                        Traverse.Create(UIHome).Property("controller").Method("HideCommunity").GetValue();
                        Traverse.Create(UIHome).Property("controller").Method("UpdateCommunity").GetValue();
                        string joinPropsId = "p_npcj_" + cm.Id;
                        if (!Game.GameData.Inventory.ContainsKey(joinPropsId))
                        {
                            new RewardProps
                            {
                                method = Method.Add,
                                propsId = "p_npcj_" + cm.Id,
                                value = 1
                            }.GetValue();
                        }
                    }
                }
            }
        }


        static List<PartyInfo> GetPartyInfo()
        {
            List<CharacterMapping> list = new List<CharacterMapping>();
            foreach (string text in Game.GameData.Party)
            {
                CharacterMapping characterMapping = new CharacterMapping();
                if (text == GameConfig.Player)
                {
                    characterMapping.Id = (characterMapping.InfoId = (characterMapping.ExteriorId = text));
                }
                else
                {
                    Npc npc = Game.Data.Get<Npc>(text);
                    if (npc == null)
                    {
                        Console.WriteLine("找不到ID: " + text + "的的NPC, 請檢查Npc.txt");
                        continue;
                    }
                    characterMapping.Id = text;
                    characterMapping.InfoId = npc.CharacterInfoId;
                    characterMapping.ExteriorId = npc.ExteriorId;
                }
                list.Add(characterMapping);
            }
            List<PartyInfo> result = new List<PartyInfo>();
            foreach (CharacterMapping characterMapping2 in list)
            {
                CharacterInfoData characterInfoData = Game.GameData.Character[characterMapping2.InfoId];
                CharacterExteriorData characterExteriorData = Game.GameData.Exterior[characterMapping2.InfoId];
                result.Add(new PartyInfo
                {
                    Protrait = characterExteriorData.Protrait,
                    Element = characterInfoData.Element,
                });
            }
            return result;
        }

        static void UpdateTeamDisplay()
        {
            var UIHome = Game.UI.Get<UIHome>();
            if (UIHome == null)
                return;
            var community = Traverse.Create(UIHome).Field("community").GetValue<WGTabScroll>();
            if (TeamMemberObject == null && community != null)
            {
                TeamMemberObject = new GameObject("Mod_TeamMember");
                TeamMemberObject.transform.SetParent(community.transform, false);
                TeamMemberObject.transform.position = new Vector3(1700, 700, 0);
                TeamMembers = new List<UITeamMemberInfo>();
                GameObject gameObject3 = new GameObject("Mod_TeamMember_Title");
                gameObject3.transform.SetParent(TeamMemberObject.transform, false);
                gameObject3.transform.localPosition = new Vector3(-870, 120, 0);
                var text = gameObject3.AddComponent<Text>();
                text.font = Game.Resource.Load<Font>("Assets/Font/kaiu.ttf");
                text.fontSize = 25;
                text.text = "当前队伍";
                text.alignment = TextAnchor.MiddleLeft;
            }
            if (!teamManageOn.Value)
            {
                TeamMemberObject.SetActive(false);
                return;
            }
            TeamMemberObject.SetActive(true);
            var teamInfos = GetPartyInfo();
            int i = 0;
            for (; i < teamInfos.Count; i++)
            {
                if (TeamMembers.Count <= i)
                {
                    var teamUI = Game.UI.Get<UITeamMember>();
                    var memberUI = Traverse.Create(teamUI).Field("infos").GetValue<List<UITeamMemberInfo>>()[0];
                    var memberUICopy = GameObject.Instantiate<UITeamMemberInfo>(memberUI, TeamMemberObject.transform, false);
                    memberUICopy.transform.localPosition = new Vector3(i * 75, -50, 0);
                    memberUICopy.HP.gameObject.SetActive(false);
                    memberUICopy.MP.gameObject.SetActive(false);
                    TeamMembers.Add(memberUICopy);
                }
                TeamMembers[i].gameObject.SetActive(true);
                TeamMembers[i].UpdateInfo(teamInfos[i]);
            }
            for (; i < TeamMembers.Count; i++)
            {
                TeamMembers[i].gameObject.SetActive(false);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIHome), "UpdateCommunity", new Type[] { typeof(int) })]
        public static void UpdateTeamMembers()
        {
            UpdateTeamDisplay();
        }

        // 队伍人数
        [HarmonyPrefix, HarmonyPatch(typeof(Party), "AddParty", new Type[] { typeof(string), typeof(bool) })]
        public static bool FixPartyCount(Party __instance, string id, bool isNeed)
        {
            if (__instance.Contains(id))
            {
                return false;
            }
            if (__instance.Count >= teamMemberMax.Value)
            {
                return false;
            }
            if (!isNeed)
            {
                __instance.Add(id);
                return false;
            }
            if (__instance.Count > 1)
            {
                string item = __instance[1];
                __instance[1] = id;
                __instance.Add(item);
                return false;
            }
            __instance.Add(id);
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Party), "GetPartyByIndex", new Type[] { typeof(int) })]
        public static bool FixPartyCount2(Party __instance, int index, ref string __result)
        {
            if (index < 0 || index >= teamMemberMax.Value || index >= __instance.Count)
            {
                __result = string.Empty;
                return false;
            }
            __result = __instance[index];
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Party), "GetPartyCharacterExteriorData")]
        public static bool FixPartyCount3(Party __instance, ref CharacterExteriorData[] __result)
        {
            CharacterExteriorData[] array = new CharacterExteriorData[teamMemberMax.Value];
            for (int i = 1; i < array.Length; i++)
            {
                string partyByIndex = __instance.GetPartyByIndex(i);
                if (!partyByIndex.IsNullOrEmpty())
                {
                    array[i] = Game.GameData.Exterior[partyByIndex];
                }
            }
            __result = array;
            return false;
        }
    }
}
