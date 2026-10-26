# Changelog

## Unreleased

- Added the Darch workspace in place of Today, with every account-permitted section, local hide/reorder settings, Chrome sign-in, and a shared Focus countdown. Requires the matching Darch API deployment; not included in v0.2.0 downloads.
- Added revocable account connections, protected native credentials, retained embedded drafts, and coverage for iframe isolation and disconnect behavior.
- Reflowed the original Darch UI for the notch: one compact navigation rail, unchanged Darch themes and fonts, side-by-side Focus controls/activity, and bounded calendar/editor scrolling. Removed the separate host navigation and desktop-page scaling overrides.
- Reduced the compact media player from 400 × 224 to 400 × 144 logical pixels. Playback controls now sit beside the artwork and track details, with the waveform below. Play/pause and text retain their sizes.
- Compact keyboard navigation follows the visible transport controls before the seek slider; long titles fit without overlapping controls.
- Home and the other full-menu panels keep their existing dimensions. The v0.2.0 download archives are unchanged.

## 0.2.0 — 2026-10-25

- First public portable downloads for Windows ARM64 and x64, including .NET, install instructions, source commit metadata, and SHA-256 checksums.
- Compact Home with centered tabs, header power controls, vertical blue connection switches, scrollable clipboard previews, direct paste/delete, and editable app-icon pins.
- Shared 760 × 366 frame for Home, Today, Stats, Wallpaper, Routines, and Settings; longer content scrolls within each panel.
- Media-first opening: a 400 × 224 player while music or video is playing, then the full menu on a second expansion. Artwork-derived colors and a curved progress line appear in both players.
- Live audio-level animation with thin purple/blue bars, microphone-session activity, and refined charging notices. Audio is never recorded.
- Longer, smoother notch shoulders, 480 ms opening, and a layout-ready handshake to avoid a transient scale jump.
- Removed the separate clipboard/launcher screens and Quick note UI.
- Expanded browser/native regression coverage and refreshed sample-data screenshots.

The downloads are unsigned portable builds. WebView2 is required; there is no automatic updater. Source and original assets remain all rights reserved.

## Public source preview — 2026-10-22

- Attached notch and floating pill with deliberate hover, click, configurable shortcut, and automatic retraction.
- Clipboard First Home: searchable copied items, compact media player, six app shortcuts, connection settings, and direct power controls.
- Music activity, timer, charging, and audio-output notices on the native idle surface.
- Experimental geometry-based smart flattening over supported interactive controls.
- Today, system statistics, local wallpapers, routines, and quick notes.
- Local profiles, opt-in encrypted clipboard history, app/website exclusions, and a one-hour pause.
- Core, browser, and native test suites; reproducible sample-data screenshots; Windows CI.

This initial public snapshot does not include an installer, automatic updates, account calendar sync, or a public binary release.
