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

        /// <summary>
        /// 爆発 = クレーター形成 + 範囲内の建物/道路/樹木/小物の破壊 + (DLC時)メテオ視覚効果。
        /// クレーター/破壊はゲームの災害ヘルパ(DisasterHelpers, 基本ゲーム側=DLC非依存)を流用し、
        /// 半径類を Scale 倍する。地形破壊を避けるためクレーターには上限を設ける。
        /// </summary>
        private static void PlayExplosion(Vector3 position, float scale)
        {
            var pos2d = new Vector2(position.x, position.z);
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);

            float craterRadius = Mathf.Min(ModConfig.CraterRadiusBase * scale, ModConfig.CraterRadiusMax);
            float craterDepth = Mathf.Min(ModConfig.CraterDepthBase * scale, ModConfig.CraterDepthMax);
            float removeRadius = ModConfig.RemoveRadiusBase * scale;      // 内側=全破壊
            float destMin = ModConfig.DestructionRadiusMinBase * scale;
            float destMax = ModConfig.DestructionRadiusMaxBase * scale;
            float burnMin = destMax;
            float burnMax = ModConfig.BurnRadiusMaxBase * scale;
            float totalRadius = burnMax;

            // クレーター(地形変形)
            DisasterHelpers.MakeCrater(pos2d, craterRadius, craterDepth, raiseEdges: true);
            // 範囲内の建物・道路・樹木・小物を破壊(preRadius=0 の一括処理)
            DisasterHelpers.DestroyStuff(seed, null, position, totalRadius, 0f, removeRadius,
                destMin, destMax, burnMin, burnMax);

            // メテオ視覚効果/効果音は Natural Disasters DLC がある場合のみ流用(無ければ破壊のみ)
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
