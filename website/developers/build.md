# Build from source

## Server, editor and browser deck

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 20 or newer.

```powershell
cd editor;    npm install; npm run build; cd ..
cd webclient; npm install; npm run build; cd ..
Copy-Item editor\dist\*    src\MacroGrid.Host\wwwroot\editor -Recurse -Force
New-Item -ItemType Directory -Force src\MacroGrid.Host\wwwroot\deck | Out-Null
Copy-Item webclient\dist\* src\MacroGrid.Host\wwwroot\deck -Recurse -Force
dotnet run --project src/MacroGrid.Host
```

Run the tests with `dotnet test`. More in [development.md](https://github.com/Deccoyi/macro-grid/blob/main/docs/development.md).

## Android app

You need Node.js 20 or newer, and Android Studio (or the Android SDK and a JDK) with `JAVA_HOME` and `ANDROID_HOME` set.

```powershell
npm install
npm run dev          # the web version in a browser
npm run build
npx cap sync android
npx cap open android
```

`scripts\build-release-apk.ps1` builds a release APK. See the [client repository](https://github.com/Deccoyi/macro-grid-client).

## Plugins

Clone `macro-grid-plugin` next to `macro-grid`, because the C# plugins reference the SDK by path.

```powershell
dotnet build OBS\src\MacroGrid.Plugin.Obs.csproj -c Release
dotnet build PLCIcons\src\MacroGrid.Plugin.PlcIcons.csproj -c Release
```

In the editor use **Plugins, Manage Plugins…, Install from Folder…** and pick the build output, for example `OBS\src\bin\Release\net10.0`. Details: [plugin documentation](https://deccoyi.github.io/macro-grid-plugin/).

## This website

```powershell
cd website
npm install
npm run docs:dev
```
