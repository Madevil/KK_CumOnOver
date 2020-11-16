﻿using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using KK_Plugins.MaterialEditor;

namespace CumOnOver
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency(KKAPI.KoikatuAPI.GUID)]
	[BepInDependency(MaterialEditorPlugin.GUID)]
	public class CumOnOver : BaseUnityPlugin
	{
		public const string Name = "CumOnOver";
		public const string GUID = "madevil.kk.CumOnOver";
		public const string Version = "1.2.1.0";

		internal static new ManualLogSource Logger;
		internal static MonoBehaviour Instance;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			Harmony.CreateAndPatchAll(typeof(Hooks));

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
				Harmony.CreateAndPatchAll(typeof(HooksStudio));
		}

		internal class Hooks
		{
			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "SetSiruFlags")]
			internal static void ChaControl_SetSiruFlags_Postfix(ChaControl __instance, ChaFileDefine.SiruParts parts, byte lv)
			{
				if (parts == ChaFileDefine.SiruParts.SiruKao)
					Instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance, true));
			}

			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "UpdateClothesSiru")]
			internal static void ChaControl_UpdateClothesSiru_Postfix(ChaControl __instance, int kind, float frontTop, float frontBot, float downTop, float downBot)
			{
				if (kind == 0)
					Instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance));
			}

			internal static IEnumerator ChaControl_UpdateClothesSiru_Coroutine(ChaControl __instance, bool skip = false)
			{
				// trick from MakerOptimizations, seems only works with 2 lines
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				float face = __instance.fileStatus.siruLv[(int) ChaFileDefine.SiruParts.SiruKao];
				float frontTop = __instance.fileStatus.siruLv[(int) ChaFileDefine.SiruParts.SiruFrontUp];
				float frontBot = __instance.fileStatus.siruLv[(int) ChaFileDefine.SiruParts.SiruFrontDown];
				float downTop = __instance.fileStatus.siruLv[(int) ChaFileDefine.SiruParts.SiruBackUp];
				float downBot = __instance.fileStatus.siruLv[(int) ChaFileDefine.SiruParts.SiruBackDown];

				if (!skip)
				{
					__instance.UpdateClothesSiru(4, frontTop, frontBot, downTop, downBot);
					__instance.UpdateClothesSiru(6, frontTop, frontBot, downTop, downBot);
					__instance.UpdateClothesSiru(7, frontTop, frontBot, downTop, downBot);
					__instance.UpdateClothesSiru(8, frontTop, frontBot, downTop, downBot);
				}

				ChaAccessoryComponent[] chaAccessories = __instance.GetComponentsInChildren<ChaAccessoryComponent>();
				for (int i = 0; i < chaAccessories.Length; i++)
				{
					ChaAccessoryComponent chaAccessory = chaAccessories[i];
					if (chaAccessory.gameObject.name.StartsWith("ca_slot"))
					{
						for (int j = 0; j < chaAccessory.rendNormal.Length; j++)
						{
							Renderer renderer = chaAccessory.rendNormal[j];
							if ((renderer != null) && (renderer.material != null))
							{
								renderer.material.SetFloat(ChaShader._liquidface, face);
								renderer.material.SetFloat(ChaShader._liquidftop, frontTop);
								renderer.material.SetFloat(ChaShader._liquidfbot, frontBot);
								renderer.material.SetFloat(ChaShader._liquidbtop, downTop);
								renderer.material.SetFloat(ChaShader._liquidbbot, downBot);
							}
						}
						for (int j = 0; j < chaAccessory.rendAlpha.Length; j++)
						{
							Renderer renderer = chaAccessory.rendAlpha[j];
							if ((renderer != null) && (renderer.material != null))
							{
								renderer.material.SetFloat(ChaShader._liquidface, face);
								renderer.material.SetFloat(ChaShader._liquidftop, frontTop);
								renderer.material.SetFloat(ChaShader._liquidfbot, frontBot);
								renderer.material.SetFloat(ChaShader._liquidbtop, downTop);
								renderer.material.SetFloat(ChaShader._liquidbbot, downBot);
							}
						}
					}
				}
			}
		}

		internal class HooksStudio
		{
			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(MaterialEditorCharaController), "LoadData")]
			internal static void MaterialEditorCharaController_LoadData_Postfix(MaterialEditorCharaController __instance, bool clothes, bool accessories, bool hair)
			{
				Instance.StartCoroutine(MaterialEditorCharaController_LoadData_Coroutine(__instance.ChaControl));
			}

			internal static IEnumerator MaterialEditorCharaController_LoadData_Coroutine(ChaControl chaCtrl)
			{
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				Instance.StartCoroutine(Hooks.ChaControl_UpdateClothesSiru_Coroutine(chaCtrl, true));
			}
		}
	}
}
