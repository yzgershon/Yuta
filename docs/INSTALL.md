# Install Yuta

Yuta 0.2.0 is a portable app for Windows 11. Download the ZIP for your computer from [GitHub Releases](https://github.com/yzgershon/Yuta/releases/latest):

| Computer | Download |
| --- | --- |
| Intel or AMD processor | [Windows x64](https://github.com/yzgershon/Yuta/releases/download/v0.2.0/Yuta-0.2.0-win-x64.zip) |
| Snapdragon or another Windows ARM processor | [Windows ARM64](https://github.com/yzgershon/Yuta/releases/download/v0.2.0/Yuta-0.2.0-win-arm64.zip) |

1. Right-click the downloaded ZIP and choose **Extract All**. Move the extracted `Yuta` folder somewhere you want to keep it.
2. Open that folder and run **Yuta.exe**. Keep its supporting files together; do not run it from inside the ZIP.
3. Click the notch, settle your pointer over it for 450 ms, or press **Alt + Space**. While media is playing, the first opening shows the small player; click its artwork or expand button to open Home.

To check your architecture, open **Settings > System > About > System type**. Use ARM64 for an ARM-based processor and x64 for an x64-based processor.

.NET is included. The app also needs the [Microsoft WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section). If Yuta reports that WebView2 is missing, install the Evergreen Runtime for your architecture and reopen Yuta. Node.js, Git, and the .NET SDK are not needed to run the download.

These builds are unsigned, so Windows may display a reputation warning. Download only from the official release and use its `SHA256SUMS.txt` to check the ZIP if needed:

```powershell
Get-FileHash .\Yuta-0.2.0-win-x64.zip -Algorithm SHA256
```

Yuta does not enable startup or clipboard recording automatically. Turn on clipboard capture in the app if wanted. Change the shortcut in Settings if Alt + Space is already used by another app. To quit, right-click the tray icon and select Exit.

## Updating

There is no automatic updater yet. Quit Yuta from the tray, extract the new release into a new folder, and run its `Yuta.exe`. Settings and saved data remain in `%LOCALAPPDATA%\Perch`, the compatibility profile directory used by earlier builds. Do not delete that directory to update the app.

## Removing Yuta

Quit from the tray and remove the extracted app folder. This leaves your profile intact. See the [privacy guide](https://github.com/yzgershon/Yuta/blob/main/docs/PRIVACY.md) for optional profile removal and storage details.
