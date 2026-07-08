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
            // ゲームの決定論RNG(セーブ再現性を保つ)で 0-99 を抽選し、結果を確率テーブルで決定
            int roll = (int)SimulationManager.instance.m_randomizer.Int32(100u);
            MeltdownOutcome outcome = MeltdownOutcomeTable.FromRoll(roll);

            // 汚染を先に確定（エフェクト取得失敗で汚染登録を失わないため）
            if (outcome.Contaminate)
            {
                long startTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                float radius = ModConfig.DefaultRadiusMeters * outcome.ContaminationScale;
                ContaminationManager.AddZone(new ContaminationZone(position.x, position.z, radius, startTicks));
            }

            // 爆発は独立して try/catch（視覚効果の失敗が汚染に影響しないように）
            if (outcome.Explode)
            {
                try { PlayExplosion(position, outcome.ExplosionScale); }
                catch (System.Exception e) { ModConfig.LogError("explosion error: " + e); }
            }

            ModConfig.Log("Meltdown roll=" + roll
                + " explode=" + outcome.Explode + "(x" + outcome.ExplosionScale + ")"
                + " contaminate=" + outcome.Contaminate + "(x" + outcome.ContaminationScale + ")");
        }

        private static void PlayExplosion(Vector3 position, float scale)
        {
            EffectInfo effect = ResolveExplosionEffect();
            if (effect == null)
            {
                ModConfig.Log("explosion effect unavailable (Natural Disasters DLC not present?) — skipping visual");
                return;
            }
            var spawnArea = new EffectInfo.SpawnArea(position, Vector3.up, 0f);
            var instanceID = default(InstanceID);
            // magnitude に Scale を渡して爆発規模を反映
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, instanceID, spawnArea, Vector3.zero, 0f, scale,
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
