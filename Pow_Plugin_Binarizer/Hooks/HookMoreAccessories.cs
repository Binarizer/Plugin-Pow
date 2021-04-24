using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Battle;
using Heluo.Utility;

namespace PathOfWuxia
{
    // 饰品栏增加
    public class HookMoreAccessories : IHook
    {
        public IEnumerable<Type> GetRegisterTypes()
        {
            return new Type[] { GetType() };
        }
        public void OnRegister(BaseUnityPlugin plugin)
        {
            moreAccessories = plugin.Config.Bind<int>("扩展功能", "多重饰品栏", 0, "大于0时可装备多个饰品");
            moreAccessories.SettingChanged += OnMoreAccessoriesChange;
        }

        public void OnUpdate()
        {
        }

        static void OnMoreAccessoriesChange(object o, EventArgs e)
        {
            moreAccessories.Value = Math.Max(0, moreAccessories.Value);
            _accessoryIndex = Math.Min(_accessoryIndex, moreAccessories.Value);
        }

        static ConfigEntry<int> moreAccessories;
        static int _accessoryIndex = 0;	// 饰品当前id

        [HarmonyPrefix, HarmonyPatch(typeof(CharacterEquipData), "GetEquip", new Type[] { typeof(EquipType) })]
        public static bool AssessoryPatch_GetEquip(CharacterEquipData __instance, EquipType type, ref Props __result)
        {
            if (__instance[type].IsNullOrEmpty())
            {
                __result = null;
                return false;
            }
            if (EquipType.Jewelry == type)
            {
                string[] array = __instance[type].Split(new char[]
                {
                    '|'
                });
                if (_accessoryIndex >= array.Length)
                {
                    __result = null;
                }
                else
                {
                    __result = Game.Data.Get<Props>(array[_accessoryIndex]);
                }
                return false;
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CharacterInfoData), "AttachEquipEffect", new Type[] { })]
        public static void AssessoryPatch_AttachEquipEffect1(CharacterInfoData __instance)
        {
            for (int i = 1; i <= moreAccessories.Value; ++i)
            {
                _accessoryIndex = (_accessoryIndex + 1) % (1 + moreAccessories.Value);
                Props equip = __instance.Equip.GetEquip(EquipType.Jewelry);
                Traverse.Create(__instance).Method("AttachEquipEffect", new Type[] { typeof(Props) }, new object[] { equip }).GetValue();
            }
            _accessoryIndex = 0;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaUnit), "Initialize")]
        public static void AssessoryPatch_AttachEquipEffect2(WuxiaUnit __instance)
        {
            for (int i = 1; i <= moreAccessories.Value; ++i)
            {
                _accessoryIndex = (_accessoryIndex + 1) % (1 + moreAccessories.Value);
                Props equip = __instance.info.Equip.GetEquip(EquipType.Jewelry);
                __instance.AttachEquipEffect(equip);
            }
            _accessoryIndex = 0;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Inventory), "UseProps", new Type[] { typeof(string), typeof(CharacterInfoData) })]
        public static bool AssessoryPatch_UseProps(Inventory __instance, string id, CharacterInfoData user)
        {
            Props props = __instance.GetProps(id);
            if (props == null)
            {
                return false;
            }
            PropsCategory propsCategory = props.PropsCategory;
            if (propsCategory == PropsCategory.Accessories)
            {
                Props equip = user.Equip.GetEquip(EquipType.Jewelry);
                if (equip != null)
                {
                    Traverse.Create(__instance).Method("DettachPropsEffect", new Type[] { typeof(Props), typeof(CharacterInfoData) }, new object[] { equip, user }).GetValue();
                }
                string s = user.Equip[EquipType.Jewelry];
                if (s == null)
                {
                    s = string.Empty;
                }
                List<string> list = s.Split(new char[]
                {
                        '|'
                }).ToList<string>();
                while (_accessoryIndex >= list.Count)
                {
                    list.Add("");
                }
                list[_accessoryIndex] = props.Id;
                user.Equip[EquipType.Jewelry] = string.Join("|", list);
                if (equip != null)
                {
                    __instance.Add(equip.Id, 1, false);
                }
                string str = "UISE_Equip01.wav";
                Traverse.Create(__instance).Method("AttachPropsEffect", new Type[] { typeof(Props), typeof(CharacterInfoData) }, new object[] { props, user }).GetValue();
                __instance.Remove(props.Id, 1);
                var e = new Heluo.Events.SoundEventArgs
                {
                    Parent = Game.UI.Canvas.gameObject,
                    SoundPath = GameConfig.UISoundFolder + str,
                    Delay = 0f,
                    Is3D = false,
                    SoundVolume = 1f
                };
                Game.Event.Send(e);
                return false;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlEquip), "OnCharacterChanged", new Type[] { typeof(CharacterMapping) })]
        public static bool AssessoryPatch_UI1()
        {
            _accessoryIndex = 0;
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(UIEquip), "Show")]
        public static bool AssessoryPatch_UI2(UIEquip __instance)
        {
            WGBtn[] componentsInChildren = __instance.GetComponentsInChildren<WGBtn>();
            WGBtn wgbtn = componentsInChildren[componentsInChildren.Length - 1];
            var trans = wgbtn.transform.parent.Find("switchAcc");
            if (trans == null)
            {
                GameObject gameObject = new GameObject("switchAcc");
                gameObject.transform.SetParent(wgbtn.transform.parent, false);
                gameObject.transform.localPosition = wgbtn.transform.localPosition + new Vector3(-180f, 0f, 0f);
                gameObject.transform.localScale *= 0.5f;
                var accSwitch = gameObject.AddComponent<WGBtn>();
                gameObject.AddComponent<Image>().sprite = Game.Resource.Load<Sprite>("Image/UI/UICharacter/Info_btn_next.png");
                accSwitch.PointerClick = new WGBtn.TriggerEvent();
                accSwitch.PointerClick.AddListener(delegate (BaseEventData eventData)
                {
                    var controller = Traverse.Create(__instance).Field("controller").GetValue<CtrlEquip>();
                    _accessoryIndex = (_accessoryIndex + 1) % (1 + moreAccessories.Value);
                    var mapping = Traverse.Create(controller).Field("mapping").GetValue<CharacterMapping>();
                    controller.ShowEquip(EquipType.Jewelry, Game.GameData.Character[mapping.InfoId].Equip.GetEquip(EquipType.Jewelry));
                });
                gameObject.SetActive(moreAccessories.Value > 0);
                AssessoryPatch_UI3(__instance);
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(UIEquip), "UpdateAccessories", new Type[] { typeof(PropsIntroductionInfo) })]
        public static bool AssessoryPatch_UI3(UIEquip __instance)
        {
            var accessories = Traverse.Create(__instance).Field("accessories").GetValue<WGPropsIntroduction>();
            Text[] componentsInChildren = accessories.GetComponentsInChildren<Text>();
            var accIdText = componentsInChildren[0];
            if (moreAccessories.Value > 0)
            {
                accIdText.text = StringTool.GetStringTable("Equip_CurrentAccessories") + " " + (_accessoryIndex + 1);
            }
            else
            {
                accIdText.text = StringTool.GetStringTable("Equip_CurrentAccessories");
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIEquip), "Hide")]
        public static void AssessoryPatch_UI4(UIEquip __instance)
        {
            _accessoryIndex = 0;
        }
    }
}
