# Privacy and storage

Yuta does not require a Yuta account and has no analytics backend or built-in AI service. The main UI is bundled locally through a WebView2 virtual host. The optional Darch integration embeds Darch's remote workspace after explicit account approval and syncs that workspace with Darch. Yuta's own preferences and routines remain local.

## What is stored

The compatibility directory is `%LOCALAPPDATA%\Perch`.

| Data | Storage |
| --- | --- |
| Settings, notes, tasks, calendar events, routines, shortcuts, recent destinations, timer, and exclusions | `state.json`, ordinary local JSON. These fields are not encrypted. |
| Captured clipboard text, links, and images | `clipboard.bin`, encrypted with Windows DPAPI for the current Windows account. |
| Optional Darch access/refresh credentials | `darch-credentials.bin`, protected with Windows DPAPI for the current Windows account. Not included in settings exports or sent to the UI. |
| WebView2 profile/cache | Local application profile directory. |
| Application errors | Local `errors.log`, when an error occurs. It may contain paths or integration details. |

Do not include a real profile, clipboard file, logs, or private screenshot in a public issue. Reproduce with sample data and include only the relevant sanitized details.

## Clipboard

Capture starts **off**. After it is enabled, Yuta collects new copies while running. It does not import the operating system's existing clipboard history. Source-app clipboard exclusion formats are respected where supplied.

History is bounded to 200 entries and a 48 MB content budget, with individual text/image limits. Pins receive retention priority but do not bypass the total budget. DPAPI protects the stored file for the current account; it does not prevent other software already running as that account from accessing the clipboard or your profile.

## Optional network requests

| Feature | Destination | Data involved |
| --- | --- | --- |
| Weather | Open-Meteo geocoding and forecast APIs | Configured city/postal query, then coordinates for the forecast. No automatic geolocation. |
| Speed test | Cloudflare speed endpoint | Approximately 11 MB of test download/upload traffic, initiated by the speed-test control. |
| Launch a website | Your default browser | The destination explicitly stored or selected by you. |
| Darch, after connecting | `https://darch-sepia.vercel.app/` | Account-authorized planner reads/edits, Focus commands, and Darch's other tools. Chrome handles sign-in; the remote workspace uses a revocable HttpOnly session cookie in the WebView profile. |

Normal network request metadata, such as your IP address, is visible to those service providers. WebView2 and Windows have their own runtime/update behavior outside Yuta's application-level integrations.

Disconnect Darch in Yuta Settings or Darch's Connected apps settings to revoke the grant. Native tokens never enter the embedded UI; a single-use ticket creates its session. The frame cannot call Yuta's native bridge. Darch controls its account storage and existing feature permissions. See [Darch integration](darch.md) for deployment and live-verification limits.

## Active-window awareness

App exclusions use executable paths. Website exclusions use the hostname exposed by a supported browser's address bar; Yuta saves the excluded host, not a full page path or search query.

Smart flattening reads accessibility control types, visibility, and bounding rectangles. It skips browser document branches and does not read page text, take screenshots, or send screen contents to a service. Provider support is not universal. Reads run outside the UI thread with time, depth, and node bounds.

Media metadata and artwork come from Windows media sessions. Artwork colors are sampled locally. Music and voice animations read Windows audio-session peak meters and process identities; they do not open a microphone recording stream, capture audio samples, store audio, or transmit audio. Browser voice activity is identified by the browser process, not the meeting tab. System statistics come from local Windows counters. Yuta does not request Spotify credentials or publish listening activity.

The Quick note interface was removed in 0.2.0. Existing notes in older profiles are left intact.

## Removing local data

Quit Yuta before changing its profile directory. Removing `%LOCALAPPDATA%\Perch` removes the local profile and clipboard history and may also affect an older Perch installation that uses the same compatibility directory. Back up anything you want to keep first.
