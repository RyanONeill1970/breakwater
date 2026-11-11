# Third-party assets

Everything else in Breakwater is original. The game shows a short version of this list on the settings screen (`Scripts/Credits.cs`).

## Vehicle models: CC BY 4.0

[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) allows commercial use as long as the author is credited. Each model's folder keeps its original `license.txt`. The models were changed for the game: textures were scaled down to 1K (and converted to JPEG where they carry no transparency). At run time each model is turned, scaled, cut into hull and turret, and repainted where noted in `Scripts/VehicleAssets.cs`.

| Folder | Work | Author | Used for |
|---|---|---|---|
| `models/abrams` | [Low Poly Abrams Free Model](https://sketchfab.com/3d-models/low-poly-abrams-free-model-ee925340fd0249bfac6f766c7eb81abd) | [Lasha-Georgy Bajadze](https://sketchfab.com/gsr19891) | M1 Abrams, Modern main tank |
| `models/bradley` | [Low Poly M2 Bradley Free Model](https://sketchfab.com/3d-models/low-poly-m2-bradley-free-model-f5fbaf2aa97641b082e8d2acc91092a3) | [Lasha-Georgy Bajadze](https://sketchfab.com/gsr19891) | M2 Bradley, Modern scout |
| `models/m113` | [M113A1](https://sketchfab.com/3d-models/m113a1-b544214a78b64b47999022f50701cd3c) | [Metehan](https://sketchfab.com/MenOfMena) | M113 and M901 ITV |
| `models/t72` | [T-72B1](https://sketchfab.com/3d-models/t-72b1-f1eeb31041f947669cedc9d6a26316d0) | [42manako](https://sketchfab.com/42manako) | T-72, Modern defender |
| `models/t62` | [T-62MD](https://sketchfab.com/3d-models/t-62md-73a22c7a0e344ff080f1e9d3945c6eb5) | [42manako](https://sketchfab.com/42manako) | T-62 |
| `models/bmp2` | [BMP-2 (low-poly)](https://sketchfab.com/3d-models/bmp-2-low-poly-312fa325c66e418395feab2d1d619a58) | [ScurvyWoof](https://sketchfab.com/ScurvyWoof) | BMP-2 |
| `models/btr60` | [SPW-60PB (East Germany)](https://sketchfab.com/3d-models/spw-60pb-east-germany-bda671c3c1584c499c12216c4d78fb0d) | [42manako](https://sketchfab.com/42manako) | BTR-60 |
| `models/m35` | [M35 Truck](https://sketchfab.com/3d-models/m35-truck-bbf515b01f7b4587a3f4531d1b871ce7) | [ZeroFrost](https://sketchfab.com/zerofrost) | Supply truck |

## Skies and surface textures: CC0

From [Poly Haven](https://polyhaven.com), public domain ([CC0](https://creativecommons.org/publicdomain/zero/1.0/)). No credit is required.

- `sky/`: [kloofendal_48d_partly_cloudy_puresky](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky) (day) and [qwantani_night_puresky](https://polyhaven.com/a/qwantani_night_puresky) (night), 2K HDR.
- `textures/`: [leafy_grass](https://polyhaven.com/a/leafy_grass), [coast_sand_01](https://polyhaven.com/a/coast_sand_01), [forest_ground_04](https://polyhaven.com/a/forest_ground_04), [rock_face](https://polyhaven.com/a/rock_face), [asphalt_02](https://polyhaven.com/a/asphalt_02), [white_stucco](https://polyhaven.com/a/white_stucco), [dirty_concrete](https://polyhaven.com/a/dirty_concrete), [bark_brown_02](https://polyhaven.com/a/bark_brown_02) and [palm_tree_bark](https://polyhaven.com/a/palm_tree_bark), 1K JPEG.

## Island and harbour scenery: CC0

The shared campaign and `ArtStudy.tscn` asset library contains the following [Poly Haven CC0 assets](https://polyhaven.com/license), downloaded as glTF. The original file manifests are retained alongside the assets; `Tools/FetchArtStudyAssets.ps1` verifies their published checksums. Scene instances are rescaled and rotated; Godot generates render LODs at import. The campaign uses the boulder, island-tree and fern scans, together with original palm, grass and harbour geometry and shaders. The longer cliff scans remain in the standalone study.

- `art-study/rock_07`: [Rock 07](https://polyhaven.com/a/rock_07), 1K; retained from the first study.
- `art-study/rock_09`: [Rock 09](https://polyhaven.com/a/rock_09), 1K; retained from the first study.
- `art-study/island_tree_01`: [Island Tree 01](https://polyhaven.com/a/island_tree_01), 1K.
- `art-study/wild_rooibos_bush`: [Wild Rooibos Bush](https://polyhaven.com/a/wild_rooibos_bush), 1K.
- `art-study/coastal_cliff_01`: [Coastal Cliff 01](https://polyhaven.com/a/coastal_cliff_01), 2K.
- `art-study/rock_face_02`: [Rock Face 02](https://polyhaven.com/a/rock_face_02), 2K.
- `art-study/boulder_01`: [Boulder 01](https://polyhaven.com/a/boulder_01), 2K.
- `art-study/fern_02`: [Fern 02](https://polyhaven.com/a/fern_02), 2K, four plant variants.

The palm and grass geometry, terrain, harbour structures and props, water shader and scene composition are original. The first study used the first two rock assets with a custom world-space material made from the existing CC0 rock-face texture. The current cliff, rock-face and boulder instances retain their photographed color, normal and roughness maps, with a shared color treatment, moss and wet surfaces. Foliage uses adapted cutout materials. Harbour surfaces combine the existing CC0 concrete texture with procedural weathering. The September 30 rock-and-surf pass reuses these assets without additional downloads. Existing vehicle models retain their credits above. The concept image in `Docs/research` is AI-generated reference art and is not used as a game texture or backdrop.
