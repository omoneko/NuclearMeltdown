# NuclearMeltdown Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a system mod for Cities: Skylines (2015) where a nuclear power plant burning down or collapsing sets off a meteor-style explosion and spreads wide-area radioactive contamination, modelled as ground pollution, which is held in place until 50 in-game years pass or a decontamination facility clears it.

**Architecture:** the pure logic - coordinate conversion, enumerating the cells in a radius, the 50-year test and serialising the zones - is separated into `Core/`, free of Unity and the game's types, and driven test-first with xUnit. The game integration layer - the Harmony patch, the effects, writing the pollution and the extension points - stays thin and calls into `Core`. Because CS decays pollution on its own, the contamination is reasserted every tick from a `ThreadingExtension` and released only on expiry or decontamination.

**Tech stack:** C# on .NET Framework 3.5 for the mod itself, built with MSBuild, plus CitiesHarmony.API (Harmony 2.0) and the ICities, Assembly-CSharp, UnityEngine and ColossalManaged references. The tests run on .NET 8 with xUnit, linking the Core sources directly.

## Global Constraints

- The mod targets **.NET Framework 3.5**. `Core` must avoid anything net35 does not have - `ValueTuple` and named tuples, `Span`, APIs newer than LINQ - because `Core` is compiled for both net35 and net8.
- The game DLLs come from `Cities_Data\Managed\` in the game's installation: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll` and `ColossalManaged.dll`, referenced with `Private=False` so they are not copied locally.
- Harmony comes from the `CitiesHarmony.API` NuGet package, and the patches are applied and removed through `HarmonyHelper.DoOnHarmonyReady` and `IsHarmonyInstalled`. The Harmony ID is `"com.omone.nuclearmeltdown"`.
- It deploys to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`.
- Logging goes through `UnityEngine.Debug.Log` with the `"[NuclearMeltdown] "` prefix and nowhere else; no `Console.WriteLine` or `print`.
- Every patch, tick and serialisation path is wrapped in try/catch, so no exception propagates into the game.
- The pollution grid constants, measured against Assembly-CSharp: `CELL_SIZE = 33.75f`, `RESOLUTION = 512`, the cell conversion `cell = Clamp((int)(world / 33.75f + 256f), 0, 511)`, `index = cellZ * 512 + cellX`, and `m_pollution` as a byte from 0 to 255.
- A decontamination facility is by default any building whose prefab name contains `"Water Treatment"`, defined in one place as the constant `DecontaminationNameKeyword`.
- The contamination radius defaults to maximum intensity at the centre falling off linearly to `700m`, as `DefaultRadiusMeters = 700f`.
- The contamination lasts `50` in-game years, as `ExpiryYears = 50`.

---

## File Structure

```
<repository root>/
├─ NuclearMeltdown.sln
├─ build.ps1                                  # runs MSBuild and deploys into the mod folder
├─ src/NuclearMeltdown/
│  ├─ NuclearMeltdown.csproj                  # net35, old-style, PackageReference: CitiesHarmony.API
│  ├─ Properties/AssemblyInfo.cs
│  ├─ Core/                                    # no Unity dependency; this is what the tests cover
│  │   ├─ CellDose.cs                          # struct { int Index; byte Intensity; }
│  │   ├─ PollutionGrid.cs                     # coordinate conversion and enumerating the cells in a radius
│  │   ├─ MeltdownClock.cs                     # the 50-year expiry test
│  │   ├─ ContaminationZone.cs                 # the zone data, as a struct
│  │   └─ ZoneSerializer.cs                    # serialises to and from byte[], versioned
│  ├─ Game/
│  │   ├─ Mod.cs                               # IUserMod + Harmony bootstrap
│  │   ├─ ModConfig.cs                         # the constants: radius, years, keywords, Harmony ID
│  │   ├─ NuclearDetector.cs                   # IsNuclearPlant(ushort)
│  │   ├─ PollutionField.cs                    # reads and writes NaturalResourceManager
│  │   ├─ ContaminationManager.cs              # the zone ledger, and applying, holding and clearing it
│  │   ├─ MeltdownEffect.cs                    # the explosion effect and the initial contamination
│  │   ├─ Patches/CollapseBuildingPatch.cs     # Harmony Prefix/Postfix
│  │   ├─ Simulation/MeltdownThreadingExtension.cs
│  │   └─ Serialization/ContaminationDataExtension.cs
│  └─ README.md
└─ tests/NuclearMeltdown.Core.Tests/
   ├─ NuclearMeltdown.Core.Tests.csproj        # net8, xUnit, linking the Core sources
   ├─ PollutionGridTests.cs
   ├─ MeltdownClockTests.cs
   └─ ZoneSerializerTests.cs
```

**Dependencies point one way:** `Game/*` depends on `Core/*`, `Core/*` depends on nothing else, and the tests cover `Core/*` alone.

---

## Task 1: the solution skeleton and the Core data types (CellDose and ContaminationZone)

**Files:**
- Create: `src/NuclearMeltdown/Core/CellDose.cs`
- Create: `src/NuclearMeltdown/Core/ContaminationZone.cs`
- Create: `tests/NuclearMeltdown.Core.Tests/NuclearMeltdown.Core.Tests.csproj`
- Create: `tests/NuclearMeltdown.Core.Tests/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `struct CellDose { public int Index; public byte Intensity; public CellDose(int index, byte intensity); }` in the `NuclearMeltdown.Core` namespace
  - `struct ContaminationZone { public float CenterX; public float CenterZ; public float Radius; public long StartTicks; public ContaminationZone(float centerX, float centerZ, float radius, long startTicks); }`

- [ ] **Step 1: create the test project, linking the Core sources.**

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
    <!-- Compile the real Core sources straight into the test assembly, so no separate build is needed -->
    <Compile Include="..\..\src\NuclearMeltdown\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```
Note that `LangVersion=7.3` is a safeguard, keeping the language features close to what net35 supports and making something like a stray ValueTuple easier to spot in review. 7.3 does allow ValueTuple, but the net35 build catches it.

- [ ] **Step 2: write a failing smoke test.**

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
Expected: FAIL with a compile error, since `CellDose` and `ContaminationZone` do not exist yet

- [ ] **Step 4: implement the Core data types.**

`src/NuclearMeltdown/Core/CellDose.cs`:
```csharp
namespace NuclearMeltdown.Core
{
    /// <summary>One grid cell (by index) to contaminate, and the intensity to apply (0-255).</summary>
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
Expected: PASS (2 tests)

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/Core tests/NuclearMeltdown.Core.Tests
git commit -m "feat: add the Core data types CellDose and ContaminationZone, plus the test scaffolding"
```

---

## Task 2: PollutionGrid (coordinate conversion and enumerating the cells in a radius)

**Files:**
- Create: `src/NuclearMeltdown/Core/PollutionGrid.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/PollutionGridTests.cs`

**Interfaces:**
- Consumes `CellDose` from Task 1
- Produces, all on `static class PollutionGrid` in the `NuclearMeltdown.Core` namespace:
  - `const float CellSize = 33.75f;`
  - `const int Resolution = 512;`
  - `int WorldToCell(float world)` → `Clamp((int)(world / 33.75f + 256f), 0, 511)`
  - `int CellIndex(int cellX, int cellZ)` → `cellZ * 512 + cellX`
  - `System.Collections.Generic.List<CellDose> CellsInRadius(float centerX, float centerZ, float radiusMeters, byte maxIntensity)` - `maxIntensity` at the centre falling off linearly to zero at the edge, excluding anything outside the radius, with a unique index per element.

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
        // A radius under one cell (33.75 m) means effectively just the centre cell.
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
Expected: FAIL, `PollutionGrid` is not defined yet

- [ ] **Step 3: implement PollutionGrid.**

`src/NuclearMeltdown/Core/PollutionGrid.cs`:
```csharp
using System.Collections.Generic;

namespace NuclearMeltdown.Core
{
    /// <summary>
    /// Coordinate maths and radius enumeration for NaturalResourceManager's pollution grid
    /// (512x512, 33.75 m cells). No Unity dependency.
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
        /// Lists the cells inside the circle at (centerX, centerZ) with the given radius.
        /// Intensity falls off linearly from maxIntensity at the centre to zero at the edge;
        /// cells outside the radius are not included.
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

                    // Test against the world distance from cell centre to cell centre.
                    float worldDx = dx * CellSize;
                    float worldDz = dz * CellSize;
                    float dist = (float)System.Math.Sqrt(worldDx * worldDx + worldDz * worldDz);
                    if (dist > radiusMeters) continue;

                    float t = 1f - (dist / radiusMeters); // 1 at the centre .. 0 at the edge
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
git commit -m "feat: add PollutionGrid, the coordinate conversion and radius enumeration"
```

---

## Task 3: MeltdownClock (the 50-year expiry test)

**Files:**
- Create: `src/NuclearMeltdown/Core/MeltdownClock.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/MeltdownClockTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces, on `static class MeltdownClock` in the `NuclearMeltdown.Core` namespace:
  - `bool HasExpired(long startTicks, long nowTicks, int years)` - computes `now >= start.AddYears(years)` through DateTime.

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
Expected: FAIL, `MeltdownClock` is not defined yet

- [ ] **Step 3: implement MeltdownClock.**

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
git commit -m "feat: add MeltdownClock, the 50-year expiry test"
```

---

## Task 4: ZoneSerializer (serialising the zone ledger to binary)

**Files:**
- Create: `src/NuclearMeltdown/Core/ZoneSerializer.cs`
- Test: `tests/NuclearMeltdown.Core.Tests/ZoneSerializerTests.cs`

**Interfaces:**
- Consumes `ContaminationZone` from Task 1
- Produces, on `static class ZoneSerializer` in the `NuclearMeltdown.Core` namespace:
  - `const byte Version = 1;`
  - `byte[] Serialize(List<ContaminationZone> zones)` - a version byte first, then the count as an int, then CenterX, CenterZ and Radius as three floats plus StartTicks as a long per zone, written with `BinaryWriter`.
  - `List<ContaminationZone> Deserialize(byte[] data)` - returns an empty list for null, empty, an unknown version or corrupt data, and never throws.

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
        Assert.Empty(ZoneSerializer.Deserialize(new byte[] { 9, 9, 9 })); // unknown version
    }
}
```

- [ ] **Step 2: run the tests and confirm they fail.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: FAIL, `ZoneSerializer` is not defined yet

- [ ] **Step 3: implement ZoneSerializer.**

`src/NuclearMeltdown/Core/ZoneSerializer.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace NuclearMeltdown.Core
{
    /// <summary>Serialises the contamination zone ledger to and from byte[] for the save game.</summary>
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
                return new List<ContaminationZone>(); // corrupt data yields nothing
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
git commit -m "feat: add ZoneSerializer, serialising and restoring the zone ledger"
```

---

## Task 5: the mod project (csproj, AssemblyInfo, ModConfig, Mod) and verifying the build

From here on this is the game integration layer. It depends on game types, so there are no unit tests; **the build succeeding under MSBuild** is the gate instead.

**Files:**
- Create: `src/NuclearMeltdown/NuclearMeltdown.csproj`
- Create: `src/NuclearMeltdown/Properties/AssemblyInfo.cs`
- Create: `src/NuclearMeltdown/Game/ModConfig.cs`
- Create: `src/NuclearMeltdown/Game/Mod.cs`
- Create: `NuclearMeltdown.sln`

**Interfaces:**
- Consumes nothing; the Core types are referenced in later tasks.
- Produces:
  - `static class ModConfig` with `const string HarmonyId = "com.omone.nuclearmeltdown";`, `const float DefaultRadiusMeters = 700f;`, `const int ExpiryYears = 50;`, `const string DecontaminationNameKeyword = "Water Treatment";`, `const string NuclearNameKeyword = "Nuclear";`, `const byte MaxPollution = 255;`, `const string LogPrefix = "[NuclearMeltdown] ";` and `static void Log(string msg)`.
  - `class Mod : IUserMod` with `string Name { get; }` and `string Description { get; }`.

- [ ] **Step 1: create the csproj** - net35, old-style, with a PackageReference.

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
Note that using a PackageReference in an old-style csproj is why `build.ps1` calls `msbuild -restore`, as in Step 5. The `CitiesHarmony.API` version can be moved to the latest 2.x during the restore.

- [ ] **Step 2: create AssemblyInfo, ModConfig and Mod.**

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
    /// <summary>The mod's entry point: the IUserMod implementation plus applying and removing the Harmony patches.</summary>
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
Note that `Patcher` is created in Task 6, so the reference to it in `Mod.cs` would not resolve yet. **Leave the bodies of `OnEnabled` and `OnDisabled` empty or commented out at this step** and enable them once Task 6 is done. To keep the build green, use this interim `Mod.cs` for now:
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

- [ ] **Step 3: create the solution file.**

`NuclearMeltdown.sln`, kept minimal: MSBuild builds the mod alone, and the tests are run separately through dotnet.
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

- [ ] **Step 4: create build.ps1** - MSBuild with a restore, then deploy into the mod folder.

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

- [ ] **Step 5: verify the build compiles.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds, `NuclearMeltdown.dll` is produced and copied into the mod folder. On an error, fix the reference paths or the PackageReference restore before going on.

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/NuclearMeltdown.csproj src/NuclearMeltdown/Properties src/NuclearMeltdown/Game/ModConfig.cs src/NuclearMeltdown/Game/Mod.cs NuclearMeltdown.sln build.ps1
git commit -m "feat: add the mod project skeleton and the build and deploy script"
```

---

## Task 6: NuclearDetector and the Harmony patch (detecting the destruction)

**Files:**
- Create: `src/NuclearMeltdown/Game/NuclearDetector.cs`
- Create: `src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`
- Create: `src/NuclearMeltdown/Game/Patcher.cs`
- Modify `src/NuclearMeltdown/Game/Mod.cs`, replacing the interim version with the real one.

**Interfaces:**
- Consumes `ModConfig` from Task 5 and `MeltdownEffect.Trigger`, which Task 8 implements; until then the patch calls a `ModConfig.Log` stub.
- Produces:
  - `static class NuclearDetector` with `bool IsNuclearPlant(ushort buildingID)`: true when `BuildingManager.instance.m_buildings.m_buffer[id].Info.m_buildingAI is PowerPlantAI` and `Info.name` contains `ModConfig.NuclearNameKeyword`.
  - `static class Patcher` with `void PatchAll()` and `void UnpatchAll()`, calling Harmony's `PatchAll` and `UnpatchAll` under `ModConfig.HarmonyId`.
  - `static class CollapseBuildingPatch`, where Harmony applies a Prefix and a Postfix to `CommonBuildingAI.CollapseBuilding`.

- [ ] **Step 1: implement NuclearDetector.**

`src/NuclearMeltdown/Game/NuclearDetector.cs`:
```csharp
namespace NuclearMeltdown.Game
{
    /// <summary>Decides whether a building is a nuclear power plant.</summary>
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
Note that `BuildingManager` and `PowerPlantAI` live in the global namespace, inside Assembly-CSharp, so no `using` is needed.

- [ ] **Step 2: implement Patcher.**

`src/NuclearMeltdown/Game/Patcher.cs`:
```csharp
using HarmonyLib;

namespace NuclearMeltdown.Game
{
    /// <summary>Applies and removes the Harmony patches.</summary>
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

- [ ] **Step 3: implement CollapseBuildingPatch** - the Prefix records whether it had already collapsed, and the Postfix fires only on the first collapse.

`src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`:
```csharp
using HarmonyLib;
using UnityEngine;

namespace NuclearMeltdown.Game.Patches
{
    /// <summary>
    /// Patches CommonBuildingAI.CollapseBuilding to notice a nuclear plant collapsing for the
    /// first time, whether it burned down or a disaster took it.
    /// The Prefix records in __state whether it had already collapsed, and the Postfix fires
    /// only on the first transition.
    /// </summary>
    [HarmonyPatch(typeof(CommonBuildingAI), "CollapseBuilding")]
    public static class CollapseBuildingPatch
    {
        // The real signature:
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
                if (testOnly) return;          // a test-only call, so ignore it
                if (__state) return;           // it had already collapsed, from a demolition or the like
                if (!__result) return;         // nothing actually changed
                if ((data.m_flags & Building.Flags.Collapsed) == Building.Flags.None) return;
                if (!NuclearDetector.IsNuclearPlant(buildingID)) return;

                Vector3 pos = data.m_position;
                // Replaced by MeltdownEffect.Trigger(pos) in Task 8
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
Note that `CommonBuildingAI`, `Building` and `InstanceManager` live in the global namespace, and Harmony injects `__state` and `__result` by name.

- [ ] **Step 4: replace Mod.cs with the real version.**

`src/NuclearMeltdown/Game/Mod.cs`, replacing the interim version from Task 5 Step 2:
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

- [ ] **Step 5: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds, with `CommonBuildingAI`, `Building.Flags` and `HarmonyLib` all resolving. HarmonyLib comes in as a dependency of CitiesHarmony.API during the restore.

- [ ] **Step 6: commit.**

```bash
git add src/NuclearMeltdown/Game/NuclearDetector.cs src/NuclearMeltdown/Game/Patcher.cs src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs src/NuclearMeltdown/Game/Mod.cs
git commit -m "feat: add the nuclear plant test and the CollapseBuilding patch that detects destruction"
```

---

## Task 7: PollutionField (reading and writing NaturalResourceManager) and ContaminationManager

**Files:**
- Create: `src/NuclearMeltdown/Game/PollutionField.cs`
- Create: `src/NuclearMeltdown/Game/ContaminationManager.cs`

**Interfaces:**
- Consumes `PollutionGrid`, `CellDose` and `ContaminationZone` from Core, plus `ModConfig`
- Produces:
  - `static class PollutionField`:
    - `void ApplyDose(CellDose dose)` - raises the cell's `m_pollution` to `Max(current, dose.Intensity)`.
    - `void ClearCell(int index)` - sets `m_pollution` to 0.
    - `void Refresh(int minX, int minZ, int maxX, int maxZ)` - calls `NaturalResourceManager.instance.AreaModifiedB(...)`.
    - `byte GetPollution(int index)`.
  - `static class ContaminationManager`:
    - `List<ContaminationZone> Zones { get; }` - a snapshot for reading.
    - `void ReplaceAll(List<ContaminationZone> zones)` - used when restoring a save; rewrites all of the contamination.
    - `void AddZone(ContaminationZone zone)` - adds it to the ledger and applies the initial contamination.
    - `void RemoveZoneAt(int index)` - removes it from the ledger without clearing the contamination, leaving that to decontamination or the natural decay. On expiry the caller calls ClearZone first.
    - `void ReassertZone(ContaminationZone zone)` - runs `ApplyDose` over the cells in the radius again, countering the natural decay.
    - `void ClearZone(ContaminationZone zone)` - zeroes the cells in the radius and refreshes.
    - `void DecontaminateAround(float worldX, float worldZ, float radiusMeters, int step)` - lowers `m_pollution` by `step` across the given area and refreshes it.
  - Internally it works out the minimum and maximum cells so it can refresh a zone's bounding rectangle.

- [ ] **Step 1: implement PollutionField.**

`src/NuclearMeltdown/Game/PollutionField.cs`:
```csharp
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>Read/write wrapper around NaturalResourceManager's ground pollution cells.</summary>
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

        /// <summary>Refreshes the pollution texture over the given cellX/cellZ range.</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
```
Note that `m_naturalResources` is an array of structs, so `arr[i].m_pollution = x` assigns in place and works.

- [ ] **Step 2: implement ContaminationManager.**

`src/NuclearMeltdown/Game/ContaminationManager.cs`:
```csharp
using System.Collections.Generic;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game
{
    /// <summary>The ledger of contamination zones, and applying, holding and clearing them on the grid.</summary>
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
Note that `NaturalResourceManager` lives in the global namespace.

- [ ] **Step 3: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: commit.**

```bash
git add src/NuclearMeltdown/Game/PollutionField.cs src/NuclearMeltdown/Game/ContaminationManager.cs
git commit -m "feat: add PollutionField for writing the pollution grid and ContaminationManager for the zone ledger"
```

---

## Task 8: MeltdownEffect (the explosion and the initial contamination) and wiring it into the patch

**Files:**
- Create: `src/NuclearMeltdown/Game/MeltdownEffect.cs`
- Modify `src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`, replacing the stub with `MeltdownEffect.Trigger`.

**Interfaces:**
- Consumes: `ContaminationManager`, `ModConfig`, `SimulationManager`, `EffectManager`, `PrefabCollection<DisasterInfo>`
- Produces:
  - `static class MeltdownEffect`:
    - `void Trigger(Vector3 position)` - plays the explosion effect if one can be obtained, then registers the zone through `ContaminationManager.AddZone` with the start time taken from `SimulationManager.instance.m_currentGameTime.Ticks`.
    - `EffectInfo ResolveExplosionEffect()` - searches the loaded `MeteorAI.m_impactEffect`, returning null if there is none.

- [ ] **Step 1: implement MeltdownEffect.**

`src/NuclearMeltdown/Game/MeltdownEffect.cs`:
```csharp
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game
{
    /// <summary>The explosion effect and the contamination zone raised on a collapse.</summary>
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
Note that `SimulationManager`, `EffectManager`, `EffectInfo`, `InstanceID`, `VehicleManager`, `Singleton<>`, `PrefabCollection<>`, `DisasterInfo` and `MeteorAI` all live in the global namespace. `DisasterInfo`'s AI field is `m_disasterAI`; if that has not been confirmed by decompiling, check the field name with `ilspycmd Assembly-CSharp.dll -t DisasterInfo` before committing to it.

- [ ] **Step 2: replace the stub in the patch with the real call.**

In the Postfix of `src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs`, replace this line:
```csharp
                Vector3 pos = data.m_position;
                // Replaced by MeltdownEffect.Trigger(pos) in Task 8
                ModConfig.Log("Nuclear plant collapsed at " + pos + " (effect stub)");
```
with:
```csharp
                Vector3 pos = data.m_position;
                MeltdownEffect.Trigger(pos);
```

- [ ] **Step 3: confirm the name of `DisasterInfo`'s AI field.**

Run:
```bash
ilspycmd "/c/Program Files (x86)/Steam/steamapps/common/Cities_Skylines/Cities_Data/Managed/Assembly-CSharp.dll" -t DisasterInfo -o /tmp/dinfo && grep -nE "DisasterAI|m_disasterAI|public .*AI " /tmp/dinfo/DisasterInfo.decompiled.cs
```
Expected: confirmation that the field is `m_disasterAI`. If it is not, correct `info.m_disasterAI` in Step 1 to the real name.

- [ ] **Step 4: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Game/MeltdownEffect.cs src/NuclearMeltdown/Game/Patches/CollapseBuildingPatch.cs
git commit -m "feat: wire MeltdownEffect - the explosion and the contamination zone - into the patch"
```

---

## Task 9: MeltdownThreadingExtension (upkeep, expiry and decontamination each tick)

**Files:**
- Create: `src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs`

**Interfaces:**
- Consumes: `ContaminationManager`, `MeltdownClock`, `ModConfig`, `SimulationManager`, `BuildingManager`
- Produces:
  - `class MeltdownThreadingExtension : ThreadingExtensionBase`, overriding `OnAfterSimulationTick()`. The game finds and drives it on its own.
    - Processes every zone at a fixed interval, for instance once every 16 ticks by an internal counter:
      1. Expired, by `MeltdownClock.HasExpired`: `ClearZone` and remove it from the ledger.
      2. A decontamination facility operating nearby: reduce it gradually, as `ReducePollution` does, and remove the zone once it is all gone.
      3. Otherwise: `ReassertZone` to hold it in place.

- [ ] **Step 1: implement MeltdownThreadingExtension.**

`src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs`:
```csharp
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;
using UnityEngine;

namespace NuclearMeltdown.Game.Simulation
{
    /// <summary>
    /// Maintains the contamination zones every tick and releases them after 50 years or once a
    /// decontamination facility clears them.
    /// The game discovers and drives any IThreadingExtension in a mod assembly on its own.
    /// </summary>
    public class MeltdownThreadingExtension : ThreadingExtensionBase
    {
        private int _tickCounter;
        private const int ProcessInterval = 16; // process every 16 ticks to keep the cost down

        public override void OnAfterSimulationTick()
        {
            try
            {
                if (++_tickCounter < ProcessInterval) return;
                _tickCounter = 0;

                List<ContaminationZone> zones = ContaminationManager.Zones; // snapshot
                if (zones.Count == 0) return;

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;

                // Walk backwards so removing by index stays valid.
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

                    ContaminationManager.ReassertZone(zone); // hold it against the natural decay
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("threading error: " + e);
            }
        }

        /// <summary>Whether a decontamination building - a water treatment plant by default - is operating near the centre of the zone.</summary>
        private bool IsDecontaminationActive(ContaminationZone zone)
        {
            var bm = BuildingManager.instance;
            ushort[] grid = bm.m_buildingGrid;
            // Scan the building grid cells around the zone's centre, plus or minus one.
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
                PollutionField.ReducePollution(doses[i].Index, 8); // removed gradually
                if (PollutionField.GetPollution(doses[i].Index) > 0) anyRemaining = true;
            }
            // Refresh the texture
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
Notes:
- `RefreshZoneTexturePublic` needs Task 7's private `RefreshZoneTexture` made public. Step 2 below does that, exposing `RefreshZoneTexture(ContaminationZone)` on `ContaminationManager` by making the private version public.
- The building grid constants - `/64f + 135f` and a resolution of 270 - are measured from `BuildingManager` in Assembly-CSharp, and are confirmed in Step 3.

- [ ] **Step 2: make ContaminationManager's Refresh public.**

Change `private static void RefreshZoneTexture(...)` in `src/NuclearMeltdown/Game/ContaminationManager.cs` to the following, making it public and settling on one name:
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
Then change `ContaminationManager.RefreshZoneTexturePublic(zone)` in `MeltdownThreadingExtension` to `ContaminationManager.RefreshZoneTexture(zone)`.

- [ ] **Step 3: confirm BuildingManager's grid constants.**

Run:
```bash
ilspycmd "/c/Program Files (x86)/Steam/steamapps/common/Cities_Skylines/Cities_Data/Managed/Assembly-CSharp.dll" -t BuildingManager -o /tmp/bm && grep -nE "m_buildingGrid|/ 64f|\* 270|m_nextGridBuilding|BUILDINGGRID_RESOLUTION" /tmp/bm/BuildingManager.decompiled.cs | head
```
Expected: confirmation of the 270 grid resolution, the 64 m cells and `m_nextGridBuilding`. If they differ, correct the constants in `IsDecontaminationActive` to the measured values.

- [ ] **Step 4: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 5: commit.**

```bash
git add src/NuclearMeltdown/Game/Simulation/MeltdownThreadingExtension.cs src/NuclearMeltdown/Game/ContaminationManager.cs
git commit -m "feat: add the per-tick contamination upkeep, the 50-year expiry and the decontamination"
```

---

## Task 10: ContaminationDataExtension (persisting across save and load)

**Files:**
- Create: `src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs`

**Interfaces:**
- Consumes: `ContaminationManager`, `ZoneSerializer`, `ModConfig`
- Produces:
  - `class ContaminationDataExtension : SerializableDataExtensionBase`, overriding `OnSaveData()` and `OnLoadData()` under the data key `"NuclearMeltdown.Contamination.v1"`. The game finds it on its own.

- [ ] **Step 1: implement ContaminationDataExtension.**

`src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs`:
```csharp
using System.Collections.Generic;
using ICities;
using NuclearMeltdown.Core;

namespace NuclearMeltdown.Game.Serialization
{
    /// <summary>Persists the contamination zone ledger into the save game. Discovered by the game.</summary>
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
Note that `serializableDataManager` is a protected property of `SerializableDataExtensionBase`, of type `ISerializableData`.

- [ ] **Step 2: verify the build.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 3: commit.**

```bash
git add src/NuclearMeltdown/Game/Serialization/ContaminationDataExtension.cs
git commit -m "feat: persist the contamination zones across save and load"
```

---

## Task 11: the README and the in-game verification guide

**Files:**
- Create: `src/NuclearMeltdown/README.md`

**Interfaces:**
- Consumes: nothing
- Produces nothing but documentation

- [ ] **Step 1: write the README.**

`src/NuclearMeltdown/README.md`:
```markdown
# Nuclear Meltdown (Cities: Skylines Mod)

When a nuclear power plant burns down or collapses, it sets off a meteor-style explosion and
spreads wide-area radioactive contamination, modelled as ground pollution. The contamination
lifts after 50 in-game years, or once a decontamination facility - a Water Treatment Plant by
default - has been operating nearby.

## Dependencies
- Harmony (a mod dependency) - subscribe to CitiesHarmony on the Steam Workshop.

## Building and deploying
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
It deploys to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`.

## Verifying it in game
1. Start the game, open the Content Manager, and enable "Nuclear Meltdown" under Mods.
2. Check that Harmony is active - the log should show `[NuclearMeltdown] Harmony patches applied`.
3. Place a nuclear power plant and destroy it, either with a disaster such as a meteor or a
   tornado, or by fire.
4. Confirm that an explosion appears where it collapsed and that roughly 700 m around it turns
   the pollution colour.
5. Operate a water treatment plant near the contaminated zone and confirm the contamination
   fades away.
6. Let 50 in-game years pass and confirm the contamination lifts on its own.
7. Save and reload, and confirm the contamination is still there.

## Settings
The constants live in `Game/ModConfig.cs`: the contamination radius, the number of years before
it lifts, the decontamination facility keyword and so on.

## Logs
Search for `[NuclearMeltdown]` in the output log under
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\`.
```

- [ ] **Step 2: commit.**

```bash
git add src/NuclearMeltdown/README.md
git commit -m "docs: add the README and the verification guide"
```

---

## Task 12: the final build, the full test run, and asking for in-game verification

**Files:** none; this is verification only

- [ ] **Step 1: run every Core test.**

Run: `dotnet test tests/NuclearMeltdown.Core.Tests`
Expected: every test passes.

- [ ] **Step 2: the final build and deployment of the mod.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds and both `NuclearMeltdown.dll` and `CitiesHarmony.API.dll` are deployed into the mod folder.

- [ ] **Step 3: ask the user to verify it in game.**

Ask the user to work through steps 1 to 7 of the README's in-game verification, since the game cannot be launched from here. If anything goes wrong, ask for the `[NuclearMeltdown]` lines from the output log.

- [ ] **Step 4: the final commit.**

```bash
git add -A
git commit -m "chore: final build and test verification"
```

---

## Self-Review

**1. Spec coverage, against the design document:**
- Identifying a nuclear plant, by PowerPlantAI plus the prefab name: Task 6, `NuclearDetector` ✅
- The trigger, detecting a burn-down or collapse through the CollapseBuilding Postfix: Task 6, `CollapseBuildingPatch` ✅
- The explosion effect, borrowed from the meteor: Task 8, `MeltdownEffect` ✅
- The ground pollution in NaturalResourceManager, falling off from the centre to 700 m: Task 2 `PollutionGrid` plus Task 7 `PollutionField` and `ContaminationManager` ✅
- Holding the contamination against the natural decay by reasserting it: Task 9, `MeltdownThreadingExtension` ✅
- Lifting after 50 years: Task 3 `MeltdownClock` plus Task 9 ✅
- Clearing through a decontamination facility, reusing the existing water treatment plant: Task 9, `IsDecontaminationActive` and `DecontaminateZone` ✅
- Persisting across save and load: Task 4 `ZoneSerializer` plus Task 10 `ContaminationDataExtension` ✅
- IUserMod, showing the Name and Description: Task 5, `Mod` ✅
- CitiesHarmony (Harmony 2.0): Tasks 5 and 6 ✅
- Error handling - try/catch, and an empty ledger on corrupt data: every patch, tick and serialisation path ✅
- Building and deploying: Task 5's `build.ps1` and Task 12 ✅

**2. Placeholder scan:** no vague wording such as "TBD", "later" or "as appropriate". Every code step gives the real code. The decompilation checks in Task 8 Step 3 and Task 9 Step 3 are not gaps to be filled but a final confirmation of real names, with the defaults already stated.

**3. Type consistency:**
- `CellDose(int, byte)` and `ContaminationZone(float,float,float,long)` - defined in Task 1 and used consistently in Tasks 2, 4, 7 and 9.
- `PollutionGrid.CellsInRadius(float,float,float,byte)` - defined in Task 2 and used consistently in Tasks 7 and 9.
- `MeltdownClock.HasExpired(long,long,int)` - defined in Task 3 and used consistently in Task 9.
- `ZoneSerializer.Serialize` and `Deserialize` - defined in Task 4 and used consistently in Task 10.
- `ContaminationManager.RefreshZoneTexture(ContaminationZone)` - made public in Task 9 Step 2, settling on one name and removing the mixed use of `RefreshZoneTexturePublic`.
- `MeltdownEffect.Trigger(Vector3)` - defined in Task 8 and called from the patch in Task 8; consistent.
```
