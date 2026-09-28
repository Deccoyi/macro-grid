# Third-party notices

Macro Grid is built on open-source components. Each one is distributed under its own license. This file is the index: for every component
it lists the version, license, copyright holder and project page, and points to the folder under [`licenses/`](licenses/) that holds the
original license text (and any NOTICE file) copied unchanged from the package.

The release folder and the installer ship this file, [LICENSE](LICENSE) and the whole `licenses/` folder next to `MacroGrid.exe`.

Versions are the ones resolved at the time of writing (see the `csproj` files and the npm lockfiles). Only components that are used at run
time or end up in the shipped files are listed; build and test tools (Vite, TypeScript, Vitest, xUnit and similar) are not distributed and are
not listed.

## The project itself

Macro Grid (server, editor and browser deck in this repository) is released under the [MIT License](LICENSE), Copyright (c) 2026 Deccoyi.

## App icons (AI-generated)

The application icons (`src/MacroGrid.Host/app.ico`, the favicon and the PNG icons in `editor/public/`) are AI-generated. They are not
taken from a third-party icon set and are covered by the project's MIT license.

## .NET (NuGet and the bundled runtime)

| Component | Version | License (SPDX) | Copyright holder | Project | Licenses folder |
|---|---|---|---|---|---|
| Jint | 4.16.3 | BSD-2-Clause | Sebastien Ros | https://github.com/sebastienros/jint | [licenses/jint](licenses/jint) |
| Acornima (used by Jint) | 1.7.0 | BSD-3-Clause | Adam Simon | https://github.com/adams85/acornima | [licenses/acornima](licenses/acornima) |
| NAudio.Core | 3.1.0 | MIT | Mark Heath | https://naudio.github.io/NAudio/ | [licenses/naudio-core](licenses/naudio-core) |
| NAudio.Wasapi | 3.1.0 | MIT | Mark Heath | https://naudio.github.io/NAudio/ | [licenses/naudio-wasapi](licenses/naudio-wasapi) |
| Microsoft.Web.WebView2 (SDK) | 1.0.4191.47 | BSD-3-Clause-style (Microsoft's own text, no SPDX id) | Microsoft Corporation | https://aka.ms/webview | [licenses/microsoft-web-webview2](licenses/microsoft-web-webview2) |
| System.Numerics.Tensors (used by Jint) | 9.0.0 | MIT | .NET Foundation and Contributors, Microsoft | https://github.com/dotnet/runtime | [licenses/system-numerics-tensors](licenses/system-numerics-tensors) |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | MIT | .NET Foundation and Contributors, Microsoft | https://github.com/dotnet/runtime | [licenses/microsoft-extensions-hosting-abstractions](licenses/microsoft-extensions-hosting-abstractions) |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | MIT | .NET Foundation and Contributors, Microsoft | https://github.com/dotnet/runtime | [licenses/microsoft-extensions-logging-abstractions](licenses/microsoft-extensions-logging-abstractions) |
| System.Security.Cryptography.ProtectedData (encrypts the device tokens on disk) | 10.0.12 | MIT | .NET Foundation and Contributors, Microsoft | https://github.com/dotnet/runtime | [licenses/system-security-cryptography-protecteddata](licenses/system-security-cryptography-protecteddata) |
| .NET runtime (bundled in the self-contained build, including the `Microsoft.Extensions.*` libraries it carries) | 10.0.x | MIT | .NET Foundation and Contributors, Microsoft | https://github.com/dotnet/runtime | [licenses/dotnet-runtime](licenses/dotnet-runtime) |
| ASP.NET Core (bundled) | 10.0.x | MIT | .NET Foundation and Contributors | https://github.com/dotnet/aspnetcore | [licenses/aspnet-core](licenses/aspnet-core) |
| Windows Forms (bundled, editor window host) | 10.0.x | MIT | .NET Foundation and Contributors | https://github.com/dotnet/winforms | [licenses/windows-forms](licenses/windows-forms) |

The WebView2 **Runtime** (the Edge-based browser engine the editor window uses) is not part of this project's files: it is installed with
Windows or separately by the user, under Microsoft's own terms. Only the WebView2 SDK package above is redistributed.

## Editor and browser deck (npm, bundled into the built web files)

The editor (`editor/`) and the browser deck (`webclient/`) share the renderer package (`packages/renderer`), so the deck bundle contains the
React and renderer dependencies as well.

| Component | Version | License (SPDX) | Copyright holder | Project | Licenses folder |
|---|---|---|---|---|---|
| react | 18.3.1 | MIT | Meta Platforms, Inc. and affiliates (Facebook, Inc.) | https://reactjs.org/ | [licenses/react](licenses/react) |
| react-dom | 18.3.1 | MIT | Meta Platforms, Inc. and affiliates (Facebook, Inc.) | https://reactjs.org/ | [licenses/react-dom](licenses/react-dom) |
| scheduler (used by react-dom) | 0.23.2 | MIT | Meta Platforms, Inc. and affiliates (Facebook, Inc.) | https://reactjs.org/ | [licenses/scheduler](licenses/scheduler) |
| lucide-react (editor icons; also portions from Feather) | 0.460.0 | ISC (portions MIT) | Lucide Contributors; Cole Bemis (Feather) | https://lucide.dev | [licenses/lucide-react](licenses/lucide-react) |
| qrcode | 1.5.4 | MIT | Ryan Day | https://github.com/soldair/node-qrcode | [licenses/qrcode](licenses/qrcode) |
| dijkstrajs (used by qrcode) | 1.0.3 | MIT | Wyatt Baldwin (adapted from Dijkstar) | https://github.com/tcort/dijkstrajs | [licenses/dijkstrajs](licenses/dijkstrajs) |
| postcss | 8.5.28 | MIT | Andrey Sitnik | https://postcss.org/ | [licenses/postcss](licenses/postcss) |
| postcss-safe-parser | 7.1.0 | MIT | Andrey Sitnik | https://github.com/postcss/postcss-safe-parser | [licenses/postcss-safe-parser](licenses/postcss-safe-parser) |
| nanoid (used by postcss) | 3.3.19 | MIT | Andrey Sitnik | https://github.com/ai/nanoid | [licenses/nanoid](licenses/nanoid) |
| picocolors (used by postcss) | 1.1.1 | ISC | Alexey Raspopov and contributors | https://github.com/alexeyraspopov/picocolors | [licenses/picocolors](licenses/picocolors) |
| source-map-js (used by postcss) | 1.2.1 | BSD-3-Clause | Mozilla Foundation and contributors | https://github.com/7rulnik/source-map-js | [licenses/source-map-js](licenses/source-map-js) |

No fonts are bundled: the editor uses the system font stack. The Lucide icons are shipped as part of `lucide-react`.

## For contributors

When you add a dependency that ends up in the shipped files, check its license, copy the original license file (and NOTICE, for Apache-2.0
packages) into `licenses/<name>/`, and add a row here. Do not add a dependency under a copyleft license (GPL, AGPL and similar) without first
checking that it is compatible with this repository's MIT license.
