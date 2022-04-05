using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Data;
using Heluo.Flow;
using Heluo;
using Heluo.UI;
using UnityEngine.UI;
using UnityEngine;
using Heluo.Utility;
using Heluo.Controller;
using Heluo.Manager;

namespace PathOfWuxia
{
	public class HookElective : IHook
	{
		private static ConfigEntry<bool> multiCourseSelect;

		public void OnRegister(PluginBinarizer plugin)
		{
			multiCourseSelect = plugin.Config.Bind("扩展功能", "自由选课", false, "每次选课可自由选择上哪些课");
		}

		public static Action onCompletedFinal;

		//获得对话完成action
		[HarmonyPostfix, HarmonyPatch(typeof(ElectiveManager), "OpenUIElective")]
		public static void ExecuteCinematicPatch_multiCourseSelect(ref ElectiveManager __instance, Action _onCompleted)
		{
			//顺便把这个初始化一下，防止下半年选课出错
			selectElectiveInfos = new List<ElectiveInfo>();


			onCompletedFinal = _onCompleted;
			Console.WriteLine("onCompleted:" + onCompletedFinal);
			ElectiveManager electiveManager = __instance;

			UIElective uielective = Game.UI.Open<UIElective>();

			//提交选课后调用的继续对话
			uielective.SetContinuationTalk(delegate
			{
				List<string> electiveIdList = electiveManager.Id.Split('_').ToList();
				createContinuationTalk(electiveIdList, 0);
			});
		}

		public static UpdaterManager.Updater updater = Game.Updater.Frame;
		//这两段是模仿Scheduler写的，主要应该是updater.RunOnce，让对话按顺序依次执行，否则会卡死
		//选了哪几门课，就会有哪些老师出来讲两句
		public static void createContinuationTalk(List<string> electiveIdList, int index)
		{
			TalkAction talkAction = new TalkAction();
			talkAction.talkId = electiveIdList[index].Replace("ec", "t") + "00_000";
			Console.WriteLine("electiveId:" + electiveIdList[index]);
			if (index < electiveIdList.Count - 1)
			{
				talkAction.onCompleted = (Action)Delegate.Combine(talkAction.onCompleted, (Action)delegate { updater.RunOnce(delegate { createContinuationTalk(electiveIdList, index + 1); }); });

			}
			else
			{
				talkAction.onCompleted = (Action)Delegate.Combine(talkAction.onCompleted, onCompletedFinal);
			}
			talkAction.Start();
		}

		//多重选课，显示与隐藏已选图标
		public static List<ElectiveInfo> selectElectiveInfos = new List<ElectiveInfo>();

		[HarmonyPostfix, HarmonyPatch(typeof(UIElective), "OnCourseBtnPressed")]
		public static void OnCourseBtnPressedPatch_multiCourseSelect(ref UIElective __instance)
		{
			if (multiCourseSelect.Value)
			{
				Console.WriteLine("OnCourseBtnPressedPatch_multiCourseSelect start");

				CtrlElective controller = Traverse.Create(__instance).Field("controller").GetValue<CtrlElective>();

				EnumArray<Grade, List<ElectiveInfo>> sort = Traverse.Create(controller).Field("sort").GetValue<EnumArray<Grade, List<ElectiveInfo>>>();
				int gradeIndex = Traverse.Create(controller).Field("gradeIndex").GetValue<int>();
				int courseIndex = Traverse.Create(controller).Field("courseIndex").GetValue<int>();

				ElectiveInfo electiveInfo = sort[gradeIndex][courseIndex];

				InputSelectable ElectiveSelectable = Traverse.Create(__instance).Field("ElectiveSelectable").GetValue<InputSelectable>();
				InputNavigation CurrentSelected = ElectiveSelectable.CurrentSelected;

				if (selectElectiveInfos.Contains(electiveInfo))
				{
					selectElectiveInfos.Remove(electiveInfo);

					hideSelectIcon(CurrentSelected);
				}
				else
				{
					selectElectiveInfos.Add(electiveInfo);

					showSelectIcon(CurrentSelected);
				}

				Console.WriteLine("OnCourseBtnPressedPatch_multiCourseSelect end");
			}
		}

		//切换基础与进阶tab时，显示与隐藏已选图标
		[HarmonyPostfix, HarmonyPatch(typeof(UIElective), "OnTabBtnIsOn")]
		public static void OnTabBtnIsOnPatch_multiCourseSelect(ref UIElective __instance, ref int index)
		{
			if (multiCourseSelect.Value)
			{
				CtrlElective controller = Traverse.Create(__instance).Field("controller").GetValue<CtrlElective>();
				EnumArray<Grade, List<ElectiveInfo>> sort = Traverse.Create(controller).Field("sort").GetValue<EnumArray<Grade, List<ElectiveInfo>>>();
				int gradeIndex = Traverse.Create(controller).Field("gradeIndex").GetValue<int>();

				List<ElectiveInfo> courses = sort[gradeIndex];

				InputSelectable ElectiveSelectable = Traverse.Create(__instance).Field("ElectiveSelectable").GetValue<InputSelectable>();
				List<InputNavigation> InputNavigations = ElectiveSelectable.InputNavigations;

				for (int i = 0; i < courses.Count; i++)
				{
					InputNavigation CurrentSelected = InputNavigations[i];

					if (selectElectiveInfos.Contains(courses[i]))
					{

						showSelectIcon(CurrentSelected);
					}
					else
					{

						hideSelectIcon(CurrentSelected);
					}
				}
			}
		}

		public static void showSelectIcon(InputNavigation CurrentSelected)
		{
			var selectImageTF = CurrentSelected.transform.Find("selectImageGO");

			if (selectImageTF == null)
			{
				GameObject selectImageGO = new GameObject("selectImageGO");
				selectImageGO.transform.position = new Vector3(selectImageGO.transform.position.x + 100, selectImageGO.transform.position.y, selectImageGO.transform.position.z);
				Image selectImage = selectImageGO.AddComponent<Image>();
				selectImage.sprite = Game.Resource.Load<Sprite>("Image/UI/UIAlchemy/alchemy_stove_shine.png");

				selectImageGO.transform.SetParent(CurrentSelected.transform, false);
			}
			else
			{
				selectImageTF.gameObject.SetActive(true);
			}
		}

		public static void hideSelectIcon(InputNavigation CurrentSelected)
		{
			var selectImageTF = CurrentSelected.transform.Find("selectImageGO");

			if (selectImageTF != null)
			{
				selectImageTF.gameObject.SetActive(false);
			}
		}

		//开启确认窗口前，判断是否所有课都满足条件
		[HarmonyPrefix, HarmonyPatch(typeof(CtrlElective), "OpenConfirmWindow")]
		public static bool OpenConfirmWindowPatch_multiCourseSelect(ref CtrlElective __instance)
		{
			if (multiCourseSelect.Value)
			{
				UIElective view = Traverse.Create(__instance).Field("view").GetValue<UIElective>();
				bool IsViewMode = Traverse.Create(__instance).Field("IsViewMode").GetValue<bool>();
				if (IsViewMode)
				{
					return false;
				}

				string electiveNames = "";
				for (int i = 0; i < selectElectiveInfos.Count; i++)
				{
					if (!selectElectiveInfos[i].IsConditionPass)
					{
						StringTable stringTable = Game.Data.Get<StringTable>("SecondaryInterface0102");
						string message = (stringTable != null) ? stringTable.Text : null;


						view.OpenConditionFailWindow(message);
						return false;
					}
					Elective elective = selectElectiveInfos[i].Elective;
					electiveNames += elective.Name + ",";
				}
				StringTable stringTable2 = Game.Data.Get<StringTable>("SecondaryInterface0101");
				string text = (stringTable2 != null) ? stringTable2.Text : null;
				electiveNames = electiveNames.Substring(0, electiveNames.Length - 1);
				text = (text.IsNullOrEmpty() ? string.Empty : string.Format(text, electiveNames));
				view.OpenConfirmWindow(text);

				return false;
			}
			return true;
		}

		//提交选课后，在列表中隐藏已选课程
		[HarmonyPrefix, HarmonyPatch(typeof(CtrlElective), "ConfirmElective")]
		public static bool ConfirmElectivePatch_multiCourseSelect(ref CtrlElective __instance)
		{

			if (multiCourseSelect.Value)
			{
				//先排个序
				ElectiveInfosSort();

				string ids = "";
				for (int i = 0; i < selectElectiveInfos.Count; i++)
				{
					Elective elective = selectElectiveInfos[i].Elective;
					Game.GameData.Elective.ConfirmElective(elective.Id, elective.IsRepeat);
					ids += selectElectiveInfos[i].Elective.Id + "_";
				}
				ids = ids.Substring(0, ids.Length - 1);
				Game.GameData.Elective.Id = ids;//这里写的selectElectiveList存不下来，所以把所有id拼起来存到原id中，到时候拆分后用
				return false;
			}
			return true;
		}

		public static void ElectiveInfosSort()
        {
			//数据量不大，冒泡就完事了
			for(int i = 0;i < selectElectiveInfos.Count; i++)
            {
				for(int j = 0;j < selectElectiveInfos.Count-i-1; j++)
                {
					if(selectElectiveInfos[j].Elective.Id.CompareTo(selectElectiveInfos[j+1].Elective.Id) > 0)
                    {
						ElectiveInfo temp = selectElectiveInfos[j];
						selectElectiveInfos[j] = selectElectiveInfos[j + 1];
						selectElectiveInfos[j + 1] = temp;

					}
                }
            }
        }


		public static List<string> selectElectiveString = new List<string>();
		public static int currentElectiveIndex = 0;
		public static int currentElectiveNumber = 0;
		public static bool isElective = false;
		//上课演出
		[HarmonyPrefix, HarmonyPatch(typeof(ElectiveManager), "ExecuteCinematic")]
		public static bool ExecuteCinematicPatch_multiCourseSelect(ref ElectiveManager __instance)
		{
				currentElectiveIndex = 0;
				isElective = true;

				if (__instance.Id.IsNullOrEmpty())
				{
					Console.WriteLine("當前沒有選課, 無法執行當前選課的演出", Heluo.Logger.LogLevel.MESSAGE, "white", "ExecuteCinematic", "D:\\Work\\PathOfWuxia2018_Update\\Assets\\Scripts\\Table\\Manager\\ElectiveManager.cs", 43);
					return false;
				}

				__instance.Number++;
				ElectiveManager electiveManager = __instance;
				if (__instance.Number > 3)
				{
					__instance.Number = 1;
				}
				currentElectiveNumber = __instance.Number;
				Console.WriteLine("__instance.Id:"+ __instance.Id);
				selectElectiveString = __instance.Id.Split('_').ToList();
			//如果中途关闭功能，则只执行所选的第一个课程
			if (!multiCourseSelect.Value)
			{
				selectElectiveString.RemoveRange(1,selectElectiveString.Count - 1);

			}
			createExecuteCinematic(currentElectiveIndex++, currentElectiveNumber);
				return false;
		}


		public static void createExecuteCinematic(int index,int number)
		{
			RunCinematicAction runCinematicAction = new RunCinematicAction();
			string str = selectElectiveString[index].Replace("ec", "m") + string.Format("{0:00}", number);
			runCinematicAction.cinematicId = str + "_00";
			Console.WriteLine("runCinematic:"+ runCinematicAction.cinematicId);
			runCinematicAction.GetValue();
		}

		//Cinematic不能用runOnce
		//尝试迂回，在上课中，如果返回宿舍进行自由回合，这时候直接执行下一门课的演出
		[HarmonyPrefix, HarmonyPatch(typeof(LoadScenesAction), "GetValue")]
		public static bool GetValuePatch_multiCourseSelect(ref LoadScenesAction __instance)
		{
			if (multiCourseSelect.Value)//开启了多重选课
			{
                if (isElective)//正在上课中
                {
					if(__instance.mapId == "S0202" && __instance.timeStage == TimeStage.Free)//准备返回宿舍进行自由回合
					{
						if(currentElectiveIndex < selectElectiveString.Count)//还有下一门课
						{
							Game.GameData.Character["Player"].HP += 9999;
							Game.GameData.Character["Player"].MP += 9999;
							createExecuteCinematic(currentElectiveIndex++, currentElectiveNumber);
							return false;
						}
                        else
                        {
							isElective = false;
						}
					}
                }
			}
			return true;
		}

	}
}
