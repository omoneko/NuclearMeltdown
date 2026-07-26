using System;
using HarmonyLib;

namespace NuclearMeltdown.Game.Patches
{
    /// <summary>
    /// CommonBuildingAI.BuildingCollapsed（private）にパッチし、原発の崩壊完了を検知する。
    ///
    /// 破壊経路（火災全焼・洪水・災害）はいずれも Building.Flags.Collapsed を立てるが、
    /// CollapseBuilding を呼ぶのは災害系のみ。火災全焼/洪水はフラグをインライン設定するため
    /// CollapseBuilding をフックすると火災で発火しない（実機テストで判明）。
    /// フラグ設定後の建物は SimulationStep 経由で constructState=0 に達した時点で必ず
    /// private BuildingCollapsed に到達するため、ここを全破壊経路の共通完了点としてフックする。
    /// BuildingCollapsed は Demolishing（プレイヤー撤去）時は本体側で早期 return するため、
    /// 撤去でも Postfix が走る点に注意し、ここでも Demolishing を明示的に除外する。
    /// </summary>
    [HarmonyPatch(typeof(CommonBuildingAI), "BuildingCollapsed")]
    public static class BuildingCollapsedPatch
    {
        // 実シグネチャ（private）:
        // void BuildingCollapsed(ushort buildingID, ref Building buildingData, ref Building.Frame frameData)
        // Harmony はパラメータを名前で注入するため buildingID / buildingData を実名で受ける。
        public static void Postfix(ushort buildingID, ref Building buildingData)
        {
            try
            {
                // プレイヤーによる撤去(Demolishing)は災害ではないので除外
                if ((buildingData.m_flags & Building.Flags.Demolishing) != Building.Flags.None) return;
                if (!NuclearDetector.IsNuclearPlant(buildingID)) return;

                // 「出力に応じた規模」モード用に、崩壊した原発の発電出力を渡す。
                int output = NuclearDetector.GetElectricityProduction(buildingID);
                MeltdownEffect.Trigger(buildingData.m_position, output);
            }
            catch (Exception e)
            {
                ModConfig.LogError("BuildingCollapsedPatch error: " + e);
            }
        }
    }
}
