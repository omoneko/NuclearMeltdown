using ColossalFramework;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>The explosion effect and the contamination zone raised when a plant is destroyed.</summary>
    public static class MeltdownEffect
    {
        /// <summary>
        /// Raises the effects of a destroyed nuclear plant. electricityProduction feeds the
        /// "scale from output" mode (0 means unknown and falls back to the baseline). How the
        /// scale is chosen, and whether there is an explosion or contamination at all, follows
        /// the options.
        /// </summary>
        public static void Trigger(Vector3 position, int electricityProduction)
        {
            // 0-99 drawn from the game's deterministic RNG, so saves stay reproducible
            // (used by the Random mode).
            int roll = (int)SimulationManager.instance.m_randomizer.Int32(100u);

            MeltdownScaleMode mode = ModSettings.ScaleMode;
            float scale;
            switch (mode)
            {
                case MeltdownScaleMode.ByOutput:
                    scale = OutputScale.FromOutput(electricityProduction, ModConfig.ReferenceOutput,
                        ModConfig.OutputScaleMin, ModConfig.OutputScaleMax);
                    break;
                case MeltdownScaleMode.Fixed:
                    scale = ModSettings.FixedScale;
                    break;
                default:
                    scale = 0f; // Random gets its scale from the probability table
                    break;
            }

            MeltdownOutcome outcome = MeltdownOutcomeSelector.Select(
                mode, roll, scale, ModSettings.ExplosionEnabled, ModSettings.ContaminationEnabled);

            // Register the contamination first, so a failure fetching the visual effect cannot
            // cost us the zone.
            if (outcome.Contaminate)
            {
                long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                // The contamination radius is uncapped as well - proportional to the scale.
                float radius = ModConfig.DefaultRadiusMeters * outcome.ContaminationScale;
                ContaminationManager.AddZone(new ContaminationZone(position.x, position.z, radius, startTicks));
            }

            // The explosion gets its own try/catch so a visual failure cannot affect the
            // contamination.
            if (outcome.Explode)
            {
                try { PlayExplosion(position, outcome.ExplosionScale); }
                catch (System.Exception e) { ModConfig.LogError("explosion error: " + e); }
            }

            // output is in internal units; the MW figure in the UI is roughly output/62.5, so
            // both are logged to make this easy to check.
            ModConfig.Log("Meltdown mode=" + mode
                + " output=" + electricityProduction + "(~" + (electricityProduction / 62.5f).ToString("0") + "MW)"
                + " roll=" + roll
                + " explode=" + outcome.Explode + "(x" + outcome.ExplosionScale + ")"
                + " contaminate=" + outcome.Contaminate + "(x" + outcome.ContaminationScale + ")");
        }

        /// <summary>
        /// The explosion: a crater, destruction of the buildings, roads, trees and props in
        /// range, and - with the DLC - the meteor visual. The crater and the destruction reuse
        /// the game's own DisasterHelpers (part of the base game, no DLC needed) with every
        /// radius multiplied by the scale.
        /// </summary>
        private static void PlayExplosion(Vector3 position, float scale)
        {
            var pos2d = new Vector2(position.x, position.z);
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);

            // The crater is deliberately uncapped - it grows with the output without limit.
            float craterRadius = ModConfig.CraterRadiusBase * scale;
            float craterDepth = ModConfig.CraterDepthBase * scale;
            // The destruction and fire radii are rounded down to EffectRadiusMax, which already
            // covers the whole map. Beyond that the result is the same (the map is gone) while
            // the DisasterHelpers scan just gets needlessly expensive.
            float cap = ModConfig.EffectRadiusMax;
            float removeRadius = Mathf.Min(ModConfig.RemoveRadiusBase * scale, cap);   // inner, everything destroyed
            float destMin = Mathf.Min(ModConfig.DestructionRadiusMinBase * scale, cap);
            float destMax = Mathf.Min(ModConfig.DestructionRadiusMaxBase * scale, cap);
            float burnMin = destMax;
            float burnMax = Mathf.Min(ModConfig.BurnRadiusMaxBase * scale, cap);
            float totalRadius = burnMax;

            // The crater (terrain deformation).
            DisasterHelpers.MakeCrater(pos2d, craterRadius, craterDepth, raiseEdges: true);
            // Destroy the buildings, roads, trees and props in range.
            // preRadius is the outer radius the shock wave reached; it drives both the guard
            // inside DestroyBuildings (num7 < preRadius) and the area scanned. Pass anything
            // other than the outer radius (totalRadius) here and nothing gets destroyed at all.
            // Inside removeRadius (centred on the plant) demolish=true removes the foundations too.
            DisasterHelpers.DestroyStuff(seed, null, position, totalRadius, totalRadius, removeRadius,
                destMin, destMax, burnMin, burnMax);

            // The meteor visual and sound are borrowed only if the Natural Disasters DLC is
            // present; without it the destruction still happens, just without the visual.
            EffectInfo effect = ResolveExplosionEffect();
            if (effect != null)
            {
                var spawnArea = new EffectInfo.SpawnArea(position, Vector3.up, 0f);
                Singleton<EffectManager>.instance.DispatchEffect(
                    effect, default(InstanceID), spawnArea, Vector3.zero, 0f, scale,
                    Singleton<VehicleManager>.instance.m_audioGroup);
            }
            else
            {
                ModConfig.Log("meteor visual effect unavailable (no DLC) — crater/destruction still applied");
            }
        }

        private static EffectInfo ResolveExplosionEffect()
        {
            // The meteor disaster is not implemented as a DisasterInfo/DisasterAI at all: it is
            // a MeteorAI (derived from VehicleAI) carried by a VehicleInfo.
            // (DisasterAI : PrefabAI and MeteorAI : VehicleAI : PrefabAI are unrelated sibling
            //  classes, so info.m_disasterAI as MeteorAI fails to compile with CS0039 -
            //  confirmed by decompiling.)
            int count = PrefabCollection<VehicleInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
                if (info == null) continue;
                MeteorAI ai = info.m_vehicleAI as MeteorAI;
                if (ai != null && ai.m_impactEffect != null) return ai.m_impactEffect;
            }
            return null;
        }
    }
}
