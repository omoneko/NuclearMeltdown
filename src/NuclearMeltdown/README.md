# Nuclear Meltdown (Cities: Skylines Mod)

When a nuclear power plant burns down or collapses, it sets off an explosion effect and spreads
radioactive contamination - modelled as ground pollution - over a wide area. The contamination
lifts after 50 in-game years, or sooner if a decontamination facility operates nearby.

## Dependencies
- Harmony (mod dependency) - subscribe to CitiesHarmony on the Steam Workshop.

## Building and deploying
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
The result is deployed to
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`.

## Checking it works in game
1. Start the game, open the content manager, and enable "Nuclear Meltdown" under Mods.
2. Confirm Harmony is active - the log should contain
   `[NuclearMeltdown] Harmony patches applied`.
3. Build a nuclear power plant and have it burn down or collapse, through a disaster (a meteor,
   a tornado) or a fire.
4. Check that an explosion appears where it collapsed and that the area around it becomes
   contaminated.
5. Operate a decontamination facility near the contaminated zone and check that the
   contamination gradually clears. A building whose name contains "Decontamination" counts; a
   water treatment plant does **not**.
6. Let 50 in-game years pass and check that the contamination lifts on its own.
7. Save and reload, and check that the contamination survives.

## Settings
The constants live in `Game/ModConfig.cs`: the contamination radius, how long it lasts, the
keyword identifying a decontamination facility, and so on.
The options screen additionally covers how the scale of the disaster is decided (a random draw,
the plant's power output, or a fixed value) and whether the explosion and the fallout happen at
all.

## Logs
Search `Cities_Data\output_log.txt`, inside the game's installation folder, for
`[NuclearMeltdown]`.
