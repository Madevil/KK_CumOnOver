using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Utilities;

namespace CumOnOver
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("marco.kkapi")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "3.0.3")]
	public class CumOnOver : BaseUnityPlugin
	{
		public const string Name = "CumOnOver";
		public const string GUID = "madevil.kk.CumOnOver";
		public const string Version = "2.1.0.0";

		internal static new ManualLogSource Logger;
		internal static MonoBehaviour Instance;
		internal static Harmony HooksInstance;

		internal static ConfigEntry<bool> CfgLiquidOverride { get; set; }
		internal static ConfigEntry<string> CfgLiquidTPath { get; set; }
		internal static ConfigEntry<string> CfgLiquidNPath { get; set; }
		internal static string LiquidTPath;
		internal static string LiquidNPath;
		internal static Texture2D LiquidT;
		internal static Texture2D LiquidN;
		internal static Texture2D liquidmaskR;
		internal static Texture2D liquidmaskG;
		internal static List<string> useR = new List<string>() { "ct_clothesTop", "ct_top_parts_A", "ct_top_parts_B", "ct_top_parts_C", "ct_bra", "ct_gloves" };
		internal static List<string> useG = new List<string>() { "ct_clothesBot", "ct_shorts", "ct_panst", "ct_socks", "ct_shoes_inner", "ct_shoes_outer" };

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			CfgLiquidOverride = Config.Bind("General", "Force Custom Liquid", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
			CfgLiquidTPath = Config.Bind("General", "LiquidT Path", "", new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
			CfgLiquidNPath = Config.Bind("General", "LiquidN Path", "", new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));

			if (!LoadLiquidTexture(CfgLiquidTPath.Value, ref LiquidT))
				CfgLiquidTPath.Value = "";
			LiquidTPath = CfgLiquidTPath.Value;
			if (!LoadLiquidTexture(CfgLiquidNPath.Value, ref LiquidN))
				CfgLiquidNPath.Value = "";
			LiquidNPath = CfgLiquidNPath.Value;

			liquidmaskR = LoadTexture(ResourceUtils.GetEmbeddedResource("red.png"));
			liquidmaskG = LoadTexture(ResourceUtils.GetEmbeddedResource("green.png"));

			HooksInstance = Harmony.CreateAndPatchAll(typeof(Hooks));
			MaterialEditorSupport();
		}

		internal static bool LoadLiquidTexture(string path, ref Texture2D tex)
		{
			tex = null;
			if (path.IsNullOrEmpty() || !File.Exists(path))
				return false;

			tex = new Texture2D(2, 2);
			if (!tex.LoadImage(File.ReadAllBytes(path)))
			{
				tex = null;
				return false;
			}

			return true;
		}

		internal static Texture2D LoadTexture(byte[] texData)
		{
			Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
			tex.LoadImage(texData);
			return tex;
		}

		internal static void MaterialEditorSupport()
		{
			BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.materialeditor", out PluginInfo PluginInfo);

			Type MaterialEditorCharaController = PluginInfo.Instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
			Type MaterialEditorHooks = PluginInfo.Instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.Hooks");

			HooksInstance.Patch(MaterialEditorHooks.GetMethod("ChaControl_UpdateSiru_Postfix", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.Prefix_return_false)));

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
			{
				HooksInstance.Patch(MaterialEditorCharaController.GetMethod("CorrectFace", AccessTools.all), postfix: new HarmonyMethod(typeof(HooksStudio), nameof(HooksStudio.MaterialEditorCharaController_LoadData_Postfix)));
			}
		}

		internal class Hooks
		{
			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "LoadCharaFbxDataAsync")]
			internal static void ChaControl_LoadCharaFbxDataAsync_Prefix(ref Action<GameObject> actObj)
			{
				Action<GameObject> oldAct = actObj;
				actObj = delegate (GameObject o)
				{
					oldAct(o);
					if (o == null) return;

					Renderer[] renderers = o.GetComponentsInChildren<Renderer>();
					foreach (Renderer r in renderers)
					{
						foreach (Material mat in r.materials)
						{
							if (!mat.HasProperty(ChaShader._liquidface)) continue;

							if (mat.GetTexture("_liquidmask") == null)
							{
								if (useR.IndexOf(o.name) >= 0)
									mat.SetTexture("_liquidmask", liquidmaskR);
								else if (useG.IndexOf(o.name) >= 0)
									mat.SetTexture("_liquidmask", liquidmaskG);
							}
						}
					}
				};
			}

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

			internal static bool Prefix_return_false() => false;

			internal static void ChaControl_UpdateClothesSiru(ChaControl chaCtrl)
			{
				if (chaCtrl == null || chaCtrl.gameObject == null)
					return;

				if (LiquidT == null || LiquidTPath != CfgLiquidTPath.Value)
				{
					if (!LoadLiquidTexture(CfgLiquidTPath.Value, ref LiquidT))
						CfgLiquidTPath.Value = "";
					LiquidTPath = CfgLiquidTPath.Value;
				}
				if (LiquidN == null || LiquidNPath != CfgLiquidNPath.Value)
				{
					if (!LoadLiquidTexture(CfgLiquidNPath.Value, ref LiquidN))
						CfgLiquidNPath.Value = "";
					LiquidNPath = CfgLiquidNPath.Value;
				}

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
				if (renderer == null || renderer.materials?.Length == 0) return;

				foreach (Material mat in renderer.materials)
				{
					if (!mat.HasProperty(ChaShader._liquidface)) continue;

					if (mat.GetTexture(ChaShader._Texture2) == null || CfgLiquidOverride.Value)
						mat.SetTexture(ChaShader._Texture2, LiquidT);

					if (mat.GetTexture(ChaShader._Texture3) == null || CfgLiquidOverride.Value)
						mat.SetTexture(ChaShader._Texture3, LiquidN);

					mat.SetFloat(ChaShader._liquidface, chaCtrl.fileStatus.siruLv[0]);
					mat.SetFloat(ChaShader._liquidftop, chaCtrl.fileStatus.siruLv[1]);
					mat.SetFloat(ChaShader._liquidfbot, chaCtrl.fileStatus.siruLv[2]);
					mat.SetFloat(ChaShader._liquidbtop, chaCtrl.fileStatus.siruLv[3]);
					mat.SetFloat(ChaShader._liquidbbot, chaCtrl.fileStatus.siruLv[4]);
				}
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
