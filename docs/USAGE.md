# Using Yuta

## Open and close

Click the notch, press **Alt + Space**, or rest the pointer over it for 450 ms. Hover opening also requires the pointer to settle for 250 ms within a small tolerance. Passing over it or dragging does not open it.

Moving the pointer away retracts the menu. Esc, the shortcut, and the collapse button also close it. A keyboard-opened menu stays open until the pointer has entered; dialogs, context menus, and active drags pause automatic retraction. Turn off deliberate hover in Settings for click-and-keyboard operation.

## Home

Home presents three clipboard items, prioritizing pins and recency. Search filters the full captured history, not just those three rows. Copy buttons copy immediately; clicking an item opens its full preview. **History** opens the complete list with type filters, pinning, paste, removal, and multiselect.

The right column combines the music player and six app shortcuts. Favorites come first, followed by recent and preferred apps. **All apps** opens the complete launcher; **Ctrl + K** also takes you there. Add files, folders, websites, or text snippets from the launcher.

Connection icons open the relevant Windows Settings page. Power buttons send lock, sleep, restart, or shutdown directly without a Yuta confirmation dialog. Restart and shutdown do not force-close applications, and Windows or an application may still present its own save or policy prompts.

## Idle activity

The notch shows local time by default. Music adds artwork, a shortened title, and animated playback bars. These bars indicate playback; they are not an audio-frequency visualizer. A running focus timer retains its place while music is playing. Reduced-motion preferences are respected.

Connecting power or changing the default audio output produces a brief status notice. Headphones appear when they become the audio output. General Bluetooth pairing, accessory battery levels, hotspot, and call activity are not integrated.

Smart flattening checks whether supported interactive controls overlap the normal idle footprint. Confirmed overlap reduces the actual window to a four-DIP black strip. It restores the normal notch after the overlap clears. This depends on Windows accessibility providers and can be disabled in Settings. Click, deliberate hover, and the shortcut remain available.

## Other sections

- **Today:** local calendar events, tasks, city weather, and a focus timer with presets or a custom duration. Calendar events are not synced to an account.
- **Stats:** CPU/GPU/RAM, top processes, disk, network activity, uptime, and an optional speed estimate. Some devices do not expose every counter.
- **Wallpaper:** choose a local folder, browse images, and apply a wallpaper. Recent selections are retained.
- **Routines:** open a named group of up to 12 apps, files, folders, or HTTP(S) websites. Arbitrary shell commands are not accepted.
- **Quick note:** an automatically saved local scratchpad.

## Personalize

Settings offers six accent choices, attached-notch or floating-pill shape, monitor selection, hover opening, smart flattening, city, temperature unit, clipboard capture, and a shortcut recorder. Click the current shortcut and press a new combination; Esc cancels. Violet (`#9b80ff`) is the initial accent.

Right-click the notch, expanded menu, or tray icon to hide it in the current app, hide it on a detected website, or pause for one hour. Website rules use a confirmed address-bar hostname and include subdomains. If a browser does not expose its address bar, the website action is unavailable. The tray offers **Resume now** and a list for removing exclusions. The saved shortcut can temporarily open Yuta during a pause or exclusion.

## Keep your data

The profile remains in `%LOCALAPPDATA%\Perch` for compatibility with early versions. This path is intentional. Read [Privacy and storage](PRIVACY.md) before sharing logs, profiles, or screenshots.
