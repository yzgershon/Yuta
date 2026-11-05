# Brief notification previews

Yuta can mirror newly arriving Windows notifications as a 320 × 64 notch preview. It shows the app logo or initial, title, and a single message line, then returns to the previous activity after 5.5 seconds. Click the preview to dismiss it. There is no inbox, stored notification history, automatic reply, or removal of the original Windows notification.

Previews are off until enabled in Settings. Startup establishes a baseline without showing old alerts. Bursts show only the newest eligible alert, with a six-second quiet gap. Hidden, flattened, expanded, and Windows-popup states consume alerts quietly; closing those states does not replay a backlog. Notifications never open the full Yuta menu. Reduced-motion settings apply to the transition.

## Windows access and packaging

Windows' [notification listener](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener) requires the `userNotificationListener` capability and explicit user permission. The portable ZIP has no package identity, so its Settings explains this requirement and offers a sample preview. It does not pretend to receive live notifications.

The `packaging/AppxManifest.xml` manifest declares the capability for a full-trust MSIX build. It preserves the existing `%LOCALAPPDATA%\Perch` profile through the documented [filesystem virtualization exclusion](https://learn.microsoft.com/en-us/windows/msix/desktop/flexible-virtualization). This avoids creating a second set of settings or encrypted clipboard history. Yuta's existing single-instance mutex prevents portable and packaged copies from writing the profile together.

Build a normal portable package first with `scripts/package.ps1`, extract it, then run:

```powershell
./scripts/package-msix.ps1 -PayloadDirectory artifacts/package-check/Yuta -Version 0.3.1.0 -Architecture x64
```

The script validates and packages the extracted payload with Windows SDK MakeAppx. Supply `-Publisher` and `-CertificateThumbprint` for an existing trusted signing certificate to sign it. Without those credentials the output is an **unsigned validation artifact**, not an installable public release. The script does not generate or trust certificates, change Windows security settings, install the app, or publish a release. ARM64 uses the matching portable payload and `-Architecture arm64`.

Before releasing signed MSIX downloads, verify installation, preserved settings, the native permission dialog, real notifications from several apps, permission revocation, sleep/resume, and uninstall on Windows. Local rendering and fixture tests do not establish those live-platform results.
