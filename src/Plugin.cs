﻿using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;

namespace CumOnOver
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("marco.kkapi")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor")]
	public class CumOnOver : BaseUnityPlugin
	{
		public const string Name = "CumOnOver";
		public const string GUID = "madevil.kk.CumOnOver";
		public const string Version = "1.5.3.0";

		internal static new ManualLogSource Logger;
		internal static MonoBehaviour Instance;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			Harmony HooksInstance = Harmony.CreateAndPatchAll(typeof(Hooks));

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.materialeditor", out PluginInfo PluginInfo);
				Type MaterialEditorCharaController = PluginInfo.Instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
				//HooksInstance.Patch(MaterialEditorCharaController.GetMethod("LoadData", AccessTools.all, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null), postfix: new HarmonyMethod(typeof(HooksStudio), nameof(HooksStudio.MaterialEditorCharaController_LoadData_Postfix)));
				HooksInstance.Patch(MaterialEditorCharaController.GetMethod("CorrectTongue", AccessTools.all), postfix: new HarmonyMethod(typeof(HooksStudio), nameof(HooksStudio.MaterialEditorCharaController_LoadData_Postfix)));
			}
		}

		internal class Hooks
		{
			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetSiruFlags), new[] { typeof(ChaFileDefine.SiruParts), typeof(byte) })]
			internal static void ChaControl_SetSiruFlags_Postfix(ChaControl __instance)
			{
				Instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance));
			}

			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.UpdateClothesSiru), new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float) })]
			internal static void ChaControl_UpdateClothesSiru_Postfix(ChaControl __instance, int __0)
			{
				if (__0 == 0)
					Instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance));
			}

			internal static IEnumerator ChaControl_UpdateClothesSiru_Coroutine(ChaControl chaCtrl)
			{
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				ChaControl_UpdateClothesSiru(chaCtrl);
			}

			internal static void ChaControl_UpdateClothesSiru(ChaControl chaCtrl)
			{
				if (chaCtrl == null || chaCtrl.gameObject == null)
					return;

				ChaClothesComponent[] chaClothes = chaCtrl.GetComponentsInChildren<ChaClothesComponent>(true);
				if (chaClothes != null)
				{
					foreach (ChaClothesComponent part in chaClothes)
					{
						Renderer[] renders = part.GetComponentsInChildren<Renderer>(true);
						for (int j = 0; j < renders.Length; j++)
							ApplyEffect(chaCtrl, renders[j]);
					}
				}

				ChaAccessoryComponent[] chaAccessories = chaCtrl.GetComponentsInChildren<ChaAccessoryComponent>(true);
				if (chaAccessories != null)
				{
					foreach (ChaAccessoryComponent part in chaAccessories)
					{
						if (part.gameObject.name.StartsWith("ca_slot"))
						{
							Renderer[] renders = part.GetComponentsInChildren<Renderer>(true);
							for (int j = 0; j < renders.Length; j++)
								ApplyEffect(chaCtrl, renders[j]);
						}
					}
				}
			}

			internal static void ApplyEffect(ChaControl chaCtrl, Renderer renderer)
			{
				if ((renderer == null) || (renderer.material == null)) return;

				renderer.material.SetFloat(ChaShader._liquidface, chaCtrl.fileStatus.siruLv[0]);
				renderer.material.SetFloat(ChaShader._liquidftop, chaCtrl.fileStatus.siruLv[1]);
				renderer.material.SetFloat(ChaShader._liquidfbot, chaCtrl.fileStatus.siruLv[2]);
				renderer.material.SetFloat(ChaShader._liquidbtop, chaCtrl.fileStatus.siruLv[3]);
				renderer.material.SetFloat(ChaShader._liquidbbot, chaCtrl.fileStatus.siruLv[4]);
			}
		}

		internal class HooksStudio
		{
			internal static void MaterialEditorCharaController_LoadData_Postfix(CharaCustomFunctionController __instance)
			{
				ChaControl chaCtrl = __instance.ChaControl;
				Instance.StartCoroutine(MaterialEditorCharaController_LoadData_Coroutine(chaCtrl));
			}

			internal static IEnumerator MaterialEditorCharaController_LoadData_Coroutine(ChaControl chaCtrl)
			{
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				Instance.StartCoroutine(Hooks.ChaControl_UpdateClothesSiru_Coroutine(chaCtrl));
			}
		}
	}
}