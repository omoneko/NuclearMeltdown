using System;
using HarmonyLib;

namespace NuclearMeltdown.Game.Patches
{
    /// <summary>
    /// Patches the private CommonBuildingAI.BuildingCollapsed to notice that a nuclear plant
    /// has finished collapsing.
    ///
    /// Every route to destruction - burning down, flooding, a disaster - sets
    /// Building.Flags.Collapsed, but only the disasters call CollapseBuilding: fire and flood
    /// set the flag inline. Hooking CollapseBuilding therefore never fires for a fire, which
    /// in-game testing confirmed. Once the flag is set, a building always reaches the private
    /// BuildingCollapsed as soon as SimulationStep drives constructState to 0, which makes it
    /// the one point every route passes through.
    /// Note that BuildingCollapsed returns early on its own when the building is Demolishing
    /// (the player removing it), so the Postfix still runs in that case - hence the explicit
    /// Demolishing check below.
    /// </summary>
    [HarmonyPatch(typeof(CommonBuildingAI), "BuildingCollapsed")]
    public static class BuildingCollapsedPatch
    {
        // The real (private) signature is:
        // void BuildingCollapsed(ushort buildingID, ref Building buildingData, ref Building.Frame frameData)
        // Harmony injects parameters by name, so buildingID and buildingData must keep theirs.
        public static void Postfix(ushort buildingID, ref Building buildingData)
        {
            try
            {
                // The player bulldozing it (Demolishing) is not a disaster, so skip it.
                if ((buildingData.m_flags & Building.Flags.Demolishing) != Building.Flags.None) return;
                if (!NuclearDetector.IsNuclearPlant(buildingID)) return;

                // Pass the collapsed plant's output along for the "scale from output" mode.
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
