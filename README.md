# MIMESIS FOV Mod

A BepInEx plugin that adds a **Field of View (FOV)** setting to *MIMESIS*.
It injects a separate **FOV** slider into the game's own Settings screen and applies your chosen FOV
to the first-person player camera.

- Game: **MIMESIS** — Unity **6000.3.9f1**, Mono backend, Cinemachine 3
- Loader: **BepInEx 5.4.23.5** (Mono)
- Patcher: **HarmonyX** (bundled with BepInEx)

## Demo

<video src="https://raw.githubusercontent.com/dsfadd/Mimesis-fov-mod/main/docs/media/demo.mp4" controls width="720">
  Your browser does not support inline video —
  <a href="docs/media/demo.mp4">download the demo</a>.
</video>

## Installation

### Manual

1. Download **BepInEx 5.4.23.5 (Mono, x64)** from the
   [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) and
   unpack the archive directly into the game's root folder, next to `MIMESIS.exe`.
2. Launch the game once, then close it. (BepInEx only finishes installing
   itself — hooking its loader and creating `BepInEx\plugins`,
   `BepInEx\config`, `BepInEx\core` — the first time the game actually runs;
   it can't host a plugin before that.)
3. Build the project:
   ```powershell
   dotnet build src\MimesisFovMod\MimesisFovMod.csproj -c Release -p:GameDir="C:\path\to\MIMESIS"
   ```
   This produces `src\MimesisFovMod\bin\Release\MimesisFovMod.dll`.
   
   `GameDir` should point to your MIMESIS install. If omitted, it defaults to
   `C:\Program Files (x86)\Steam\steamapps\common\MIMESIS`.
   
4. Copy `MimesisFovMod.dll` into `MIMESIS\BepInEx\plugins\`.

## Configure

Edit `MIMESIS\BepInEx\config\com.dsfadd.mimesis.fovmod.cfg` (created on first run):

```ini
[General]
FieldOfView = 60      # vertical FOV in degrees (30–120)

[UI]
SliderMin = 60        # in-game slider lower bound
SliderMax = 100       # in-game slider upper bound
```

Changing the value from the in-game slider or the config file applies live.

## How it works

The player view is a `Unity.Cinemachine.CinemachineCamera` created by
`CameraManager` from a prefab; its `Lens.FieldOfView` (default **60**) drives
`Camera.main` through the CinemachineBrain. The plugin:

| Piece | Target | What it does |
|---|---|---|
| `CameraFov` | `CameraManager.SetupPlayerCamera` (postfix) | Re-applies FOV whenever the player camera is (re)created — spawn / respawn. |
| `CameraFov.Tick` | `LateUpdate` | Cheap per-frame re-assert. **Only** in `CameraMode.Normal` and **never** while `IsZoomToTargetActive`, so it doesn't fight the game's zoom / spectate / death-cam animations. |
| `SettingsUiInjector` | `UIPrefab_GameSettings.OnEnable` (postfix) | Clones the *Look Sensitivity* row, relabels it **FOV** and binds its slider to the config. |

Two details that made the UI part fiddly (handled in the code):

- **Localization.** The row title carries a `UIApplyL10N` component that rewrites
  the text to the translated *"Look Sensitivity"* after we run. The injector
  removes localizer components from the cloned title and also re-asserts the
  `"FOV"` text every `LateUpdate` as a fallback.
- **Layout.** The `GamePlay` container positions rows absolutely (no
  `LayoutGroup`), so the clone is manually offset down by one row so it doesn't
  overlap the next setting.

The FOV value is stored in the BepInEx config and persists between sessions.

## License

[MIT](LICENSE) © 2026 [dsfadd](https://github.com/dsfadd)
