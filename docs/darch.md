# Darch in Yuta

Darch replaces the Today page with your connected school workspace. Darch's own navigation becomes a compact icon rail containing Today, Assignments, Calendar, Focus, Time log, Grades, Brain games, Settings, and SAT practice or Admin when your Darch account permits them. Hover an icon for its name; scroll the rail to reach more sections. Mental math and individual games remain inside Brain games; onboarding opens when a new account needs setup.

Open **Yuta Settings → Darch → Connect**. Yuta opens Chrome for Darch sign-in and approval. No shared API key or password is entered into Yuta. Click the Darch logo in the icon rail to **Open Darch in Chrome** at the selected section. In Yuta Settings, hide sections you do not use or move them up and down. These visibility preferences apply to Darch's original links and are local to Yuta; they do not change your Darch account or grant additional permissions.

The full workspace stays within Yuta's 760 × 366 logical-pixel frame. It uses the original Darch components, fonts, artwork, and saved light/dark/custom theme. Responsive layout rules put the schedule beside due work, the Focus timer beside its activity summary, and grades beside their categories. There is no whole-page scaling or second Yuta navigation rail. Lists and supporting details scroll internally; editor fields scroll while Save and Cancel remain reachable.

The Darch frame stays mounted when switching Yuta tabs, preserving drafts and avoiding repeated loading. Assignments, grades, calendars, settings, games and SAT use Darch's real interfaces and existing account data. Focus runs against a shared server deadline and can appear in the idle notch even while the web view is suspended.

![Original Darch Focus timer and activity summary inside Yuta's compact frame](images/darch-focus.png)

Development preview with a fictional account. This is the original Darch UI with a compact layout; it is not included in the current downloads.

Disconnect from Yuta Settings or from **Darch Settings → Connected apps** to revoke the connection. Tokens stay in Windows DPAPI-protected storage and are not exposed to the local UI. The embedded page receives only a one-use bootstrap ticket and an HttpOnly cookie bound to the same revocable grant. Frame messages are checked against Darch's exact origin and the active frame, and cannot directly invoke arbitrary native actions.

## Integration status

This source change requires the matching Darch API deployment. The existing downloadable Yuta release does not acquire the integration simply because a source branch is pushed. Until the backend is deployed, Yuta shows a deployment/connection error rather than pretending to sync. The local preview and automated integration fixtures use sample accounts. Production sign-in, third-party cookie behavior in the installed WebView2 runtime, and real-account cross-device sync must be checked after Darch deploys.

The native client is fixed to `https://darch-sepia.vercel.app/`. API details live in the Darch repository's `docs/API.md`. It uses native browser authorization with PKCE, rotating access and refresh tokens, and an explicit scope list. Read-only projections, a versioned full workspace, Focus commands, and aliases for Darch's remaining feature handlers are available under `/api/v1/`.

## Schoology extension

The existing Darch extension stays in Chrome. It imports Schoology assignments, classes, grades, and absences into the signed-in Darch account. Connect Yuta to that same account to see the resulting planner data; the extension does not need a separate Yuta API key. Yuta does not run the Chrome extension inside its WebView.

Direct extension commands and last-import status are not part of this integration. The extension currently keeps its sync result in Chrome storage. A future Schoology update notice in the notch could show new assignments, changed grades, and the last successful import by publishing that summary through the authenticated Darch API.

## Checks

`npm test` and `npm run test:ui` cover the Yuta interface, visibility preferences, iframe retention, disconnect, frame-message isolation, and existing interactions. `Yuta.exe --self-test` includes credential rotation/protection, revoked refresh handling, bounded loopback input and disabled-network fixtures. Darch's own tests exercise the production handler with fake accounts, including website/native writes, compare-and-set conflicts, role boundaries and timer deduplication.
