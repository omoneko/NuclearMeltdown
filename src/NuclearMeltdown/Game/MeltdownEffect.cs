using ColossalFramework;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>崩壊時の爆発エフェクトと汚染ゾーン発生。</summary>
    public static class MeltdownEffect
    {
        public static void Trigger(Vector3 position)
        {
            PlayExplosion(position);

            long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
            var zone = new ContaminationZone(position.x, position.z, ModConfig.DefaultRadiusMeters, startTicks);
            ContaminationManager.AddZone(zone);
            ModConfig.Log("Meltdown triggered at " + position + " radius " + ModConfig.DefaultRadiusMeters);
        }

        private static void PlayExplosion(Vector3 position)
        {
            EffectInfo effect = ResolveExplosionEffect();
            if (effect == null)
            {
                ModConfig.Log("explosion effect unavailable (Natural Disasters DLC not present?) — skipping visual");
                return;
            }
            var spawnArea = new EffectInfo.SpawnArea(position, Vector3.up, 0f);
            var instanceID = default(InstanceID);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, instanceID, spawnArea, Vector3.zero, 0f, 1f,
                Singleton<VehicleManager>.instance.m_audioGroup);
        }

        private static EffectInfo ResolveExplosionEffect()
        {
            // メテオ(隕石)災害の実体は DisasterInfo/DisasterAI ではなく、
            // VehicleInfo に載る MeteorAI (VehicleAI 派生) として実装されている。
            // (DisasterAI : PrefabAI と MeteorAI : VehicleAI : PrefabAI は無関係な兄弟クラスのため
            //  info.m_disasterAI as MeteorAI はコンパイルエラー CS0039 になる — 要デコンパイル検証)
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
