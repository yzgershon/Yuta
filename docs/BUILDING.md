# Building and testing Yuta

## Requirements

| Dependency | Purpose |
| --- | --- |
| Windows 11 | Development and validation platform. The project targets Windows APIs; it is not a cross-platform desktop app. |
| .NET 8 SDK | Compile the C# Windows host and publish portable builds. |
| WebView2 Evergreen Runtime | Render the local menu inside the desktop app. |
| Node.js 24 + npm | Run browser/core tests and regenerate documentation screenshots. Not required by the desktop app. |
| Microsoft Edge | Browser used by the Playwright configuration. |

WebView2 is distinct from the Edge browser. Microsoft documents its runtime requirements in [Distribute your app and the WebView2 Runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

## Portable builds

Run from a PowerShell terminal at the repository root:

```powershell
.\scripts\build.ps1 -Runtime win-x64
.\scripts\build.ps1 -Runtime win-arm64
```

Build the architectures sequentially; they share intermediate project output. The result is `release\yuta\<runtime>\Yuta.exe` with its supporting files. For an SDK outside PATH, pass `-Dotnet 'C:\path\to\dotnet.exe'`.

The build script uses a self-contained .NET publish. It does not install Yuta, register startup, replace another copy, or restart a running instance. Quit a copy running from the build's destination before rebuilding that destination. The root `Start Yuta.cmd` selects the matching built architecture.

## Tests

```powershell
npm.cmd ci
npm.cmd test
npm.cmd run test:ui

# Build first; run the architecture supported by this machine.
.\scripts\test-native.ps1 -Runtime win-x64
.\scripts\test-native.ps1 -Runtime win-x64 -Mode performance-test
.\scripts\test-native.ps1 -Runtime win-x64 -Mode smoke-test
```

On Windows on ARM, replace `win-x64` with `win-arm64`.

| Suite | Coverage |
| --- | --- |
| Core | Timer arithmetic, title handling, escaping, destination validation, calendar, and filtering. |
| Browser | Navigation, search, retained focus and drafts, media races, hotkeys, hover intent, responsive layout, and persistence through a simulated native bridge. |
| Native fixtures | Profile migration, DPAPI clipboard storage, hotkeys, hover timing, geometry, idle rendering, media state, visibility, and obstruction policy. |
| Native performance | Real window transitions, rapid reversals, fixed browser viewport, lifecycle, and cached integration reads. |
| Native smoke | Offscreen captures of seven panels, JavaScript errors, overflow, warm reopening, idle suspension, and cold resuming. |

The native test modes use isolated profiles. UI test modes disable mutating system actions; browser tests do not execute Windows power or media commands. `Yuta.exe --network-test` is separate and explicitly makes weather and speed-test requests. Result files are written beside the tested executable.

The [Windows checks workflow](../.github/workflows/ci.yml) runs core/browser tests, builds both architectures, and executes x64 native fixtures on a Windows runner. Hosted checks do not establish behavior across every graphics driver, display scaling setting, browser accessibility provider, or media player.

## Browser preview

```powershell
npm.cmd ci
npm.cmd run preview
```

Open `http://127.0.0.1:4179`. This is a UI preview with sample data. It cannot control Windows, read the system clipboard, change wallpapers, or perform power actions. Stop the server with Ctrl+C.

## Documentation captures

```powershell
npm.cmd run docs:capture
```

This renders `ui/` at 2x pixel density with a deterministic clock and fictional app, clipboard, weather, task, and media data. It writes `docs/images/home.png` and `docs/images/today.png`. It never opens a real user profile. See [Media](MEDIA.md) for generated product artwork.

## Troubleshooting

- **No window after starting:** look in the system tray. Yuta is single-instance; starting another copy brings the existing one forward.
- **WebView2 startup failure:** install the Evergreen Runtime and keep the entire portable directory together.
- **Hotkey conflict:** choose another combination in Settings. A failed registration leaves the previous shortcut intact.
- **Browser test cannot find Edge:** install Microsoft Edge, then rerun the test. The configuration explicitly selects `msedge`.
- **Clipboard is empty:** capture starts off and only collects new copies while Yuta runs. It does not import Windows clipboard history.
- **Missing music or seeking:** the music app must expose the corresponding Windows media-session capability.
