using HarmonyLib;
using UnityEngine;

namespace NuclearMeltdown.Game.Patches
{
    /// <summary>
    /// CommonBuildingAI.CollapseBuilding にパッチし、原発の初回崩壊(全焼/災害)を検知する。
    /// Prefixで「崩壊前だったか」を__stateに退避し、Postfixで初回遷移のみ発火。
    /// </summary>
    [HarmonyPatch(typeof(CommonBuildingAI), "CollapseBuilding")]
    public static class CollapseBuildingPatch
    {
        // 実シグネチャ:
        // bool CollapseBuilding(ushort buildingID, ref Building data,
        //     InstanceManager.Group group, bool testOnly, bool demolish, int burnAmount)
        public static void Prefix(ushort buildingID, ref Building data, bool testOnly, out bool __state)
        {
            __state = (data.m_flags & Building.Flags.Collapsed) != Building.Flags.None;
        }

        public static void Postfix(ushort buildingID, ref Building data, bool testOnly, bool __state, bool __result)
        {
            try
            {
                if (testOnly) return;          // 判定のみの呼び出しは無視
                if (__state) return;           // 既に崩壊済み（デモリッシュ等）は無視
                if (!__result) return;         // 実際に状態が変化していない
                if ((data.m_flags & Building.Flags.Collapsed) == Building.Flags.None) return;
                if (!NuclearDetector.IsNuclearPlant(buildingID)) return;

                Vector3 pos = data.m_position;
                MeltdownEffect.Trigger(pos);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("CollapseBuildingPatch error: " + e);
            }
        }
    }
}
