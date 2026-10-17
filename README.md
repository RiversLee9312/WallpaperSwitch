# WallpaperSwitch

A Windows desktop app that switches wallpapers and light/dark appearance across dawn, daytime, dusk, and night. It supports custom schedules or sunrise/sunset times calculated from your location.

## Features

- Per-period wallpaper and application/system appearance settings.
- Optional lock-screen wallpaper synchronization and automatic location refresh.
- System-tray controls and ordinary sign-in startup.
- Opt-in high-priority startup through a Windows service. Administrator approval is required when changing service registration; the desktop app runs as the signed-in user.
- Localized UI and runtime messages in 24 languages, with a system-language default, English fallback, a saved language preference, and right-to-left layout for Arabic and Hebrew.
- A multilingual Inno Setup installer.

## Technology

The app uses C#/.NET 8, WinUI 3, and Windows App SDK **2.5.1**. Localization uses Microsoft's **MRT Core** resource manager, standard `Strings/<language>/Resources.resw` files, and XAML `x:Uid` identifiers. The standalone service helper uses embedded .NET resources so it can run independently of the UI resource loader.

Microsoft references:

- [Localize WinUI apps](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app)
- [MRT Core overview](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/mrtcore-overview)
- [Windows App SDK release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0?pivots=stable)

## Languages

English, Simplified Chinese, Traditional Chinese, Japanese, Korean, German, French, Spanish, Italian, Brazilian Portuguese, Russian, Ukrainian, Polish, Dutch, Swedish, Danish, Finnish, Turkish, Arabic, Hebrew, Hindi, Indonesian, Thai, and Vietnamese.

The language setting applies when the app next starts. The Windows display language and region are not changed. Installer language resources are documented in [installer/Languages/README.md](installer/Languages/README.md).

## Build

Use Windows x64 with the .NET SDK, the Windows SDK build tools, and Inno Setup 6.7.3 or later. .NET 10 SDK can build the .NET 8 targets. NuGet restore downloads the required targeting and runtime packs.

```powershell
dotnet restore WallpaperSwitch.sln
dotnet build WallpaperSwitch.sln -c Release -p:Platform=x64
```

To publish the app and service helper, run the unit tests, and build the installer:

```powershell
.\tools\Build-Release.ps1
```

The script accepts `-InnoCompiler` when Inno Setup is installed elsewhere, and `-NuGetSource` for an optional package source. It writes the release app to `artifacts/publish-1.3.0/` and the installer to `artifacts/RiyeQiehuan-Setup.exe`. Published builds bundle the Windows App SDK runtime.

## Tests

All automated tests use MSTest. Build the test project, then run its generated runner:

```powershell
dotnet build .\Tests\WallpaperSwitch.Tests.csproj -c Release
.\Tests\bin\x64\Release\net8.0-windows\WallpaperSwitch.Tests.exe --filter 'TestCategory!=UI&TestCategory!=ServiceIntegration' --report-trx
```

For desktop UI tests, publish the app first. The UI runner uses isolated settings and an independent instance name, so an installed instance can keep running. `WALLPAPERSWITCH_TEST_APP` can point to the executable being tested. Service integration tests require `WALLPAPERSWITCH_SERVICE_TEST=1` and Windows administrator approval; they temporarily register a service and remove it afterwards. Run those only on a machine without an existing WallpaperSwitch startup service registration.

```powershell
$env:WALLPAPERSWITCH_TEST_APP = (Resolve-Path .\artifacts\publish-1.3.0\WallpaperSwitch.exe).Path
.\Tests\bin\x64\Release\net8.0-windows\WallpaperSwitch.Tests.exe --filter 'TestCategory=UI' --report-trx
```

## Project layout

- `Strings/` and `Localization/`: app translations and language selection.
- `Services/`, `Models/`, `Shared/`: app behavior, settings, and shared startup contracts.
- `StartupHost/`: independent Windows service and registration helper.
- `Tests/`: MSTest unit, resource, UI, and service tests.
- `Assets/` and `tools/Generate-*`: icons and their source/generation scripts.
- `installer/`: Inno Setup configuration and Chinese installer language files.

Generated output, IDE caches, local screenshots, and machine-specific cleanup inventories are excluded from Git.
