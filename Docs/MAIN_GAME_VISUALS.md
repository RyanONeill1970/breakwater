# Playable island visuals

The approved art direction is integrated into the main game. Build C# and press **F5** to play any of the three islands in Modern or Classic mode. `ArtStudy.tscn` remains a separate demonstration scene.

The campaign shares the study's ground and water materials, scanned boulders and foliage, detailed palms, grass, harbour structures, warm daylight and muted vehicle finish. An irregular coast and shallow lagoon surround the existing mission maps. Classic roads, rivers and jungle still follow their terrain cells. Night missions retain moonlight and thermal gun sights.

Playable ground stays level. Scanned ridges fit the simulation's existing cover footprints and heights, and buildings retain their destruction behaviour. Harbour structures and large coastal decoration stand outside the navigable boundary. This preserves the existing routes and objectives; navigable hills would require further simulation work.

Shared components live in `IslandAssets`, `IslandHarbour`, `IslandGroundCover`, `ArtStudyPalm` and `ArtStudyBatch`. The campaign uses complete boulders for blocking ridges; the larger, open cliff scans remain in the standalone study. Asset sources and licences are in [the credits](../assets/CREDITS.md).

## Gameplay captures

These are actual main-game renders captured during integration on 30 September 2026:

- [Reef tactical view](main-game/modern-reef.png)
- [Breakwater tactical view](main-game/modern-breakwater.png)
- [Chase camera](main-game/chase.png)
- [Classic group windows](main-game/classic.png)
- [Night gun sight](main-game/night.png)
- [Signal chase camera](main-game/signal-chase.png)
- [Retro gun sight](main-game/retro.png)

## Performance and verification

Integration samples used Godot 4.7.2 .NET, Vulkan Forward+ and a Radeon RX 570. The requested 1600 × 1000 window rendered at 1777 × 1111 with host scaling. Captures discarded 90 warmup frames and measured 302 process intervals, including VSync.

| View | Median | 95th percentile |
| --- | ---: | ---: |
| Reef tactical | 16.67 ms | 16.67 ms |
| Breakwater tactical | 16.67 ms | 16.81 ms |
| Chase | 16.67 ms | 16.67 ms |
| Classic group windows | 16.67 ms | 16.75 ms |
| Night sight | 16.67 ms | 16.67 ms |
| Signal chase | 16.67 ms | 16.77 ms |

These are short local samples, approximately 60 FPS, not isolated GPU measurements or a guarantee across hardware and every battle. Smaller group cameras use reduced mesh detail, and the covered main camera skips world rendering. Ground cover is spatially batched. Foliage LODs can still become sparse at a distance.

`Tools/Check.ps1` includes simulation, gameplay, camera/UI, campaign, save/settings and scenery checks. The scenery check measures actual imported rock bounds and verifies level navigable ground on all three islands in both modes. View-state regression checks cover thermal filters and stale combat messages across camera changes, new campaigns, retries, checkpoint restores and island transitions. Graphical runs of that check save transition captures under `.godot/qa`.
