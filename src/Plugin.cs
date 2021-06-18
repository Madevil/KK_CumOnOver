using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using MoreAccessoriesKOI;

using KKAPI.Chara;
using KKAPI.Utilities;

namespace CumOnOver
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("marco.kkapi")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "3.0.4")]
	public class CumOnOver : BaseUnityPlugin
	{
#if DEBUG
		public const string Name = "CumOnOver (debug build)";
#else
		public const string Name = "CumOnOver";
#endif
		public const string GUID = "madevil.kk.CumOnOver";
		public const string Version = "2.2.0.0";

		internal static new ManualLogSource Logger;
		internal static MonoBehaviour Instance;
		internal static Harmony HooksInstance;

		internal static ConfigEntry<bool> CfgEnable { get; set; }
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
#if DEBUG
		internal static Dictionary<ChaControl, int> LoadDataTicket = new Dictionary<ChaControl, int>();
		internal static Dictionary<ChaControl, int> CorrectFaceTicket = new Dictionary<ChaControl, int>();
		internal static Type MaterialEditorPluginBase;
		internal static bool DebugBuildME = false;
		internal static bool OptimizeLoadData => DebugBuildME ? Traverse.Create(MaterialEditorPluginBase).Property("OptimizeLoadData").GetValue<ConfigEntry<bool>>().Value : false;
#endif
		internal static Type MaterialEditorCharaController;
		internal static Type ObjectType;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			CfgEnable = Config.Bind("General", "Enable", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 3 }));
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

		internal static object GetController(ChaControl chaCtrl) => chaCtrl?.gameObject?.GetComponent(MaterialEditorCharaController);

		internal static void MaterialEditorSupport()
		{
			BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.materialeditor", out PluginInfo PluginInfo);
			Type MaterialEditorType = PluginInfo.Instance.GetType();
			MaterialEditorCharaController = MaterialEditorType.Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
			Type MaterialEditorHooks = MaterialEditorType.Assembly.GetType("KK_Plugins.MaterialEditor.Hooks");
			ObjectType = MaterialEditorType.Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController+ObjectType");
#if DEBUG
			MaterialEditorPluginBase = MaterialEditorType.Assembly.GetType("MaterialEditorAPI.MaterialEditorPluginBase");
			DebugBuildME = MaterialEditorPluginBase.GetProperty("OptimizeLoadData", AccessTools.all) != null;
#endif
			HooksInstance.Patch(MaterialEditorHooks.GetMethod("ChaControl_UpdateSiru_Postfix", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.Prefix_return_false)));

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
			{
#if DEBUG
				HooksInstance.Patch(MaterialEditorCharaController.GetMethod("LoadData", AccessTools.all), postfix: new HarmonyMethod(typeof(HooksStudio), nameof(HooksStudio.MaterialEditorCharaController_LoadData_Postfix)));
#endif
				HooksInstance.Patch(MaterialEditorCharaController.GetMethod("CorrectFace", AccessTools.all), postfix: new HarmonyMethod(typeof(HooksStudio), nameof(HooksStudio.MaterialEditorCharaController_CorrectFace_Postfix)));
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

							if (mat.GetTexture(ChaShader._Texture2) == null || CfgLiquidOverride.Value)
								mat.SetTexture(ChaShader._Texture2, LiquidT);

							if (mat.GetTexture(ChaShader._Texture3) == null || CfgLiquidOverride.Value)
								mat.SetTexture(ChaShader._Texture3, LiquidN);
						}
					}
				};
			}

			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.SetSiruFlags), new[] { typeof(ChaFileDefine.SiruParts), typeof(byte) })]
			internal static void ChaControl_SetSiruFlags_Postfix(ChaControl __instance)
			{
				__instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance));
			}

			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.UpdateClothesSiru), new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float) })]
			internal static void ChaControl_UpdateClothesSiru_Postfix(ChaControl __instance, int __0)
			{
				if (__0 == 0)
					__instance.StartCoroutine(ChaControl_UpdateClothesSiru_Coroutine(__instance));
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
				if (!CfgEnable.Value)
					return;
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

				for (int i = 0; i < chaCtrl.objClothes.Length; i++)
				{
					if (chaCtrl.objClothes[i] == null) continue;
					ApplyEffect(chaCtrl, 1, i, chaCtrl.objClothes[i]);
				}

				for (int i = 0; i < chaCtrl.objAccessory.Length; i++)
				{
					if (chaCtrl.nowCoordinate.accessory.parts[i].type == 120) continue;
					GameObject go = chaCtrl.objAccessory[i];
					if (go == null) continue;
					ApplyEffect(chaCtrl, 2, i, go);
				}

				MoreAccessories.CharAdditionalData _accessoriesByChar = Traverse.Create(MoreAccessories._self).Field("_accessoriesByChar").GetValue().RefTryGetValue<MoreAccessories.CharAdditionalData>(chaCtrl.chaFile);
				List<ChaFileAccessory.PartsInfo> nowAccessories = _accessoriesByChar?.nowAccessories ?? new List<ChaFileAccessory.PartsInfo>();
				for (int i = 0; i < nowAccessories.Count; i++)
				{
					if (nowAccessories[i].type == 120) continue;
					GameObject go = _accessoriesByChar.objAccessory[i];
					ApplyEffect(chaCtrl, 2, i, go);
				}
			}

			internal static void ApplyEffect(ChaControl chaCtrl, int type, int slot, GameObject go)
			{
				if (go == null) return;

				Renderer[] renders = go.GetComponentsInChildren<Renderer>(true);
				if (renders?.Length == 0) return;

				object pluginCtrl = GetController(chaCtrl);

				for (int i = 0; i < renders.Length; i++)
				{
					Renderer renderer = renders[i];
					if (renderer.materials?.Length == 0) continue;

					foreach (Material mat in renderer.materials)
					{
						if (!mat.HasProperty(ChaShader._liquidface)) continue;

						if (mat.GetTexture("_liquidmask") == null)
						{
							if (useR.IndexOf(go.name) >= 0)
								mat.SetTexture("_liquidmask", liquidmaskR);
							else if (useG.IndexOf(go.name) >= 0)
								mat.SetTexture("_liquidmask", liquidmaskG);
						}

						if (mat.GetTexture(ChaShader._Texture2) == null || CfgLiquidOverride.Value)
						{
							if (Traverse.Create(pluginCtrl).Method("GetMaterialTexture", new object[] { slot, type, mat, "Texture2", go }).GetValue() == null)
								mat.SetTexture(ChaShader._Texture2, LiquidT);
						}

						if (mat.GetTexture(ChaShader._Texture3) == null || CfgLiquidOverride.Value)
						{
							if (Traverse.Create(pluginCtrl).Method("GetMaterialTexture", new object[] { slot, type, mat, "Texture3", go }).GetValue() == null)
								mat.SetTexture(ChaShader._Texture3, LiquidN);
						}

						float s0 = Traverse.Create(pluginCtrl).Method("GetMaterialFloatPropertyValue", new object[] { slot, type, mat, "liquidface", go }).GetValue<float?>() ?? chaCtrl.fileStatus.siruLv[0];
						mat.SetFloat(ChaShader._liquidface, s0);
						float s1 = Traverse.Create(pluginCtrl).Method("GetMaterialFloatPropertyValue", new object[] { slot, type, mat, "liquidftop", go }).GetValue<float?>() ?? chaCtrl.fileStatus.siruLv[1];
						mat.SetFloat(ChaShader._liquidftop, s1);
						float s2 = Traverse.Create(pluginCtrl).Method("GetMaterialFloatPropertyValue", new object[] { slot, type, mat, "liquidfbot", go }).GetValue<float?>() ?? chaCtrl.fileStatus.siruLv[2];
						mat.SetFloat(ChaShader._liquidfbot, s2);
						float s3 = Traverse.Create(pluginCtrl).Method("GetMaterialFloatPropertyValue", new object[] { slot, type, mat, "liquidbtop", go }).GetValue<float?>() ?? chaCtrl.fileStatus.siruLv[3];
						mat.SetFloat(ChaShader._liquidbtop, s3);
						float s4 = Traverse.Create(pluginCtrl).Method("GetMaterialFloatPropertyValue", new object[] { slot, type, mat, "liquidbbot", go }).GetValue<float?>() ?? chaCtrl.fileStatus.siruLv[4];
						mat.SetFloat(ChaShader._liquidbbot, s4);
					}
				}
			}
		}

		internal class HooksStudio
		{
#if DEBUG
			internal static void MaterialEditorCharaController_LoadData_Postfix(CharaCustomFunctionController __instance)
			{
				ChaControl chaCtrl = __instance.ChaControl;
				if (!LoadDataTicket.ContainsKey(chaCtrl))
					LoadDataTicket[chaCtrl] = 0;
				LoadDataTicket[chaCtrl]++;
				Logger.LogWarning($"[MaterialEditorCharaController_LoadData_Postfix]");
			}
#endif
			internal static void MaterialEditorCharaController_CorrectFace_Postfix(CharaCustomFunctionController __instance)
			{
				ChaControl chaCtrl = __instance.ChaControl;
#if DEBUG
				if (!CorrectFaceTicket.ContainsKey(chaCtrl))
					CorrectFaceTicket[chaCtrl] = 0;
				CorrectFaceTicket[chaCtrl]++;

				if (LoadDataTicket[chaCtrl] == CorrectFaceTicket[chaCtrl] || OptimizeLoadData)
#endif
				chaCtrl.StartCoroutine(MaterialEditorCharaController_CorrectFace_Coroutine(chaCtrl));
			}

			internal static IEnumerator MaterialEditorCharaController_CorrectFace_Coroutine(ChaControl chaCtrl)
			{
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
#if DEBUG
				Logger.LogWarning($"[MaterialEditorCharaController_CorrectFace_Coroutine]");
#endif
				chaCtrl.StartCoroutine(Hooks.ChaControl_UpdateClothesSiru_Coroutine(chaCtrl));
			}
		}
	}
}
