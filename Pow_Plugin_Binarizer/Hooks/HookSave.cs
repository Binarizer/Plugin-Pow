using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Platform;
using Heluo.Data;
using Steamworks;
using Heluo.Flow;
using Heluo;
using Heluo.UI;
using Heluo.FSM.Main;
using UnityEngine.UI;
using UnityEngine;

namespace PathOfWuxia
{
	[System.ComponentModel.DisplayName("存档设定")]
	[System.ComponentModel.Description("存档设定")]
	class HookSave : IHook
	{
		private static ConfigEntry<int> saveCount;
		private static ConfigEntry<bool> remindBlankSaveCount;
		private static ConfigEntry<bool> jumpToLatestSave;
		private static ConfigEntry<bool> pagination;
		private static ConfigEntry<int> countPerPage;
		private static ConfigEntry<bool> deleteSaveFile;
		private static int currentPage = 1;
		private static int totalPage = 1;


		public void OnRegister(PluginBinarizer plugin)
		{
			saveCount = plugin.Config.Bind("存档设定", "存档数量", 20, "扩充存档数量");
			remindBlankSaveCount = plugin.Config.Bind("存档设定", "自动存档剩余数量提示", false, "在自动存档剩余空白存档数量不足5个时弹窗提示");
			jumpToLatestSave = plugin.Config.Bind("存档设定", "自动跳转到最新存档位置", false, "在存档数量太多的时候会有点作用");
			pagination = plugin.Config.Bind("存档设定", "存档分页", false, "在存档数量太多的时候会有点作用");
			countPerPage = plugin.Config.Bind("存档设定", "存档分页-每页存档数", 20, "每页多少条存档，存档分页启用后才有用");
			deleteSaveFile = plugin.Config.Bind("存档设定", "删除存档（未完成）", false, "可删除存档");
		}

		//修改存档数量，分页展示
		//覆盖原逻辑
		[HarmonyPrefix, HarmonyPatch(typeof(SteamPlatform), "ListSaveHeaderFile", new Type[] { typeof(GameSaveType) })]
		public static bool ListSaveHeaderFilePatch_changeSaveCount(ref SteamPlatform __instance, ref GameSaveType Type, ref List<PathOfWuxiaSaveHeader> __result)
		{
			Console.WriteLine("ListSaveHeaderFilePatch_changeSaveCount start");
			List<PathOfWuxiaSaveHeader> list = new List<PathOfWuxiaSaveHeader>();
			string format = (Type == GameSaveType.Auto) ? "PathOfWuxia_{0:00}.autosave" : "PathOfWuxia_{0:00}.save";

			int startIndex = 0;
			int endIndex = saveCount.Value;

			if (pagination.Value)
			{
				startIndex = (currentPage - 1) * countPerPage.Value;
				endIndex = Math.Min(currentPage * countPerPage.Value,saveCount.Value);
			}

			Console.WriteLine("startIndex:"+ startIndex);
			Console.WriteLine("endIndex:"+ endIndex);
			for (int i = startIndex; i < endIndex; i++)
			{
				PathOfWuxiaSaveHeader pathOfWuxiaSaveHeader = null;
				string text = string.Format(format, i);
				if (SteamRemoteStorage.FileExists(text))
				{
					__instance.GetSaveFileHeader(text, ref pathOfWuxiaSaveHeader);
				}
				else
				{
					pathOfWuxiaSaveHeader = new PathOfWuxiaSaveHeader();
				}
				if (pathOfWuxiaSaveHeader == null)
				{
					pathOfWuxiaSaveHeader = new PathOfWuxiaSaveHeader();
				}
				list.Add(pathOfWuxiaSaveHeader);
			}

			__result = list;
			Console.WriteLine("list.count:" + list.Count);
			Console.WriteLine("ListSaveHeaderFilePatch_changeSaveCount end");
			return false;
		}

		//提示空白存档剩余数量
		//覆盖原逻辑，基本都是原代码
		//这里是场景中触发的自动存档
		[HarmonyPrefix, HarmonyPatch(typeof(SaveAction), "AutoSave")]
		public static bool AutoSavePatch_remindBlankSaveCount(ref SaveAction __instance)
		{
			Console.WriteLine("AutoSavePatch_remindBlankSaveCount start");
			UIAutoSave uiautoSave = Game.UI.Open<UIAutoSave>();
			bool paginationTemp = pagination.Value;
			pagination.Value = false;
			List<PathOfWuxiaSaveHeader> list = Game.Platform.ListSaveHeaderFile(GameSaveType.Auto);
			pagination.Value = paginationTemp;
			string format = "PathOfWuxia_{0:00}.{1}";
			int num = -1;
			DateTime saveTime = new DateTime(100L);
			for (int i = 0; i < list.Count; i++)
			{
				PathOfWuxiaSaveHeader pathOfWuxiaSaveHeader = list[i];
				if (!pathOfWuxiaSaveHeader.HasData)
				{
					num = i;
					break;
				}
				if (DateTime.Compare(pathOfWuxiaSaveHeader.SaveTime, saveTime) > 0)
				{
					num = ((i + 1 > saveCount.Value - 1) ? 0 : (i + 1));//存档满了不能只覆盖前20个
					saveTime = pathOfWuxiaSaveHeader.SaveTime;
				}
			}
			string filename = string.Format(format, num, "autosave");
			Game.GameData.AutoSaveTotalTime = Game.GameData.Round.TotalTime;
			Game.SaveAsync(filename, null);
			uiautoSave.Show();
			//提示空白存档剩余数量
			if (remindBlankSaveCount.Value && saveCount.Value - num - 1 <= 5)
			{
				string text = "空白存档数量剩余" + (saveCount.Value - num - 1) + "个，请及时扩容，否则将从头开始覆盖存档";
				Game.UI.OpenMessageWindow(text, null, true);
			}
			Console.WriteLine("AutoSavePatch_remindBlankSaveCount end");
			return false;
		}

		//这里是切换日期的自动存档
		[HarmonyPrefix, HarmonyPatch(typeof(InGame), "AutoSave")]
		public static bool AutoSavePatch_remindBlankSaveCount2(ref InGame __instance)
		{
			Console.WriteLine("AutoSavePatch_remindBlankSaveCount2 start");
			bool paginationTemp = pagination.Value;
			pagination.Value = false;
			List<PathOfWuxiaSaveHeader> list = Game.Platform.ListSaveHeaderFile(GameSaveType.Auto);
			pagination.Value = paginationTemp;
			string format = "PathOfWuxia_{0:00}.{1}";
			int num = -1;
			DateTime saveTime = new DateTime(100L);
			for (int i = 0; i < list.Count; i++)
			{
				PathOfWuxiaSaveHeader pathOfWuxiaSaveHeader = list[i];
				if (!pathOfWuxiaSaveHeader.HasData)
				{
					num = i;
					break;
				}
				if (DateTime.Compare(pathOfWuxiaSaveHeader.SaveTime, saveTime) > 0)
				{
					num = ((i + 1 > saveCount.Value - 1) ? 0 : (i + 1));//存档满了不能只覆盖前20个
					saveTime = pathOfWuxiaSaveHeader.SaveTime;
				}
			}
			string filename = string.Format(format, num, "autosave");
			Game.GameData.AutoSaveTotalTime = Game.GameData.Round.TotalTime;
			Game.SaveAsync(filename, null);

			//提示空白存档剩余数量
			if (remindBlankSaveCount.Value && saveCount.Value - num - 1 <= 5)
			{
				string text = "空白存档数量剩余" + (saveCount.Value - num - 1) + "个，请及时扩容，否则将从头开始覆盖存档";
				Game.UI.OpenMessageWindow(text, null, true);
			}
			Console.WriteLine("AutoSavePatch_remindBlankSaveCount2 end");
			return false;
		}

		//切换存档与自动存档时自动跳转最新存档处
		[HarmonyPostfix, HarmonyPatch(typeof(CtrlSaveLoad), "UpdateSaveLoad")]
		public static void UpdateSaveLoadPatch_jumpToLatestSave(ref CtrlSaveLoad __instance)
		{
			Console.WriteLine("UpdateSaveLoadPatch_jumpToLatestSave start");
			//每次进来先置1，防止中途关闭分页功能后仍然停留在后续页面
			currentPage = 1;
			if (jumpToLatestSave.Value)
			{
				//判断是存档还是自动存档
				int categoryIndex = Traverse.Create(__instance).Field("categoryIndex").GetValue<int>();

				//先暂时取消分页，获取所有存档，以找到最新的一条,然后恢复
				bool paginationTemp = pagination.Value;
				pagination.Value = false;
				List<PathOfWuxiaSaveHeader> saves;
				if (categoryIndex == 0)
				{
					saves = Game.Platform.ListSaveHeaderFile(GameSaveType.Manual);
				}
				else
				{
					saves = Game.Platform.ListSaveHeaderFile(GameSaveType.Auto);
				}
				pagination.Value = paginationTemp;

				//获取最新存档的index
				int num = -1;
				DateTime saveTime = new DateTime(100L);

				for (int i = 0; i < saves.Count; i++)
				{
					PathOfWuxiaSaveHeader pathOfWuxiaSaveHeader = saves[i];
					if (!pathOfWuxiaSaveHeader.HasData)
					{
						continue;
					}
					if (DateTime.Compare(pathOfWuxiaSaveHeader.SaveTime, saveTime) > 0)
					{
						num = i;
						saveTime = pathOfWuxiaSaveHeader.SaveTime;
					}
				}
				int currentIndex = num;
				int totalIndex = saveCount.Value;

				//重新给saves和autosaves赋值
				if (pagination.Value)
				{
					currentPage = (num / countPerPage.Value)+1;

					//如果是最后一页，有可能出现存档数不足一页的情况，所以以总数量-前面几页的数量计算
					if(currentPage == totalPage)
					{
						totalIndex = saveCount.Value - (currentPage - 1) * countPerPage.Value;
					}
                    else
					{
						totalIndex = countPerPage.Value;
					}

					currentIndex = num % countPerPage.Value;
				}
				Traverse.Create(__instance).Field("saves").SetValue(Game.Platform.ListSaveHeaderFile(GameSaveType.Manual));
				Traverse.Create(__instance).Field("autosaves").SetValue(Game.Platform.ListSaveHeaderFile(GameSaveType.Auto));

				//更新界面
				UISaveLoad view = Traverse.Create(__instance).Field("view").GetValue<UISaveLoad>();
				view.UpdateSaveLoad(totalIndex, true, true);

				//更新分页栏
				WGTabScroll saveload = Traverse.Create(view).Field("saveload").GetValue<WGTabScroll>();
				GameObject pageBar = saveload.transform.Find("pageBar").gameObject;
				createPageBar(pageBar, __instance);

				//更新滚动条位置
				WGInfiniteScroll loopScroll = Traverse.Create(saveload).Field("loopScroll").GetValue<WGInfiniteScroll>();
				ScrollRect scrollRect = Traverse.Create(loopScroll).Field("scrollRect").GetValue<ScrollRect>();
				scrollRect.verticalScrollbar.value = ((float)(totalIndex - currentIndex - 1)) / (totalIndex-1);//滑动条是反的，不知道为什么
			}
			Console.WriteLine("UpdateSaveLoadPatch_jumpToLatestSave end");
		}

		//存档分页
		[HarmonyPostfix, HarmonyPatch(typeof(UISaveLoad), "Show")]
		public static void showPatch_pagination(ref UISaveLoad __instance)
		{
			Console.WriteLine("showPatch_pagination start");
			//创建主挂载对象，这个对象是不会删除的
			GameObject obj;
			WGTabScroll saveload = Traverse.Create(__instance).Field("saveload").GetValue<WGTabScroll>();
			var trans = saveload.transform.Find("pageBar");
			if (trans == null)
			{
				GameObject pageBar = new GameObject("pageBar");
				Image bg = pageBar.AddComponent<Image>();
				bg.sprite = Game.Resource.Load<Sprite>("Image/UI/UICharacter/Info_HotKey_bg.png");

				HorizontalLayoutGroup layout = pageBar.AddComponent<HorizontalLayoutGroup>();
				layout.childControlHeight = true;
				layout.childControlWidth = true;
				layout.childForceExpandHeight = true;
				layout.childForceExpandWidth = true;

				pageBar.transform.SetParent(saveload.transform, false);
				pageBar.transform.position = new Vector3(pageBar.transform.position.x, pageBar.transform.position.y - Screen.height/2 + 50, 0);
				pageBar.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width, 100);

				//controller:刷新用
				CtrlSaveLoad controller = Traverse.Create(__instance).Field("controller").GetValue<CtrlSaveLoad>();
				obj = createPageBar(pageBar, controller);
			}
            else
            {
				obj = trans.gameObject;
			}
			if (pagination.Value)
			{
				obj.SetActive(true);
			}
            else
            {
				obj.SetActive(false);
			}
			Console.WriteLine("showPatch_pagination end");
		}

		//创建分页栏
		public static GameObject createPageBar(GameObject pageBar, CtrlSaveLoad controller)
		{
			Console.WriteLine("createPageBar start");
			//每次更新都销毁所有页码按钮，重新创建
			for (int i = 0; i < pageBar.transform.childCount; i++)
			{
				UnityEngine.Object.Destroy(pageBar.transform.GetChild(i).gameObject);
			}
			//左侧的<<和<按钮
			GameObject leftBar = new GameObject("leftBar");
			HorizontalLayoutGroup leftLayout = leftBar.AddComponent<HorizontalLayoutGroup>();
			leftLayout.childControlHeight = true;
			leftLayout.childControlWidth = true;
			leftLayout.childForceExpandHeight = true;
			leftLayout.childForceExpandWidth = true;

			GameObject firstPage = createPageButton("firstPage", "<<");
			firstPage.AddComponent<Button>().onClick.AddListener(() => pageClick(pageBar, controller, 1));
			firstPage.transform.SetParent(leftBar.transform, false);

			GameObject previousPage = createPageButton("previousPage", "<");
			previousPage.AddComponent<Button>().onClick.AddListener(() => pageClick(pageBar, controller, currentPage - 1));
			previousPage.transform.SetParent(leftBar.transform, false);

			leftBar.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
			leftBar.transform.SetParent(pageBar.transform, false);

			//中间的数字页码按钮
			GameObject middleBar = new GameObject("leftBar");
			HorizontalLayoutGroup middleLayout = middleBar.AddComponent<HorizontalLayoutGroup>();
			middleLayout.childControlHeight = true;
			middleLayout.childControlWidth = true;
			middleLayout.childForceExpandHeight = true;
			middleLayout.childForceExpandWidth = true;
			middleBar.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width-400, 100);
			middleBar.transform.SetParent(pageBar.transform, false);

			totalPage = (int)Math.Ceiling((float)saveCount.Value / countPerPage.Value);
			//仅展示第1页，最后1页，当前页的±3页，其余省略为…
			for (int i = 1; i <= totalPage; i++)
			{
				if (i != 1 && i < currentPage - 3)
				{
					GameObject pointPage = createPageButton("page", "…");
					pointPage.transform.SetParent(middleBar.transform, false);
					i = currentPage - 4;
				}

				GameObject page = createPageButton(i + "page", i + "");
				page.AddComponent<Button>().onClick.AddListener(() => pageClick(pageBar, controller, int.Parse(page.GetComponent<Text>().text)));
				if (i == currentPage)
				{
					page.GetComponentInChildren<Text>().color = Color.red;//当前页标红
				}
				page.transform.SetParent(middleBar.transform,false);

				if (i != totalPage && i > currentPage + 3)
				{
					GameObject pointPage = createPageButton("page", "…");
					pointPage.transform.SetParent(middleBar.transform, false);
					i = totalPage - 1;
				}
			}

			//右侧的>和>>按钮
			GameObject rightBar = new GameObject("rightBar");
			HorizontalLayoutGroup rightLayout = rightBar.AddComponent<HorizontalLayoutGroup>();
			rightLayout.childControlHeight = true;
			rightLayout.childControlWidth = true;
			rightLayout.childForceExpandHeight = true;
			rightLayout.childForceExpandWidth = true;
			rightBar.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
			rightBar.transform.SetParent(pageBar.transform, false);

			GameObject nextPage = createPageButton("nextPage", ">");
			nextPage.AddComponent<Button>().onClick.AddListener(() => pageClick(pageBar, controller, currentPage + 1));
			nextPage.transform.SetParent(rightBar.transform, false);

			GameObject lastPage = createPageButton("lastPage", ">>");
			lastPage.AddComponent<Button>().onClick.AddListener(() => pageClick(pageBar, controller, totalPage));
			lastPage.transform.SetParent(rightBar.transform, false);

			Console.WriteLine("createPageBar end");
			return pageBar;
		}

		//创建页码按钮
		public static GameObject createPageButton(string name, string value)
		{
			//Console.WriteLine("createPageButton start");
			GameObject go = new GameObject(name);
			Text text = go.AddComponent<Text>();
			text.font = Game.Resource.Load<Font>("Assets/Font/kaiu.ttf");
			text.text = value;
			text.alignment = TextAnchor.MiddleCenter;
			//Console.WriteLine("createPageButton end");
			return go;
		}

		//页码按钮事件
		public static void pageClick(GameObject pageBar, CtrlSaveLoad controller, int page)
		{
			Console.WriteLine("pageClick start");
			//获得当前页码
			if (page < 1)
			{
				page = 1;
			}
			if (page > totalPage)
			{
				page = totalPage;
			}
			Text[] texts = pageBar.GetComponentsInChildren<Text>();
			for (int i = 0; i < texts.Length; i++)
			{
				Text text = texts[i];

				if (text.text.Equals(page + ""))
				{
					currentPage = page;
					break;
				}
			}
			//每次点击都重新创建分页栏
			createPageBar(pageBar, controller);

			//然后更新saves和autosaves
			Traverse.Create(controller).Field("saves").SetValue(Game.Platform.ListSaveHeaderFile(GameSaveType.Manual));
			Traverse.Create(controller).Field("autosaves").SetValue(Game.Platform.ListSaveHeaderFile(GameSaveType.Auto));

			//刷新页面
			UISaveLoad view = Traverse.Create(controller).Field("view").GetValue<UISaveLoad>();
			view.UpdateSaveLoad(countPerPage.Value, true, true);

			Console.WriteLine("pageClick end");
		}

		//存档分页-处理存档左侧的数字
		[HarmonyPostfix, HarmonyPatch(typeof(WGSaveLoadScrollBtn), "UpdateWidget")]
		public static void UpdateWidgetPatch_pagination(ref WGSaveLoadScrollBtn __instance,ref object[] obj)
		{
			//Console.WriteLine("UpdateWidgetPatch_pagination start");
			WGText number = Traverse.Create(__instance).Field("number").GetValue<WGText>();
			SaveLoadScrollInfo saveLoadScrollInfo = obj[0] as SaveLoadScrollInfo;
			int moveNumber = 0;
            if (pagination.Value)
            {
				moveNumber = (currentPage - 1) * countPerPage.Value;

			}
			number.Text = ""+(int.Parse(saveLoadScrollInfo.number) + moveNumber);
			//Console.WriteLine("UpdateWidgetPatch_pagination end");
		}

		//存档分页-修复修复开启分页时所有存读档操作都会处理成第一页档位的问题
		//基本都是原逻辑，注意trueSaveIndex的部分即可
		[HarmonyPrefix, HarmonyPatch(typeof(CtrlSaveLoad), "ConfirmSaveLoad")]
		public static bool ConfirmSaveLoadPatch_pagination(ref CtrlSaveLoad __instance)
		{
			Console.WriteLine("ConfirmSaveLoadPatch_pagination start");
			UISaveLoad view = Traverse.Create(__instance).Field("view").GetValue<UISaveLoad>();

			int saveIndex = Traverse.Create(__instance).Field("saveIndex").GetValue<int>();
			//获取存读档文件时需要转换成真实index
			int trueSaveIndex = saveIndex;
			if (pagination.Value)
			{
				trueSaveIndex += (currentPage - 1) * countPerPage.Value;
			}
			//存档
			if (__instance.isSave)
			{
				string filename = string.Format("PathOfWuxia_{0:00}.{1}", trueSaveIndex, "save");
				view.HideBlur();
				Game.SaveAsync(filename, new Action(__instance.OnSaveFinish));
				view.ShowBlur();
				Console.WriteLine("ConfirmSaveLoadPatch_pagination end");
				return false;
			}
			Game.UI.HideTeamMemeberUI();
			Game.UI.Open<UILoading>();
			int categoryIndex = Traverse.Create(__instance).Field("categoryIndex").GetValue<int>();
			//读档
			if (categoryIndex == 0)
			{
				List<PathOfWuxiaSaveHeader> saves = Traverse.Create(__instance).Field("saves").GetValue<List<PathOfWuxiaSaveHeader>>();
				if (!saves[saveIndex].HasData)
				{
					return false;
				}
				Game.LoadAsync(string.Format("PathOfWuxia_{0:00}.{1}", trueSaveIndex, "save"), null);
				view.Hide();
				Console.WriteLine("ConfirmSaveLoadPatch_pagination end");
				return false;
			}
			//读自动存档
			else
			{
				List<PathOfWuxiaSaveHeader> autosaves = Traverse.Create(__instance).Field("autosaves").GetValue<List<PathOfWuxiaSaveHeader>>();
				if (!autosaves[saveIndex].HasData)
				{
					return false;
				}
				Game.LoadAsync(string.Format("PathOfWuxia_{0:00}.{1}", trueSaveIndex, "autosave"), null);
				view.Hide();
				Console.WriteLine("ConfirmSaveLoadPatch_pagination end");
				return false;
			}
		}

		//存档分页-修复开启分页时首页的继续游戏按钮消失问题
		//检查前关闭分页功能，检查后恢复即可
		[HarmonyPrefix, HarmonyPatch(typeof(CtrlMain), "CheckContinue")]
		public static bool CheckContinuePatch_pagination_pre(ref CtrlMain __instance,ref bool __state)
		{
			Console.WriteLine("CheckContinuePatch_pagination_pre start");
			__state = pagination.Value;
			pagination.Value = false;
			Console.WriteLine("CheckContinuePatch_pagination_pre end");
			return true;
		}
		[HarmonyPostfix, HarmonyPatch(typeof(CtrlMain), "CheckContinue")]
		public static void CheckContinuePatch_pagination_post(ref CtrlMain __instance, ref bool __state)
		{
			Console.WriteLine("CheckContinuePatch_pagination_post start");
			pagination.Value = __state;
			Console.WriteLine("CheckContinuePatch_pagination_post end");
		}
	}
}
