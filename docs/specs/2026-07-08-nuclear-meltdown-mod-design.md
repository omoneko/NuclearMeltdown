# NuclearMeltdown Mod 設計書

- 日付: 2026-07-08
- 対象: Cities: Skylines（初代 / Unity 5 世代 / .NET Framework 3.5）
- ステータス: 承認済み（実装前の実API検証を残す）

## 1. 概要

原子力発電所（`PowerPlantAI` を持つ原発）が「火災で全焼」または「災害などで崩壊(Collapse)」したとき、その座標に **隕石落下相当の爆発エフェクト** を発生させ、周囲に **広範囲の土壌汚染（疑似・放射能汚染）** を適用するシステム Mod。

汚染は以下のいずれかで消滅する:

1. **時間**: 発生からゲーム内 **50 年** 経過
2. **除染**: 汚染ゾーン内/近傍で **除染施設に見立てた既存建物（デフォルト: 下水処理施設 Water Treatment Plant）** が稼働し、範囲の汚染を徐々に除去

汚染は Cities: Skylines の自然減衰に抗って **Mod 側で維持** され、上記条件を満たすまで消えない。

## 2. 技術要件

- 言語/FW: C# / .NET Framework 3.5
- 参照: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll`, `ColossalManaged.dll`（`C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\`）
- Harmony: NuGet `CitiesHarmony.API`（`HarmonyHelper.DoOnHarmonyReady` 経由で適用）
- ビルド: MSBuild（VS2022）。旧形式 csproj + `<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>`。dotnet SDK は net35 非対応のため不使用。
- 配置先: `C:\Users\omone\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`
- インターフェース: `ICities.IUserMod`（Name/Description をModマネージャに表示）

## 3. アーキテクチャ

```
NuclearMeltdown/
├─ NuclearMeltdown.csproj            # net35, MSBuild旧形式, NuGet:CitiesHarmony.API
├─ Properties/AssemblyInfo.cs
├─ Source/
│  ├─ Mod.cs                         # IUserMod + OnEnabled/OnDisabled で Harmony patch/unpatch
│  ├─ NuclearDetector.cs             # 原発判定（PowerPlantAI + Prefab名）
│  ├─ MeltdownEffect.cs              # 爆発エフェクト生成 + 初回汚染書き込み
│  ├─ ContaminationManager.cs        # 汚染ゾーン台帳（中心/半径/開始ゲーム時刻）※中核
│  ├─ Patches/
│  │   └─ BuildingCollapsePatch.cs   # 破壊検知 Postfix（実APIは逆コンパイルで検証）
│  ├─ Simulation/
│  │   └─ MeltdownThreading.cs       # IThreadingExtension: 毎tick 期限監視 / 汚染維持 / 除染判定
│  └─ Serialization/
│      └─ ContaminationSerializer.cs # ISerializableData でゾーン台帳を保存/復元
├─ docs/specs/2026-07-08-nuclear-meltdown-mod-design.md
└─ README.md
```

## 4. コンポーネント責務

### Mod.cs
- `IUserMod` 実装（`Name`, `Description`）。
- `OnEnabled`/`OnDisabled` で `HarmonyHelper.DoOnHarmonyReady` によりパッチ適用/解除。
- （必要なら）設定 UI（除染対象建物、汚染半径などのオプション）を `OnSettingsUI` で提供。

### NuclearDetector.cs
- `IsNuclearPlant(ushort buildingID)` 純粋判定関数。
- 判定基準: `BuildingManager.instance.m_buildings.m_buffer[id].Info.m_buildingAI is PowerPlantAI` かつ Prefab 名に "Nuclear" を含む等（実名は逆コンパイル/実データで確認）。

### BuildingCollapsePatch.cs
- 破壊トリガーを検知する Harmony `Postfix`。
- フック対象候補（実装前に逆コンパイルで検証）:
  - 崩壊: `CommonBuildingAI.CollapseBuilding`
  - 全焼: 火災による建物消滅処理（`BuildingAI`/`CommonBuildingAI` 側の該当メソッド）
- 検知したら `NuclearDetector.IsNuclearPlant` を確認し、原発なら `MeltdownEffect.Trigger(position)` を呼ぶだけ（薄いパッチ）。

### MeltdownEffect.cs
- `Trigger(Vector3 position)`:
  - (a) 隕石爆発エフェクト（Meteor strike 相当）を座標に生成。エフェクトプレハブ/`EffectInfo` の取得方法は逆コンパイルで確認。
  - (b) 初回の土壌汚染を中心〜約 700m の減衰で書き込み（`NaturalResourceManager` の土壌汚染セル）。
  - (c) `ContaminationManager.RegisterZone(center, radius, startTime)` でゾーン登録。

### ContaminationManager.cs（中核）
- 汚染ゾーンのリストを保持: `{ Vector3 center, float radius, DateTime startGameTime }`。
- `RegisterZone(...)`, `RemoveZone(...)`, `GetZones()`。
- ゾーン→土壌汚染グリッドセルへの書き込み/クリアのユーティリティ（境界チェック込み）。
- immutable 指向: ゾーン更新は新リスト生成で行う（グローバルルールに準拠）。

### MeltdownThreading.cs
- `IThreadingExtension.OnAfterSimulationTick`（頻度は間引き可）で:
  1. 各ゾーンの汚染セルを **再アサート**（自然減衰に抗い濃度維持）。
  2. 開始から **ゲーム内50年** 経過したゾーンを解除・汚染クリア（`SimulationManager.instance.m_currentGameTime` の年差で判定）。
  3. 除染判定: ゾーン内/近傍に稼働中の除染対象建物（下水処理施設）があれば、その範囲の汚染を徐々に減衰させ、全消去でゾーン解除。

### ContaminationSerializer.cs
- `ISerializableData`（`SerializableDataExtensionBase`）でゾーン台帳をセーブデータに保存/復元。
- 一意キー（例: `"NuclearMeltdown.Contamination.v1"`）でバイナリ保存。
- 逆シリアライズ失敗時は空台帳で継続（セーブ破損を招かない）。

## 5. データフロー

```
ゲームが原発を破壊(全焼/崩壊)
  └→ BuildingCollapsePatch.Postfix
        └→ NuclearDetector.IsNuclearPlant? ── no → 何もしない
              └ yes → MeltdownEffect.Trigger(pos)
                        ├→ 爆発エフェクト生成
                        ├→ 初回 土壌汚染書き込み（中心〜700m 減衰）
                        └→ ContaminationManager.RegisterZone

毎シミュレーションtick: MeltdownThreading
  ├→ 汚染セル再アサート（維持）
  ├→ 50年経過ゾーン → クリア&解除
  └→ 除染施設が範囲内 → 徐々に除去 → 全消去で解除

セーブ/ロード: ContaminationSerializer が台帳を永続化
```

## 6. エラーハンドリング / 安全性

- パッチ・tick・シリアライズ処理はすべて try/catch で保護。例外でゲーム本体を巻き込まない（Mod が失敗してもゲームは続行）。
- 同一建物の多重破壊イベントに対する二重発火ガード。
- 座標→グリッドセル変換時の境界チェック。
- 逆シリアライズ失敗時は空台帳フォールバック。
- console 出力は残さず、必要なログは CS の `DebugOutputPanel`/`Debug.Log` に限定。

## 7. 検証方針

- MSBuild で net35 コンパイル成功（参照解決含む）を確認。
- **逆コンパイル（ilspycmd 等）で以下の実シグネチャを照合してから実装**:
  - 破壊フック（`CollapseBuilding` ほか全焼処理）
  - 土壌汚染セル書込（`NaturalResourceManager`）
  - ゲーム内時刻（`SimulationManager.m_currentGameTime`）
  - エフェクト生成（Meteor `EffectInfo` の取得/再生）
  - `ISerializableData` / `IThreadingExtension` の実インターフェース
- 実ゲーム内動作（発生→50年待ち／除染）確認は **ユーザーに依頼**（Claude からゲーム起動テスト不可）。

## 8. 未確定事項（実装計画で解消）

- 破壊検知メソッドの正確なシグネチャと引数（崩壊 vs 全焼の分岐）。
- 隕石爆発エフェクトの取得手段（`EffectCollection`/`DisasterManager` 経由か、`EffectInfo` 参照か）。
- 土壌汚染セルの解像度・座標変換・書込 API の正確な形。
- 除染施設（下水処理施設）の判定方法（AI 種別 or Prefab 名）と除染速度パラメータ。
- tick 間引き頻度と汚染維持のコスト（大量ゾーン時のパフォーマンス）。

## 9. スコープ外（YAGNI）

- 新規建物アセット（除染施設）の作成（既存建物流用のため不要）。
- 放射能の独自リソースシステム（既存汚染を流用）。
- マルチ言語 UI（初期は日本語/英語の最小表記）。
