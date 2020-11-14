using System.Collections;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CumOnOver
{
	[BepInPlugin(GUID, Name, Version)]
	public class CumOnOver : BaseUnityPlugin
	{
		public const string Name = "CumOnOver";
		public const string GUID = "madevil.kk.CumOnOver";
		public const string Version = "1.1.0.0";

		internal static new ManualLogSource Logger;
		internal static MonoBehaviour Instance;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			Harmony.CreateAndPatchAll(typeof(Hooks));
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

				for (int i = 0; i < __instance.objAccessory.Length; i++)
				{
					if ((__instance.objAccessory[i] != null) && (__instance.objAccessory[i].GetComponent<ChaAccessoryComponent>() != null))
					{
						ChaAccessoryComponent chaAccessory = __instance.objAccessory[i].GetComponent<ChaAccessoryComponent>();
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
	}
}
