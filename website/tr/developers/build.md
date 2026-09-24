# Kaynaktan derleme

## Sunucu, düzenleyici ve tarayıcı deck'i

[.NET 10 SDK](https://dotnet.microsoft.com/download) ve [Node.js](https://nodejs.org/) 20 veya üzeri gerekir.

```powershell
cd editor;    npm install; npm run build; cd ..
cd webclient; npm install; npm run build; cd ..
Copy-Item editor\dist\*    src\MacroGrid.Host\wwwroot\editor -Recurse -Force
New-Item -ItemType Directory -Force src\MacroGrid.Host\wwwroot\deck | Out-Null
Copy-Item webclient\dist\* src\MacroGrid.Host\wwwroot\deck -Recurse -Force
dotnet run --project src/MacroGrid.Host
```

Testleri `dotnet test` ile çalıştırın. Ayrıntılar [development.md](https://github.com/Deccoyi/macro-grid/blob/main/docs/development.md) dosyasında.

## Android uygulaması

Node.js 20 veya üzeri ile Android Studio (ya da Android SDK ve bir JDK) gerekir; `JAVA_HOME` ve `ANDROID_HOME` ayarlı olmalıdır.

```powershell
npm install
npm run dev          # tarayıcıda web sürümü
npm run build
npx cap sync android
npx cap open android
```

`scripts\build-release-apk.ps1` bir sürüm APK'sı derler. Bkz. [istemci deposu](https://github.com/Deccoyi/macro-grid-client).

## Eklentiler

`macro-grid-plugin` deposunu `macro-grid`'in yanına klonlayın; çünkü C# eklentileri SDK'ya yol üzerinden başvurur.

```powershell
dotnet build OBS\src\MacroGrid.Plugin.Obs.csproj -c Release
dotnet build PLCIcons\src\MacroGrid.Plugin.PlcIcons.csproj -c Release
```

Düzenleyicide **Eklentiler, Eklentileri Yönet…, Klasörden Yükle…** yolunu izleyin ve derleme çıktısını seçin, örneğin `OBS\src\bin\Release\net10.0`. Ayrıntılar: [eklenti dokümantasyonu](https://deccoyi.github.io/macro-grid-plugin/).

## Bu web sitesi

```powershell
cd website
npm install
npm run docs:dev
```
