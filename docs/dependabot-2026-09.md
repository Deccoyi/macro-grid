# Dependabot değerlendirmesi — 2026-09-24

Durum: **bitti, `dev`'e push edildi, bot PR'ları kapatıldı** (3 repoda 14 PR, açık PR kalmadı).

## Bitenler

| Repo | Commit | İçerik |
|---|---|---|
| macro-grid | `fd59b49` | vite 8, plugin-react 6, lucide-react 1.x, React 19, TS 7 (editor/webclient/renderer), renderer'da vitest 5 + jsdom 30 + jest-dom 7, Actions (checkout v7, setup-dotnet v6, setup-node v7), NuGet (coverlet 10, Test.Sdk 18, xunit.runner 4) |
| macro-grid-client | `0a43e85` | React 19, TS 7, vite 8, plugin-react 6, vitest 5, jsdom 30, Actions (checkout v7, setup-node v7, setup-java v6, setup-android v4, upload-artifact v7) |
| macro-grid-client | `fad328f` | google-services 4.5.0 |
| macro-grid-client | `31b5356` | Gradle wrapper 8.14.3 → **9.5.1** |
| macro-grid-plugin | `9b80e15` | Actions (checkout v7, setup-dotnet v6, setup-node v7, configure-pages v6, upload-pages-artifact v5, deploy-pages v5) |

Kodda gereken düzeltmeler (React 19 / TS 7 kaynaklı):
- `editor/src/panels/actionForms/forms.tsx`: `import type { JSX } from "react"` (global `JSX` namespace kalktı)
- `editor/src/vite-env.d.ts`, `macro-grid-client/src/vite-env.d.ts`: `/// <reference types="vite/client" />` (css side-effect import, TS 7)

Doğrulama (temiz klonda, dev üstünde): renderer 26 test, editor/webclient typecheck+build, `dotnet test` 206, plugin 7 test, client typecheck+build+26 test, `./gradlew assembleDebug` (Gradle 9.5.1). `dev` CI'ı macro-grid ve plugin'de yeşil.

## Alınmayanlar
- **Gradle 9.7.1** (bot önerisi): AGP 8.13 ile çalışmıyor (Gradle 9.6'da kaldırılan `InternalProblems` API'si). 9.5.1 alındı. AGP 9'a geçince 9.7+ tekrar denenir.
- Tek başına vite/vitest PR'ları (macro-grid #8, #9, #10; client #6): kırıktı veya diğer güncellemelerin içinde kaldı, kapatıldı.

## Senden beklenenler
1. **Evde her repoda `git pull` (dev), sonra `npm ci`**: client kökü, macro-grid'de `packages/renderer`, `editor`, `webclient`. Eski `node_modules` React 18 ile kalır, `npm ci` şart.
2. **Editor ve deck arayüzünü elle bir kez aç** (React 19 sonrası): testler render davranışının hepsini kapsamıyor.
3. **`macro-grid-client` release.yml'de Android SDK adımını bir kez çalıştır**: setup-android v4 dev'de var, Actions üzerinde henüz doğrulanmadı (yerelde sadece Gradle build denendi).
4. **`macro-grid-plugin` Pages deploy'unu bir sonraki docs push'unda izle**: configure-pages v6 / upload-pages-artifact v5 / deploy-pages v5 sadece gerçek deploy'da çalışıyor.
5. **Karar:** `dependabot.yml` düzenlensin mi? (a) `macro-grid-client/.github/dependabot.yml`'de her blokta `open-pull-requests-limit` iki kez yazılı (2 ve 5), (b) uzun branch adları için grup adı `all-updates` kısaltılabilir, dizinler azaltılabilir. Windows'ta `git config core.longpaths true`. Sen söylersen yaparım.
6. Client'ta `dev` push'una CI tetiklenmiyor (sadece PR/main); dev'i doğrulamak için elle çalıştırmak gerekir.

## Notlar
- Masaüstündeki klasörde `dotnet build` "Access denied" veriyor (antivirüs / Controlled Folder Access olası); klasör dışında derleme çalışıyor.
- Bot gelecek aylık taramada yeni PR açarsa, aynı yaklaşım: gruplar birlikte `dev`'de test edilmeli. React/TS/vite major'larını `ignore` ile dondurmak da bir seçenek.
