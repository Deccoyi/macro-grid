# Third-party notices

Macro Station uses the open-source components below. Each is distributed under its own license; this file is a summary of the direct
dependencies, not a replacement for the license texts. The full dependency trees (and the licenses of the transitive dependencies) can be listed
with `dotnet list package --include-transitive` and `npm ls`.

## Server (.NET, NuGet)

| Package | License | Used for |
|---|---|---|
| `Jint` | BSD-2-Clause | The JavaScript sandbox for JavaScript plugins |
| `Acornima` (dependency of Jint) | BSD-3-Clause | JavaScript parser |
| `NAudio.Wasapi` (and `NAudio.Core`) | MIT | Windows core audio (master volume and mute) |
| `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | MIT | Hosting and logging abstractions |
| ASP.NET Core and the .NET runtime | MIT | Web server; bundled in the self-contained release build |
| `Microsoft.Web.WebView2` | Microsoft software license terms | **Not open source.** Microsoft's free, redistributable SDK for the editor window; its use is subject to the [WebView2 license terms](https://developer.microsoft.com/microsoft-edge/webview2/). The WebView2 Runtime itself is installed with Windows or separately by the user. |
| `xunit`, `xunit.runner.visualstudio` | Apache-2.0 | Tests only, not distributed |
| `Microsoft.NET.Test.Sdk`, `coverlet.collector` | MIT | Tests only, not distributed |

## Editor, browser deck and renderer (npm)

| Package | License | Used in |
|---|---|---|
| `react`, `react-dom` | MIT | Editor, deck, renderer |
| `lucide-react` | ISC (with MIT-licensed portions derived from Feather) | Editor icons and the icon picker |
| `qrcode` | MIT | The pairing QR code in the editor |
| `postcss`, `postcss-safe-parser` | MIT | The custom CSS sanitizer in the renderer |
| `vite`, `@vitejs/plugin-react` | MIT | Build tooling |
| `typescript` | Apache-2.0 | Build tooling |
| `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `jsdom` | MIT | Renderer tests only |

## For contributors

When you add a dependency, check its license and add it here. Do not add a dependency under a copyleft license (GPL, AGPL and similar) without first
checking that it is compatible with this repository's MIT license.
