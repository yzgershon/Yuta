# Third-party notices

Yuta's original source and project artwork are all rights reserved. That notice does not replace or restrict the licenses supplied with third-party components.

| Component | Role | License / source |
| --- | --- | --- |
| Phosphor Icons | Bundled interface icons | MIT; the complete notice is in [ui/assets/icons/LICENSE](ui/assets/icons/LICENSE). [Project](https://github.com/phosphor-icons/core). |
| Microsoft.Web.WebView2 | Local web UI host | [NuGet package and license](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4022.49). |
| NAudio | Windows audio integration | [Project and MIT license](https://github.com/naudio/NAudio). |
| System.Management | Windows management queries | [NuGet package and license](https://www.nuget.org/packages/System.Management/8.0.0). |
| System.Diagnostics.PerformanceCounter | System activity counters | [NuGet package and license](https://www.nuget.org/packages/System.Diagnostics.PerformanceCounter/8.0.0). |
| System.Security.Cryptography.ProtectedData | Account-scoped clipboard encryption | [NuGet package and license](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/8.0.0). |
| .NET / Windows Desktop runtime | Self-contained Windows builds | [Runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) and notices supplied with the runtime. |
| Playwright | Development-only browser tests and screenshots | [Apache-2.0](https://github.com/microsoft/playwright/blob/main/LICENSE). |

Exact direct dependencies are in [Yuta.csproj](Yuta.csproj) and [package.json](package.json). Transitive dependencies retain their own package notices. Preserve all bundled runtime notices when distributing an authorized build.

Official portable downloads include NAudio and WebView2 license/notice texts in `licenses/`, Phosphor's notice in `ui/assets/icons/LICENSE`, and the .NET runtime notices supplied by the self-contained publish.

Weather data is provided by [Open-Meteo](https://open-meteo.com/), with attribution under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The optional speed test uses [Cloudflare's speed service](https://speed.cloudflare.com/).

Windows, Microsoft, Spotify, and other third-party names or marks identify compatible platforms or applications. Yuta is not affiliated with or endorsed by those companies. Demonstration track names and artwork are fictional; no commercial album artwork is bundled in the documentation.
