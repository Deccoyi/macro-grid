# Üçüncü Taraf Lisansları

Bu proje aşağıdaki açık kaynak bileşenleri kullanıyor. Her biri kendi lisansıyla dağıtılır; bu dosya sadece bir özet/envanterdir, lisans metinlerinin yerine geçmez.

## Server (.NET / NuGet)

| Paket | Lisans | Not |
|---|---|---|
| ASP.NET Core (`Microsoft.Extensions.*`) | MIT | .NET runtime/SDK'nın parçası |
| `Microsoft.Web.WebView2` | Microsoft (WebView2 SDK Lisans Koşulları) | **Açık kaynak değil** — Microsoft'un ücretsiz, yeniden dağıtılabilir bileşeni. Kullanım Microsoft'un [WebView2 lisans koşullarına](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) tabidir. |
| `xunit`, `xunit.runner.visualstudio` | Apache-2.0 | Yalnızca test projesi (`MacroStation.Tests`), dağıtıma girmiyor |
| `coverlet.collector` | MIT | Yalnızca test projesi |

## Editör (npm — `editor/`, `packages/renderer/`)

| Paket | Lisans |
|---|---|
| React, ReactDOM | MIT |
| Vite, `@vitejs/plugin-react` | MIT |
| TypeScript | Apache-2.0 |
| `lucide-react` (ikon seti) | ISC |
| `postcss`, `postcss-safe-parser` | MIT |
| `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `jsdom` | MIT |

Tam bağımlılık ağacı ve alt-bağımlılıkların lisansları için `npm ls` / `npm-license-checker` gibi bir araçla kendi ortamınızda ayrıca doğrulama yapmanız önerilir; bu liste yalnızca doğrudan (birinci seviye) bağımlılıkları kapsar.

## Katkıda bulunanlar için not

Yeni bir bağımlılık eklerken lisansını kontrol edin ve bu dosyaya ekleyin. Copyleft (GPL/AGPL gibi) lisanslı bir bağımlılık eklemeden önce projenin MIT lisansıyla uyumluluğunu değerlendirin.
