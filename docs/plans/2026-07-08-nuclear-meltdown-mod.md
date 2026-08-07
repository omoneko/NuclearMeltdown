# NuclearMeltdown Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cities: Skylines（初代）で原子力発電所が全焼/崩壊した際に、隕石爆発エフェクトと広範囲の疑似放射能汚染（土壌汚染）を発生させ、汚染はゲーム内50年経過または除染施設稼働まで維持されるシステムModを作る。

**Architecture:** Unity/ゲーム型に依存しない純粋ロジック（座標変換・半径セル列挙・50年判定・ゾーンのバイナリ直列化）を `Core/` に分離してxUnitで実TDD。ゲーム統合層（Harmonyパッチ、エフェクト、汚染書込、拡張点）は薄く保ち、`Core` を呼び出す。汚染はCSの自然減衰があるため `ThreadingExtension` の毎tickで再アサートし、期限/除染で解除する。

**Tech Stack:** C# / .NET Framework 3.5（Mod本体, MSBuildでビルド）, CitiesHarmony.API (Harmony 2.0), ICities/Assembly-CSharp/UnityEngine/ColossalManaged。テストは .NET 8 + xUnit（Coreソースをリンク参照）。

## Global Constraints

- 対象FW（Mod本体）: **.NET Framework 3.5**。`ValueTuple`/名前付きタプル・`Span`・LINQ以降の新API等の net35 非対応機能を `Core` で使用しない（`Core` は net35 と net8 の両方でコンパイルされる）。
- ゲームDLL参照元: `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\`（`ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll`, `ColossalManaged.dll`）。参照は `Private=False`（Copy Local=false）。
- Harmony: NuGet `CitiesHarmony.API`。パッチは `HarmonyHelper.DoOnHarmonyReady` / `IsHarmonyInstalled` 経由で適用/解除。ハーモニーID: `"com.omone.nuclearmeltdown"`。
- デプロイ先: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`。
- ログは `UnityEngine.Debug.Log` に接頭辞 `"[NuclearMeltdown] "` を付けてのみ出力（`Console.WriteLine`/`print` 禁止）。
- 全パッチ・tick・直列化処理は try/catch で保護し、例外をゲーム本体へ伝播させない。
- 汚染グリッド定数（Assembly-CSharp 実測値）: `CELL_SIZE = 33.75f`, `RESOLUTION = 512`, セル変換 `cell = Clamp((int)(world / 33.75f + 256f), 0, 511)`, `index = cellZ * 512 + cellX`, `m_pollution` は byte(0–255)。
- 除染施設デフォルト判定: Prefab名に `"Water Treatment"` を含む建物（設定定数 `DecontaminationNameKeyword` として一箇所に定義）。
- 汚染半径デフォルト: 中心最大濃度、外周 `700m` へ線形減衰（`DefaultRadiusMeters = 700f`）。
- 除染猶予: ゲーム内 `50` 年（`ExpiryYears = 50`）。

---

## File Structure

```
原子力発電所プロジェクト/
├─ NuclearMeltdown.sln
├─ build.ps1                                  # MSBuild実行 + Modフォルダへ配置
├─ src/NuclearMeltdown/
│  ├─ NuclearMeltdown.csproj                  # net35, 旧形式, PackageReference:CitiesHarmony.API
│  ├─ Properties/AssemblyInfo.cs
│  ├─ Core/                                    # Unity非依存・テスト対象
│  │   ├─ CellDose.cs                          # struct { int Index; byte Intensity; }
│  │   ├─ PollutionGrid.cs                     # 座標変換 + 半径セル列挙
│  │   ├─ MeltdownClock.cs                     # 50年経過判定
│  │   ├─ ContaminationZone.cs                 # struct ゾーンデータ
│  │   └─ ZoneSerializer.cs                    # byte[] 直列化/復元（versioned）
│  ├─ Game/
│  │   ├─ Mod.cs                               # IUserMod + Harmony bootstrap
│  │   ├─ ModConfig.cs                         # 定数（半径/年数/キーワード/HarmonyID）
│  │   ├─ NuclearDetector.cs                   # IsNuclearPlant(ushort)
│  │   ├─ PollutionField.cs                    # NaturalResourceManagerへの読み書き
│  │   ├─ ContaminationManager.cs              # ゾーン台帳 + 適用/維持/除去
│  │   ├─ MeltdownEffect.cs                    # 爆発エフェクト + 初回発災
│  │   ├─ Patches/CollapseBuildingPatch.cs     # Harmony Prefix/Postfix
│  │   ├─ Simulation/MeltdownThreadingExtension.cs
│  │   └─ Serialization/ContaminationDataExtension.cs
│  └─ README.md
└─ tests/NuclearMeltdown.Core.Tests/
   ├─ NuclearMeltdown.Core.Tests.csproj        # net8, xUnit, Coreソースをlink参照
   ├─ PollutionGridTests.cs
   ├─ MeltdownClockTests.cs
   └─ ZoneSerializerTests.cs
```

**依存の向き:** `Game/*` → `Core/*`（一方向）。`Core/*` は他に依存しない。テストは `Core/*` のみ。

---

## Task 1: ソリューション骨組みとCoreデータ型（CellDose / ContaminationZone）

**Files:**
- Create: `src/NuclearMeltdown/Core/CellDose.cs`
- Create: `src/NuclearMeltdown/Core/ContaminationZone.cs`
- Create: `tests/NuclearMeltdown.Core.Tests/NuclearMeltdown.Core.Tests.csproj`
- Create: `tests/NuclearMeltdown.Core.Tests/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `struct CellDose { public int Index; public byte Intensity; public CellDose(int index, byte intensity); }`（namespace `NuclearMeltdown.Core`）
  - `struct ContaminationZone { public float CenterX; public float CenterZ; public float Radius; public long StartTicks; public ContaminationZone(float centerX, float centerZ, float radius, long startTicks); }`

- [ ] **Step 1: テストプロジェクトを作成（Coreソースをリンク参照）**

`tests/NuclearMeltdown.Core.Tests/NuclearMeltdown.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Coreの実ソースを直接コンパイルしてテスト（別ビルド不要） -->
    <Compile Include="..\..\src\NuclearMeltdown\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```
注: `LangVersion=7.3` は net35 と互換の言語機能に制約するための保険（ValueTuple等の混入をレビューで気づきやすくする。7.3自体はValueTupleを許すが、net35側ビルドで検出される）。

- [ ] **Step 2: 失敗するスモークテストを書く**

`tests/NuclearMeltdown.Core.Tests/SmokeTest.cs`:
```csharp
using NuclearMeltdown.Core;
using Xunit;

public class SmokeTest
{
    [Fact]
    public void CellDose_stores_fields()
    {
        var d = new CellDose(5, 200);
        Assert.Equal(5, d.Index);
        Assert.Equal((byte)200, d.Intensity);
    }

    [Fact]
    public void ContaminationZone_stores_fields()
    {
        var z = new ContaminationZone(10f, 20f, 700f, 123L);
        Assert.Equal(10f, z.CenterX);
        Assert.Equal(20f, z.CenterZ);
        Assert.Equal(700f, z.Radius);
        Assert.Equal(123L, z.StartTicks);
    }
}
```

- [ ] **Step 3: run the tests and confirm they fail.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: FAIL（`CellDose`/`ContaminationZone` が未定義でコンパイルエラー）

- [ ] **Step 4: Coreデータ型を実装**

`src/NuclearMeltdown/Core/CellDose.cs`:
```csharp
namespace NuclearMeltdown.Core
{
    /// <summary>汚染を適用する単一セル（グリッドindex）とその濃度(0-255)。</summary>
    public struct CellDose
    {
        public int Index;
        public byte Intensity;

        public CellDose(int index, byte intensity)
        {
            Index = index;
            Intensity = intensity;
        }
    }
}
```

`src/NuclearMeltdown/Core/ContaminationZone.cs`:
```csharp
namespace NuclearMeltdown.Core
{
    /// <summary>A contamination zone: world-space centre, radius in metres, and the in-game time it started (DateTime.Ticks).</summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
        }
    }
}
```

- [ ] **Step 5: run the tests and confirm they pass.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: PASS（2件）

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/Core tests/NuclearMeltdown.Core.Tests
git commit -m "feat: Coreデータ型 CellDose/ContaminationZone とテスト基盤を追加"
```

---

## Task 2: PollutionGrid（座標変換と半径セル列挙）

**Files:**
- Create: `src/NuclearMeltdown/Core/PollutionGrid.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/PollutionGridTests.cs`

**Interfaces:**
- Consumes: `CellDose`（Task 1）
- Produces（すべて `static class PollutionGrid`, namespace `NuclearMeltdown.Core`）:
  - `const float CellSize = 33.75f;`
  - `const int Resolution = 512;`
  - `int WorldToCell(float world)` → `Clamp((int)(world / 33.75f + 256f), 0, 511)`
  - `int CellIndex(int cellX, int cellZ)` → `cellZ * 512 + cellX`
  - `System.Collections.Generic.List<CellDose> CellsInRadius(float centerX, float centerZ, float radiusMeters, byte maxIntensity)` — 中心 `maxIntensity`、外周0への線形減衰。半径外は含めない。各要素は一意のindex。

- [ ] **Step 1: write the failing tests.**

`tests/NuclearMeltdown.Core.Tests/PollutionGridTests.cs`:
```csharp
using System.Collections.Generic;
using NuclearMeltdown.Core;
using Xunit;

public class PollutionGridTests
{
    [Fact]
    public void WorldToCell_maps_origin_to_center()
    {
        Assert.Equal(256, PollutionGrid.WorldToCell(0f));
    }

    [Fact]
    public void WorldToCell_clamps_out_of_range()
    {
        Assert.Equal(0, PollutionGrid.WorldToCell(-100000f));
        Assert.Equal(511, PollutionGrid.WorldToCell(100000f));
    }

    [Fact]
    public void CellIndex_is_row_major()
    {
        Assert.Equal(2 * 512 + 3, PollutionGrid.CellIndex(3, 2));
    }

    [Fact]
    public void CellsInRadius_center_has_max_intensity()
    {
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 700f, 255);
        int centerIndex = PollutionGrid.CellIndex(256, 256);
        var center = cells.Find(c => c.Index == centerIndex);
        Assert.Equal((byte)255, center.Intensity);
    }

    [Fact]
    public void CellsInRadius_excludes_cells_outside_radius()
    {
        // 半径 1セル(33.75m)未満 → 実質中心セルのみ
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 10f, 255);
        Assert.All(cells, c =>
        {
            int cz = c.Index / 512;
            int cx = c.Index % 512;
            Assert.InRange(cx, 255, 257);
            Assert.InRange(cz, 255, 257);
        });
    }

    [Fact]
    public void CellsInRadius_indices_are_unique()
    {
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 300f, 255);
        var seen = new HashSet<int>();
        foreach (var c in cells) Assert.True(seen.Add(c.Index), "duplicate index " + c.Index);
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: FAIL（`PollutionGrid` 未定義）

- [ ] **Step 3: PollutionGridを実装**

`src/NuclearMeltdown/Core/PollutionGrid.cs`:
```csharp
using System.Collections.Generic;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// NaturalResourceManager の汚染グリッド(512x512, セル33.75m)に対する
    /// Unity非依存の座標計算・半径列挙。
    /// </summary>
    public static class PollutionGrid
    {
        public const float CellSize = 33.75f;
        public const int Resolution = 512;

        public static int WorldToCell(float world)
        {
            int cell = (int)(world / CellSize + 256f);
            if (cell < 0) return 0;
            if (cell > Resolution - 1) return Resolution - 1;
            return cell;
        }

        public static int CellIndex(int cellX, int cellZ)
        {
            return cellZ * Resolution + cellX;
        }

        /// <summary>
        /// 中心(centerX,centerZ)・半径radiusMetersの円内セルを列挙。
        /// 濃度は中心 maxIntensity、半径端で0への線形減衰（半径外は含めない）。
        /// </summary>
        public static List<CellDose> CellsInRadius(float centerX, float centerZ, float radiusMeters, byte maxIntensity)
        {
            var result = new List<CellDose>();
            if (radiusMeters <= 0f) return result;

            int cellRadius = (int)(radiusMeters / CellSize) + 1;
            int centerCellX = WorldToCell(centerX);
            int centerCellZ = WorldToCell(centerZ);

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                int cz = centerCellZ + dz;
                if (cz < 0 || cz > Resolution - 1) continue;
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int cx = centerCellX + dx;
                    if (cx < 0 || cx > Resolution - 1) continue;

                    // セル中心のワールド距離で判定
                    float worldDx = dx * CellSize;
                    float worldDz = dz * CellSize;
                    float dist = (float)System.Math.Sqrt(worldDx * worldDx + worldDz * worldDz);
                    if (dist > radiusMeters) continue;

                    float t = 1f - (dist / radiusMeters); // 中心1..端0
                    if (t < 0f) t = 0f;
                    byte intensity = (byte)(maxIntensity * t);
                    result.Add(new CellDose(CellIndex(cx, cz), intensity));
                }
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Core/PollutionGrid.cs tests/NuclearMeltdown.Core.Tests/PollutionGridTests.cs
git commit -m "feat: PollutionGrid 座標変換と半径セル列挙を追加"
```

---

## Task 3: MeltdownClock（50年経過判定）

**Files:**
- Create: `src/NuclearMeltdown/Core/MeltdownClock.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/MeltdownClockTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces（`static class MeltdownClock`, namespace `NuclearMeltdown.Core`）:
  - `bool HasExpired(long startTicks, long nowTicks, int years)` — `now >= start.AddYears(years)` を DateTime で計算。

- [ ] **Step 1: write the failing tests.**

`tests/NuclearMeltdown.Core.Tests/MeltdownClockTests.cs`:
```csharp
using System;
using NuclearMeltdown.Core;
using Xunit;

public class MeltdownClockTests
{
    [Fact]
    public void Not_expired_before_years_elapse()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2049, 12, 31);
        Assert.False(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }

    [Fact]
    public void Expired_exactly_at_boundary()
    {
        var start = new DateTime(2000, 1, 1);
        var now = new DateTime(2050, 1, 1);
        Assert.True(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }

    [Fact]
    public void Expired_after_boundary()
    {
        var start = new DateTime(2000, 6, 15);
        var now = new DateTime(2051, 1, 1);
        Assert.True(MeltdownClock.HasExpired(start.Ticks, now.Ticks, 50));
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: FAIL（`MeltdownClock` 未定義）

- [ ] **Step 3: MeltdownClockを実装**

`src/NuclearMeltdown/Core/MeltdownClock.cs`:
```csharp
using System;

namespace NuclearMeltdown.Core
{
    /// <summary>Decides when a contamination zone has aged out, based on in-game time.</summary>
    public static class MeltdownClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int years)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddYears(years);
            return nowTicks >= expiry.Ticks;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Core/MeltdownClock.cs tests/NuclearMeltdown.Core.Tests/MeltdownClockTests.cs
git commit -m "feat: MeltdownClock 50年経過判定を追加"
```

---

## Task 4: ZoneSerializer（ゾーン台帳のバイナリ直列化）

**Files:**
- Create: `src/NuclearMeltdown/Core/ZoneSerializer.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/ZoneSerializerTests.cs`

**Interfaces:**
- Consumes `ContaminationZone` from Task 1
- Produces（`static class ZoneSerializer`, namespace `NuclearMeltdown.Core`）:
  - `const byte Version = 1;`
  - `byte[] Serialize(List<ContaminationZone> zones)` — 先頭にVersion(byte)、次にcount(int)、各ゾーンに CenterX,CenterZ,Radius(float×3)+StartTicks(long)。`BinaryWriter`使用。
  - `List<ContaminationZone> Deserialize(byte[] data)` — null/空/未知Version/破損時は空リストを返す（例外を投げない）。

- [ ] **Step 1: write the failing tests.**

`tests/NuclearMeltdown.Core.Tests/ZoneSerializerTests.cs`:
```csharp
using System.Collections.Generic;
using NuclearMeltdown.Core;
using Xunit;

public class ZoneSerializerTests
{
    [Fact]
    public void Round_trips_zones()
    {
        var zones = new List<ContaminationZone>
        {
            new ContaminationZone(100f, -200f, 700f, 630000000000000000L),
            new ContaminationZone(0f, 0f, 500f, 630000000000000001L),
        };
        byte[] bytes = ZoneSerializer.Serialize(zones);
        List<ContaminationZone> back = ZoneSerializer.Deserialize(bytes);

        Assert.Equal(2, back.Count);
        Assert.Equal(100f, back[0].CenterX);
        Assert.Equal(-200f, back[0].CenterZ);
        Assert.Equal(700f, back[0].Radius);
        Assert.Equal(630000000000000000L, back[0].StartTicks);
        Assert.Equal(630000000000000001L, back[1].StartTicks);
    }

    [Fact]
    public void Empty_list_round_trips()
    {
        byte[] bytes = ZoneSerializer.Serialize(new List<ContaminationZone>());
        Assert.Empty(ZoneSerializer.Deserialize(bytes));
    }

    [Fact]
    public void Null_input_returns_empty()
    {
        Assert.Empty(ZoneSerializer.Deserialize(null));
    }

    [Fact]
    public void Corrupt_input_returns_empty_without_throwing()
    {
        Assert.Empty(ZoneSerializer.Deserialize(new byte[] { 9, 9, 9 })); // 未知Version
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: FAIL, `ZoneSerializer` is not defined yet

- [ ] **Step 3: ZoneSerializerを実装**

`src/NuclearMeltdown/Core/ZoneSerializer.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace NuclearMeltdown.Core
{
    /// <summary>汚染ゾーン台帳を byte[] に直列化/復元（セーブデータ保存用）。</summary>
    public static class ZoneSerializer
    {
        public const byte Version = 1;

        public static byte[] Serialize(List<ContaminationZone> zones)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write(zones.Count);
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    w.Write(z.CenterX);
                    w.Write(z.CenterZ);
                    w.Write(z.Radius);
                    w.Write(z.StartTicks);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static List<ContaminationZone> Deserialize(byte[] data)
        {
            var result = new List<ContaminationZone>();
            if (data == null || data.Length < 5) return result;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms))
                {
                    byte version = r.ReadByte();
                    if (version != Version) return new List<ContaminationZone>();
                    int count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        float cx = r.ReadSingle();
                        float cz = r.ReadSingle();
                        float radius = r.ReadSingle();
                        long start = r.ReadInt64();
                        result.Add(new ContaminationZone(cx, cz, radius, start));
                    }
                }
            }
            catch
            {
                return new List<ContaminationZone>(); // 破損時は空
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: run the tests and confirm they pass.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: PASS (all of them)

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Core/ZoneSerializer.cs tests/NuclearMeltdown.Core.Tests/ZoneSerializerTests.cs
git commit -m "feat: ZoneSerializer ゾーン台帳の直列化/復元を追加"
```

---

## Task 5: Mod本体プロジェクト（csproj/AssemblyInfo/ModConfig/Mod）とビルド検証

このタスクからゲーム統合層。ゲーム型に依存するため単体テストは行わず、**MSBuildコンパイル成功**を検証ゲートにする。

**Files:**
- Create: `src/NuclearMeltdown/NuclearMeltdown.csproj`
- Create: `src/NuclearMeltdown/Properties/AssemblyInfo.cs`
- Create: `src/NuclearMeltdown/Game/ModConfig.cs`
- Create: `src/NuclearMeltdown/Game/Mod.cs`
- Create: `NuclearMeltdown.sln`

**Interfaces:**
- Consumes: nothing（Core型は後続タスクで参照）
- Produces:
  - `static class ModConfig`: `const string HarmonyId = "com.omone.nuclearmeltdown";`, `const float DefaultRadiusMeters = 700f;`, `const int ExpiryYears = 50;`, `const string DecontaminationNameKeyword = "Water Treatment";`, `const string NuclearNameKeyword = "Nuclear";`, `const byte MaxPollution = 255;`, `const string LogPrefix = "[NuclearMeltdown] ";`, `static void Log(string msg)`。
  - `class Mod : IUserMod`: `string Name { get; }`, `string Description { get; }`。

- [ ] **Step 1: csproj を作成（net35, 旧形式 + PackageReference）**

`src/NuclearMeltdown/NuclearMeltdown.csproj`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.props" Condition="Exists('$(MSBuildToolsPath)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{B1E7A2C0-0000-4000-8000-000000000001}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>NuclearMeltdown</RootNamespace>
    <AssemblyName>NuclearMeltdown</AssemblyName>
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <LangVersion>7.3</LangVersion>
    <FileAlignment>512</FileAlignment>
    <ManagedDLLPath>C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed</ManagedDLLPath>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="ICities">
      <HintPath>$(ManagedDLLPath)\ICities.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDLLPath)\Assembly-CSharp.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="ColossalManaged">
      <HintPath>$(ManagedDLLPath)\ColossalManaged.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDLLPath)\UnityEngine.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="CitiesHarmony.API" Version="2.2.0" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Core\**\*.cs" />
    <Compile Include="Game\**\*.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```
注: PackageReference を旧形式csprojで使うため、`build.ps1` は `msbuild -restore` を用いる（Step 5参照）。`CitiesHarmony.API` のバージョンは restore 時に最新2.x系へ調整可。

- [ ] **Step 2: AssemblyInfo と ModConfig と Mod を作成**

`src/NuclearMeltdown/Properties/AssemblyInfo.cs`:
```csharp
using System.Reflection;
[assembly: AssemblyTitle("NuclearMeltdown")]
[assembly: AssemblyProduct("NuclearMeltdown")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

`src/NuclearMeltdown/Game/ModConfig.cs`:
```csharp
using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>Mod-wide constants and shared logging.</summary>
    public static class ModConfig
    {
        public const string HarmonyId = "com.omone.nuclearmeltdown";
        public const float DefaultRadiusMeters = 700f;
        public const int ExpiryYears = 50;
        public const string DecontaminationNameKeyword = "Water Treatment";
        public const string NuclearNameKeyword = "Nuclear";
        public const byte MaxPollution = 255;
        public const string LogPrefix = "[NuclearMeltdown] ";

        public static void Log(string msg)
        {
            Debug.Log(LogPrefix + msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(LogPrefix + msg);
        }
    }
}
```

`src/NuclearMeltdown/Game/Mod.cs`:
```csharp
using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    /// <summary>Modエントリポイント。IUserMod実装 + Harmonyパッチの適用/解除。</summary>
    public class Mod : IUserMod
    {
        public string Name => "Nuclear Meltdown";
        public string Description => "When a nuclear power plant burns down or collapses, sets off an explosion and spreads radioactive contamination over a wide area. The contamination lifts after 50 in-game years, or sooner with a decontamination facility.";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
```
注: `Patcher` は Task 6 で作成。このタスクでは `Mod.cs` に `Patcher` 参照が未解決になるため、**Step 2の時点では `OnEnabled`/`OnDisabled` の本体をコメントアウトまたは空**にし、Task 6完了時に有効化する。ビルド検証を通すため、この段階の `Mod.cs` は下記の暫定版を使う:
```csharp
using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Nuclear Meltdown";
        public string Description => "When a nuclear power plant burns down or collapses, sets off an explosion and spreads radioactive contamination over a wide area. The contamination lifts after 50 in-game years, or sooner with a decontamination facility.";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => ModConfig.Log("enabled (patches applied in Task 6)"));
        }

        public void OnDisabled() { }
    }
}
```

- [ ] **Step 3: ソリューションファイルを作成**

`NuclearMeltdown.sln`（最小構成。Mod本体のみをMSBuildでビルド／テストはdotnet別管理）:
```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NuclearMeltdown", "src\NuclearMeltdown\NuclearMeltdown.csproj", "{B1E7A2C0-0000-4000-8000-000000000001}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{B1E7A2C0-0000-4000-8000-000000000001}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B1E7A2C0-0000-4000-8000-000000000001}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
```

- [ ] **Step 4: build.ps1 を作成（restore付きMSBuild + Modフォルダへ配置）**

`build.ps1`:
```powershell
$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\NuclearMeltdown\NuclearMeltdown.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\NuclearMeltdown\bin\Release\NuclearMeltdown.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force
$apiDll = "src\NuclearMeltdown\bin\Release\CitiesHarmony.API.dll"
if (Test-Path $apiDll) { Copy-Item $apiDll $modDir -Force }
Write-Host "Deploy complete: $modDir"
```

- [ ] **Step 5: ビルド検証（コンパイル成功）**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ビルド成功` → `NuclearMeltdown.dll` が生成され Modフォルダへコピーされる。エラーが出た場合は参照パス/PackageReference restore を修正してから次へ。

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/NuclearMeltdown.csproj src/NuclearMeltdown/Properties src/NuclearMeltdown/Game/ModConfig.cs src/NuclearMeltdown/Game/Mod.cs NuclearMeltdown.sln build.ps1
git commit -m "feat: Mod本体プロジェクト骨組みとビルド/配置スクリプトを追加"
```

---

## Task 6: NuclearDetector と Harmonyパッチ（破壊検知トリガー）

**Files:**
- Create: `src/NuclearMeltdown/Game/NuclearDetector.cs`
- Create: `src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`
- Create: `src/NuclearMeltdown/Game/Patcher.cs`
- Modify: `src/NuclearMeltdown/Game/Mod.cs`（暫定版 → 正式版に差し替え）

**Interfaces:**
- Consumes: `ModConfig`（Task 5）, `MeltdownEffect.Trigger`（Task 8 で実装。パッチからの呼び出しは Task 8 完了まで `ModConfig.Log` によるスタブにする）
- Produces:
  - `static class NuclearDetector`: `bool IsNuclearPlant(ushort buildingID)` — `BuildingManager.instance.m_buildings.m_buffer[id].Info.m_buildingAI is PowerPlantAI` かつ `Info.name` に `ModConfig.NuclearNameKeyword` を含む。
  - `static class Patcher`: `void PatchAll()`, `void UnpatchAll()`（Harmony `PatchAll`/`UnpatchAll` を `ModConfig.HarmonyId` で実行）。
  - `static class CollapseBuildingPatch`: Harmony が `CommonBuildingAI.CollapseBuilding` にPrefix/Postfixを適用。

- [ ] **Step 1: NuclearDetector を実装**

`src/NuclearMeltdown/Game/NuclearDetector.cs`:
```csharp
namespace NuclearMeltdown.Game
{
    /// <summary>建物が原子力発電所かどうかを判定する。</summary>
    public static class NuclearDetector
    {
        public static bool IsNuclearPlant(ushort buildingID)
        {
            if (buildingID == 0) return false;
            var info = BuildingManager.instance.m_buildings.m_buffer[buildingID].Info;
            if (info == null || info.m_buildingAI == null) return false;
            if (!(info.m_buildingAI is PowerPlantAI)) return false;
            string name = info.name;
            return name != null && name.Contains(ModConfig.NuclearNameKeyword);
        }
    }
}
```
注: `BuildingManager`, `PowerPlantAI` はグローバル名前空間（Assembly-CSharp）。`using` 不要。

- [ ] **Step 2: Patcher を実装**

`src/NuclearMeltdown/Game/Patcher.cs`:
```csharp
using HarmonyLib;

namespace NuclearMeltdown.Game
{
    /// <summary>Harmonyパッチの適用/解除。</summary>
    public static class Patcher
    {
        private static bool _patched;

        public static void PatchAll()
        {
            if (_patched) return;
            var harmony = new Harmony(ModConfig.HarmonyId);
            harmony.PatchAll(typeof(Patcher).Assembly);
            _patched = true;
            ModConfig.Log("Harmony patches applied");
        }

        public static void UnpatchAll()
        {
            if (!_patched) return;
            var harmony = new Harmony(ModConfig.HarmonyId);
            harmony.UnpatchAll(ModConfig.HarmonyId);
            _patched = false;
            ModConfig.Log("Harmony patches removed");
        }
    }
}
```

- [ ] **Step 3: CollapseBuildingPatch を実装（Prefixで崩壊前状態を退避、Postfixで初回崩壊のみ発火）**

`src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`:
```csharp
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
                // Task 8 で MeltdownEffect.Trigger(pos) に置換
                ModConfig.Log("Nuclear plant collapsed at " + pos + " (effect stub)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("CollapseBuildingPatch error: " + e);
            }
        }
    }
}
```
注: `CommonBuildingAI`, `Building`, `InstanceManager` はグローバル名前空間。`Harmony` の `__state`/`__result` は名前一致で注入される。

- [ ] **Step 4: Mod.cs を正式版へ差し替え**

`src/NuclearMeltdown/Game/Mod.cs`（Task 5 Step 2 の暫定版を置換）:
```csharp
using CitiesHarmony.API;
using ICities;

namespace NuclearMeltdown.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Nuclear Meltdown";
        public string Description => "When a nuclear power plant burns down or collapses, sets off an explosion and spreads radioactive contamination over a wide area. The contamination lifts after 50 in-game years, or sooner with a decontamination facility.";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled)
            {
                Patcher.UnpatchAll();
            }
        }
    }
}
```

- [ ] **Step 5: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.`CommonBuildingAI`/`Building.Flags`/`HarmonyLib` の解決を確認（HarmonyLib は CitiesHarmony.API の依存で restore 済み）。

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/Game/NuclearDetector.cs src/NuclearMeltdown/Game/Patcher.cs src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs src/NuclearMeltdown/Game/Mod.cs
git commit -m "feat: 原発判定とCollapseBuildingパッチ(破壊検知トリガー)を追加"
```

---

## Task 7: PollutionField（NaturalResourceManagerへの汚染読み書き）と ContaminationManager

**Files:**
- Create: `src/NuclearMeltdown/Game/PollutionField.cs`
- Create: `src/NuclearMeltdown/Game/ContaminationManager.cs`

**Interfaces:**
- Consumes: `PollutionGrid`, `CellDose`, `ContaminationZone`（Core）, `ModConfig`
- Produces:
  - `static class PollutionField`:
    - `void ApplyDose(CellDose dose)` — 対象セルの `m_pollution` を `Max(current, dose.Intensity)` に上げる。
    - `void ClearCell(int index)` — `m_pollution = 0`。
    - `void Refresh(int minX, int minZ, int maxX, int maxZ)` — `NaturalResourceManager.instance.AreaModifiedB(...)`。
    - `byte GetPollution(int index)`。
  - `static class ContaminationManager`:
    - `List<ContaminationZone> Zones { get; }`（読み取り用スナップショット）
    - `void ReplaceAll(List<ContaminationZone> zones)`（ロード復元用。全汚染を一旦書き直す）
    - `void AddZone(ContaminationZone zone)` — 台帳へ追加し初回汚染を適用。
    - `void RemoveZoneAt(int index)` — 台帳から除去（汚染はクリアしない＝除染/自然減衰に委ねる。ただし期限切れ時は呼び出し側が先にClearZone）。
    - `void ReassertZone(ContaminationZone zone)` — 半径内セルを再度 `ApplyDose`（自然減衰対策）。
    - `void ClearZone(ContaminationZone zone)` — 半径内セルを0にしてRefresh。
    - `void DecontaminateAround(float worldX, float worldZ, float radiusMeters, int step)` — 指定範囲のセル `m_pollution` を `step` 分減衰させRefreshWide。
  - 内部で全ゾーンの外接矩形をRefreshするための最小/最大セル計算を持つ。

- [ ] **Step 1: PollutionField を実装**

`src/NuclearMeltdown/Game/PollutionField.cs`:
```csharp
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>NaturalResourceManager の土壌汚染セルへの読み書きラッパ。</summary>
    public static class PollutionField
    {
        public static byte GetPollution(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return 0;
            return arr[index].m_pollution;
        }

        public static void ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
            }
        }

        public static void ClearCell(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return;
            arr[index].m_pollution = 0;
        }

        public static void ReducePollution(int index, int step)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return;
            int v = arr[index].m_pollution - step;
            arr[index].m_pollution = (byte)(v < 0 ? 0 : v);
        }

        /// <summary>汚染テクスチャを更新（cellX/cellZ範囲）。</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
```
注: `m_naturalResources` は構造体配列。`arr[i].m_pollution = x` はインプレース代入で有効。

- [ ] **Step 2: ContaminationManager を実装**

`src/NuclearMeltdown/Game/ContaminationManager.cs`:
```csharp
using System.Collections.Generic;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>汚染ゾーン台帳と、グリッドへの適用/維持/除去。</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();

        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        public static void AddZone(ContaminationZone zone)
        {
            _zones.Add(zone);
            ReassertZone(zone);
        }

        public static void RemoveZoneAt(int index)
        {
            if (index >= 0 && index < _zones.Count) _zones.RemoveAt(index);
        }

        public static void ReassertZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            for (int i = 0; i < doses.Count; i++) PollutionField.ApplyDose(doses[i]);
            RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            for (int i = 0; i < doses.Count; i++) PollutionField.ClearCell(doses[i].Index);
            RefreshZoneTexture(zone);
        }

        private static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(zone.CenterX);
            int cz = PollutionGrid.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > PollutionGrid.Resolution - 1) return PollutionGrid.Resolution - 1;
            return v;
        }
    }
}
```
注: `NaturalResourceManager` はグローバル名前空間。

- [ ] **Step 3: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: commit.**

```bash
git add src/NuclearMeltdown/Game/PollutionField.cs src/NuclearMeltdown/Game/ContaminationManager.cs
git commit -m "feat: 汚染グリッド書込(PollutionField)とゾーン台帳(ContaminationManager)を追加"
```

---

## Task 8: MeltdownEffect（爆発エフェクト + 初回発災）とパッチ結線

**Files:**
- Create: `src/NuclearMeltdown/Game/MeltdownEffect.cs`
- Modify: `src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`（スタブ → `MeltdownEffect.Trigger`）

**Interfaces:**
- Consumes: `ContaminationManager`, `ModConfig`, `SimulationManager`, `EffectManager`, `PrefabCollection<DisasterInfo>`
- Produces:
  - `static class MeltdownEffect`:
    - `void Trigger(Vector3 position)` — (1) 爆発エフェクト再生（取得できれば）, (2) ゾーンを `ContaminationManager.AddZone` で登録（開始時刻 = `SimulationManager.instance.m_currentGameTime.Ticks`）。
    - `EffectInfo ResolveExplosionEffect()` — ロード済み `MeteorAI.m_impactEffect` を探索、無ければ null。

- [ ] **Step 1: MeltdownEffect を実装**

`src/NuclearMeltdown/Game/MeltdownEffect.cs`:
```csharp
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
            int count = PrefabCollection<DisasterInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                DisasterInfo info = PrefabCollection<DisasterInfo>.GetLoaded((uint)i);
                if (info == null) continue;
                MeteorAI ai = info.m_disasterAI as MeteorAI;
                if (ai != null && ai.m_impactEffect != null) return ai.m_impactEffect;
            }
            return null;
        }
    }
}
```
注: `SimulationManager`, `EffectManager`, `EffectInfo`, `InstanceID`, `VehicleManager`, `Singleton<>`, `PrefabCollection<>`, `DisasterInfo`, `MeteorAI` はグローバル名前空間。`DisasterInfo` のAIフィールド名は `m_disasterAI`（Task検証: `DisasterInfo` を逆コンパイルして確認済みでない場合は `ilspycmd Assembly-CSharp.dll -t DisasterInfo` でフィールド名を確認してから確定）。

- [ ] **Step 2: パッチのスタブを実呼び出しへ置換**

`src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs` の Postfix 内、以下の行:
```csharp
                Vector3 pos = data.m_position;
                // Task 8 で MeltdownEffect.Trigger(pos) に置換
                ModConfig.Log("Nuclear plant collapsed at " + pos + " (effect stub)");
```
を次に置換:
```csharp
                Vector3 pos = data.m_position;
                MeltdownEffect.Trigger(pos);
```

- [ ] **Step 3: `DisasterInfo` のAIフィールド名を確認**

Run:
```bash
ilspycmd "/c/Program Files (x86)/Steam/steamapps/common/Cities_Skylines/Cities_Data/Managed/Assembly-CSharp.dll" -t DisasterInfo -o /tmp/dinfo && grep -nE "DisasterAI|m_disasterAI|public .*AI " /tmp/dinfo/DisasterInfo.decompiled.cs
```
Expected: `m_disasterAI` のフィールド名を確認。異なる場合は Step 1 の `info.m_disasterAI` を実名に修正。

- [ ] **Step 4: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Game/MeltdownEffect.cs src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs
git commit -m "feat: 爆発エフェクトと汚染ゾーン発生(MeltdownEffect)をパッチに結線"
```

---

## Task 9: MeltdownThreadingExtension（毎tick 維持/期限/除染）

**Files:**
- Create: `src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs`

**Interfaces:**
- Consumes: `ContaminationManager`, `MeltdownClock`, `ModConfig`, `SimulationManager`, `BuildingManager`
- Produces:
  - `class MeltdownThreadingExtension : ThreadingExtensionBase` — `OnAfterSimulationTick()` をオーバーライド。ゲームが自動検出・実行。
    - 一定tick間隔（例: 内部カウンタで16tickに1回）で全ゾーンを処理:
      1. 期限（`MeltdownClock.HasExpired`）→ `ClearZone` して台帳から除去。
      2. 除染施設が近傍稼働 → `ReducePollution` 相当で徐々に除去。全消去でゾーン除去。
      3. それ以外 → `ReassertZone`（維持）。

- [ ] **Step 1: MeltdownThreadingExtension を実装**

`src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs`:
```csharp
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game.Simulation
{
    /// <summary>
    /// 毎tickで汚染ゾーンを維持し、50年経過または除染施設稼働で解除する。
    /// ゲームがModアセンブリ内のIThreadingExtension実装を自動検出して駆動する。
    /// </summary>
    public class MeltdownThreadingExtension : ThreadingExtensionBase
    {
        private int _tickCounter;
        private const int ProcessInterval = 16; // 16tickに1回処理（負荷軽減）

        public override void OnAfterSimulationTick()
        {
            try
            {
                if (++_tickCounter < ProcessInterval) return;
                _tickCounter = 0;

                List<ContaminationZone> zones = ContaminationManager.Zones; // スナップショット
                if (zones.Count == 0) return;

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;

                // 後ろから走査してインデックス除去に対応
                for (int i = zones.Count - 1; i >= 0; i--)
                {
                    ContaminationZone zone = zones[i];

                    if (MeltdownClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ExpiryYears))
                    {
                        ContaminationManager.ClearZone(zone);
                        ContaminationManager.RemoveZoneAt(i);
                        ModConfig.Log("zone expired (50y) and cleared");
                        continue;
                    }

                    if (IsDecontaminationActive(zone))
                    {
                        DecontaminateZone(zone, i);
                        continue;
                    }

                    ContaminationManager.ReassertZone(zone); // 自然減衰対策で維持
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("threading error: " + e);
            }
        }

        /// <summary>ゾーン中心付近に除染対象建物(既定:下水処理施設)が稼働中か。</summary>
        private bool IsDecontaminationActive(ContaminationZone zone)
        {
            var bm = BuildingManager.instance;
            ushort[] grid = bm.m_buildingGrid;
            // ゾーン中心のビルディンググリッドセル(±1)を走査
            int gx = Mathf.Clamp((int)(zone.CenterX / 64f + 135f), 0, 269);
            int gz = Mathf.Clamp((int)(zone.CenterZ / 64f + 135f), 0, 269);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cell = (gz + dz) * 270 + (gx + dx);
                    if (cell < 0 || cell >= grid.Length) continue;
                    ushort id = grid[cell];
                    int guard = 0;
                    while (id != 0 && guard++ < 32768)
                    {
                        var info = bm.m_buildings.m_buffer[id].Info;
                        if (info != null && info.name != null &&
                            info.name.Contains(ModConfig.DecontaminationNameKeyword) &&
                            (bm.m_buildings.m_buffer[id].m_flags & Building.Flags.Active) != Building.Flags.None)
                        {
                            return true;
                        }
                        id = bm.m_buildings.m_buffer[id].m_nextGridBuilding;
                    }
                }
            }
            return false;
        }

        private void DecontaminateZone(ContaminationZone zone, int index)
        {
            var doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ModConfig.MaxPollution);
            bool anyRemaining = false;
            for (int i = 0; i < doses.Count; i++)
            {
                PollutionField.ReducePollution(doses[i].Index, 8); // 徐々に除去
                if (PollutionField.GetPollution(doses[i].Index) > 0) anyRemaining = true;
            }
            // テクスチャ更新
            ContaminationManager.RefreshZoneTexturePublic(zone);
            if (!anyRemaining)
            {
                ContaminationManager.RemoveZoneAt(index);
                ModConfig.Log("zone decontaminated and removed");
            }
        }
    }
}
```
注:
- `RefreshZoneTexturePublic` は Task 7 の private `RefreshZoneTexture` を public 化する必要がある → 下記 Step 2 で `ContaminationManager` に public メソッド `RefreshZoneTexture(ContaminationZone)` を公開する形に変更（private版を public にリネーム）。
- ビルディンググリッド定数（`/64f + 135f`, 解像度270）は Assembly-CSharp の `BuildingManager` 実測値。Step 3 で確認する。

- [ ] **Step 2: ContaminationManager の Refresh を公開**

`src/NuclearMeltdown/Game/ContaminationManager.cs` の `private static void RefreshZoneTexture(...)` を以下に変更（public化 + 呼び出し名統一）:
```csharp
        public static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(zone.CenterX);
            int cz = PollutionGrid.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }
```
そして `MeltdownThreadingExtension` 内の `ContaminationManager.RefreshZoneTexturePublic(zone)` を `ContaminationManager.RefreshZoneTexture(zone)` に修正する。

- [ ] **Step 3: BuildingManagerのグリッド定数を確認**

Run:
```bash
ilspycmd "/c/Program Files (x86)/Steam/steamapps/common/Cities_Skylines/Cities_Data/Managed/Assembly-CSharp.dll" -t BuildingManager -o /tmp/bm && grep -nE "m_buildingGrid|/ 64f|\* 270|m_nextGridBuilding|BUILDINGGRID_RESOLUTION" /tmp/bm/BuildingManager.decompiled.cs | head
```
Expected: グリッド解像度270・セル64m・`m_nextGridBuilding` を確認。異なる場合は `IsDecontaminationActive` の定数を実測値に修正。

- [ ] **Step 4: ビルド検証**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs src/NuclearMeltdown/Game/ContaminationManager.cs
git commit -m "feat: 毎tickの汚染維持/50年期限/除染処理を追加"
```

---

## Task 10: ContaminationDataExtension（セーブ/ロード永続化）

**Files:**
- Create: `src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs`

**Interfaces:**
- Consumes: `ContaminationManager`, `ZoneSerializer`, `ModConfig`
- Produces:
  - `class ContaminationDataExtension : SerializableDataExtensionBase` — `OnSaveData()`/`OnLoadData()` をオーバーライド。データキー `"NuclearMeltdown.Contamination.v1"`。ゲームが自動検出。

- [ ] **Step 1: ContaminationDataExtension を実装**

`src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs`:
```csharp
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game.Serialization
{
    /// <summary>汚染ゾーン台帳をセーブデータへ永続化する。ゲームが自動検出。</summary>
    public class ContaminationDataExtension : SerializableDataExtensionBase
    {
        private const string DataId = "NuclearMeltdown.Contamination.v1";

        public override void OnSaveData()
        {
            try
            {
                List<ContaminationZone> zones = ContaminationManager.Zones;
                byte[] bytes = ZoneSerializer.Serialize(zones);
                serializableDataManager.SaveData(DataId, bytes);
                ModConfig.Log("saved " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("save error: " + e);
            }
        }

        public override void OnLoadData()
        {
            try
            {
                byte[] bytes = serializableDataManager.LoadData(DataId);
                List<ContaminationZone> zones = ZoneSerializer.Deserialize(bytes);
                ContaminationManager.ReplaceAll(zones);
                ModConfig.Log("loaded " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("load error: " + e);
            }
        }
    }
}
```
注: `serializableDataManager` は `SerializableDataExtensionBase` の保護プロパティ（型 `ISerializableData`）。

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs
git commit -m "feat: 汚染ゾーンのセーブ/ロード永続化を追加"
```

---

## Task 11: README とゲーム内動作確認ガイド

**Files:**
- Create: `src/NuclearMeltdown/README.md`

**Interfaces:**
- Consumes: nothing
- Produces: なし（ドキュメント）

- [ ] **Step 1: README を作成**

`src/NuclearMeltdown/README.md`:
```markdown
# Nuclear Meltdown (Cities: Skylines Mod)

原子力発電所が全焼または崩壊すると、隕石爆発エフェクトと広範囲の疑似放射能汚染（土壌汚染）を発生させる。汚染はゲーム内50年経過、または除染施設（既定: 下水処理施設 Water Treatment Plant）の稼働で消滅する。

## 依存
- Harmony (Mod Dependency) — Steam Workshop の CitiesHarmony を購読しておくこと。

## ビルドと配置
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\` に配置される。

## ゲーム内動作確認手順
1. 起動 → コンテンツマネージャ → Mods で "Nuclear Meltdown" を有効化。
2. Harmony が有効であること（ログに `[NuclearMeltdown] Harmony patches applied`）。
3. 原子力発電所を設置し、災害(隕石/竜巻等)または火災で全焼/崩壊させる。
4. 崩壊地点に爆発エフェクトが出て、周囲約700mが汚染（紫/汚染色）になることを確認。
5. 汚染ゾーン近傍に下水処理施設を稼働 → 汚染が徐々に消えることを確認。
6. （時間確認）ゲーム内で50年経過 → 汚染が自動消滅することを確認。
7. セーブ→ロードで汚染が維持されることを確認。

## 設定
定数は `Game/ModConfig.cs`（汚染半径・除染猶予年数・除染施設キーワード等）。

## ログ
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\` の output_log で `[NuclearMeltdown]` を検索。
```

- [ ] **Step 2: commit.**

```bash
git add src/NuclearMeltdown/README.md
git commit -m "docs: READMEと動作確認ガイドを追加"
```

---

## Task 12: 最終ビルド・全テスト・ゲーム内検証依頼

**Files:** なし（検証のみ）

- [ ] **Step 1: Coreの全テスト実行**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: 全テストPASS。

- [ ] **Step 2: Mod本体の最終ビルド・配置**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功、Modフォルダへ `NuclearMeltdown.dll` と `CitiesHarmony.API.dll` が配置。

- [ ] **Step 3: ゲーム内検証をユーザーへ依頼**

README の「ゲーム内動作確認手順」1–7 をユーザーに実施依頼（Claudeはゲーム起動テスト不可）。不具合が出た場合は output_log の `[NuclearMeltdown]` 行を共有してもらう。

- [ ] **Step 4: 最終コミット**

```bash
git add -A
git commit -m "chore: 最終ビルド・テスト確認"
```

---

## Self-Review

**1. Spec coverage（設計書との対応）:**
- 原発判定（PowerPlantAI + Prefab名）→ Task 6 `NuclearDetector` ✅
- トリガー（全焼/崩壊検知, CollapseBuilding Postfix）→ Task 6 `CollapseBuildingPatch` ✅
- 爆発エフェクト（隕石流用）→ Task 8 `MeltdownEffect` ✅
- 土壌汚染（NaturalResourceManager, 中心〜700m減衰）→ Task 2 `PollutionGrid` + Task 7 `PollutionField/ContaminationManager` ✅
- 汚染維持（自然減衰対策の再アサート）→ Task 9 `MeltdownThreadingExtension` ✅
- 50年で消滅 → Task 3 `MeltdownClock` + Task 9 ✅
- 除染施設で消滅（既存建物流用=下水処理施設）→ Task 9 `IsDecontaminationActive/DecontaminateZone` ✅
- セーブ/ロード永続化 → Task 4 `ZoneSerializer` + Task 10 `ContaminationDataExtension` ✅
- IUserMod（Name/Description表示）→ Task 5 `Mod` ✅
- CitiesHarmony（Harmony 2.0）→ Task 5/6 ✅
- エラーハンドリング（try/catch, 破損時空台帳）→ 各パッチ/tick/直列化 ✅
- ビルド→Mod配置 → Task 5 `build.ps1`, Task 12 ✅

**2. Placeholder scan:** "TBD"/"後で"/"適切に"等の曖昧語なし。各コードステップは実コードを提示。Task 8 Step 3・Task 9 Step 3 の逆コンパイル確認は「未確定の穴埋め」ではなく、実名の最終照合手順（デフォルト値を提示済み）。

**3. Type consistency:**
- `CellDose(int, byte)` / `ContaminationZone(float,float,float,long)` — Task 1定義とTask 2/4/7/9利用で一致。
- `PollutionGrid.CellsInRadius(float,float,float,byte)` — Task 2定義, Task 7/9利用で一致。
- `MeltdownClock.HasExpired(long,long,int)` — Task 3定義, Task 9利用で一致。
- `ZoneSerializer.Serialize/Deserialize(List<ContaminationZone>/byte[])` — Task 4定義, Task 10利用で一致。
- `ContaminationManager.RefreshZoneTexture(ContaminationZone)` — Task 9 Step 2でpublic化し呼び出し名を統一（`RefreshZoneTexturePublic` の混在を解消済み）。
- `MeltdownEffect.Trigger(Vector3)` — Task 8定義, Task 8でパッチから呼出、一致。
```
