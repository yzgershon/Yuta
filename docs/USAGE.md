# Using Yuta

## Open and close

Click the notch, press **Alt + Space**, or rest the pointer over it for 450 ms. Hover opening also requires the pointer to settle for 250 ms within a small tolerance. Passing over it or dragging does not open it.

While a Windows media session is playing, the first opening shows the compact player. Click its artwork, background, or expand icon, or press Alt + Space again, to open the full menu. Playback and seek controls stay in the compact view. Paused or unavailable media opens Home directly.

Moving the pointer away retracts the menu. Esc and the collapse button close it; the shortcut closes the full menu. A keyboard-opened menu stays open until the pointer has entered; dialogs, context menus, and active drags pause automatic retraction. Turn off deliberate hover in Settings for click-and-keyboard operation.

## Home

Home scrolls through captured clipboard history, prioritizing existing pins and recency. Images appear as thumbnails; text has readable previews. Click an item or its paste icon to paste into the previously focused app. Hover over an item for its trash button, which removes it immediately. There is no separate history/detail screen or search field.

The right column combines the music player and pinned app icons. Use the pencil or **Ctrl + K** to edit pins. Up to twelve can be selected; each icon shows its app name on hover. The player samples cover colors locally and uses a curved played-progress line. This decorative progress line is separate from the idle audio meter.

Blue connection buttons run vertically on the left: the main button toggles Wi-Fi, Bluetooth, or audio mute; the arrow opens its Windows Settings page. Device/permission support can limit radio control. Power icons in the header send lock, sleep, restart, or shutdown directly without a Yuta confirmation dialog. Restart and shutdown do not force-close applications, and Windows or an application may still present its own save or policy prompts.

## Idle activity

The notch shows local time by default. Music adds artwork, a shortened title, and thin purple-to-blue bars driven by Windows audio meters. These show an amplitude history, not separate frequency bands. Audio must play on this computer: a remote Spotify Connect device supplies no local sound level. A running focus timer retains its place while music is playing. Reduced-motion preferences are respected.

An active microphone session can show voice activity for apps such as WhatsApp, Zoom, or Discord. Browser calls use microphone levels only and show the browser name, so another tab's music does not drive the voice display. This is microphone activity detection, not universal call detection: other microphone use can appear, muting can hide it, and exclusive audio drivers may not expose meters. Yuta does not record audio.

Connecting power or changing the default audio output produces a brief status notice. Headphones appear when they become the audio output. General Bluetooth pairing, accessory battery levels, and hotspot activity are not integrated.

Smart flattening checks whether supported interactive controls overlap the normal idle footprint. Confirmed overlap reduces the actual window to a four-DIP black strip. It restores the normal notch after the overlap clears. This depends on Windows accessibility providers and can be disabled in Settings. Click, deliberate hover, and the shortcut remain available.

## Other sections

- **Darch (development source):** connect through Chrome from Yuta Settings to use the existing Darch workspace. All account-permitted sections appear by default; hide or reorder them in Yuta Settings. Assignments, calendars, grades and the other tools use Darch's account data, with a shared Focus timer. Use the arrow beside Darch to open the selected page in Chrome. The matching API must be deployed first; v0.2.0 downloads still have the original local Today page. See [Darch](darch.md).
- **Stats:** CPU/GPU/RAM, top processes, disk, network activity, uptime, and an optional speed estimate. Some devices do not expose every counter.
- **Wallpaper:** choose a local folder, browse images, and apply a wallpaper. Recent selections are retained.
- **Routines:** open a named group of up to 12 apps, files, folders, or HTTP(S) websites. Arbitrary shell commands are not accepted.

## Personalize

Settings offers six accent choices, attached-notch or floating-pill shape, monitor selection, hover opening, smart flattening, city, temperature unit, clipboard capture, and a shortcut recorder. Click the current shortcut and press a new combination; Esc cancels. Violet (`#9b80ff`) is the initial accent.

Right-click the notch, expanded menu, or tray icon to hide it in the current app, hide it on a detected website, or pause for one hour. Website rules use a confirmed address-bar hostname and include subdomains. If a browser does not expose its address bar, the website action is unavailable. The tray offers **Resume now** and a list for removing exclusions. The saved shortcut can temporarily open Yuta during a pause or exclusion.

## Keep your data

The profile remains in `%LOCALAPPDATA%\Perch` for compatibility with early versions. This path is intentional. Read [Privacy and storage](PRIVACY.md) before sharing logs, profiles, or screenshots.
