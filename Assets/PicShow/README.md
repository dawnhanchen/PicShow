# Four Realms Gallery

Open `Assets/Scenes/SampleScene.unity` and enter Play Mode with the Game View visible.

- Four categories contain five matching works each.
- Click the top bar to change style while keeping the current work.
- Click a side card, an arrow, or a position marker to navigate.
- Left and right keyboard arrows also navigate.
- The scene includes an XR UI input module and PICO controller interactors.
- The three card objects are reused throughout animation.

The editable prefab is `Assets/PicShow/Prefabs/FourRealmsGallery.prefab`.
Content and accent colors are stored in `Assets/PicShow/Data/GalleryCatalog.asset`.
The gallery root uses a World Space Canvas at 0.001 scale, initially two meters
in front of the scene camera. Position and rotate the root to place it in a room.

`PicShow/Create Four Realms Gallery` creates a gallery in a scene without one.
The menu preserves an existing gallery instead of replacing it.
The static Chinese font atlas includes all shipped interface text and titles.
Rebuild the font atlas when adding characters not already present.

Run `PicShow.Editor.Tests` through Unity Test Runner for state, content, font,
and prefab validation. `GalleryPlayVerification.Run(outputPath)` verifies screen
raycasts and UI events in Play Mode with the Game View visible.
PICO device input and performance still require on-device validation.

All original JPEG files are imported without modifying their source files.
Android textures use ASTC 6x6. The Noto Sans SC license is in `Fonts/OFL.txt`;
TMP Essential Resources and their licenses are bundled under `TextMesh Pro`.
