# Localization maintenance

Wallpaper Switch uses Windows App SDK MRT Core for the WinUI app. The default resource language is `en-US`; the resource index is built from `Strings/<language-tag>/Resources.resw`. Keep every supported locale's resource-name set aligned with the English file. The picker currently exposes 24 translated languages plus the system-default option.

Use `x:Uid` for text declared in XAML. Resource names for an element use the form `<Uid>.<Property>`, including attached properties such as `<Uid>.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name`. For code lookups, use the same dotted key in `Localizer.GetString` or `Localizer.Format`; the MRT Core `ResourceMap` path converts those dots to `/` before lookup. See Microsoft's [WinUI 3 localization guidance](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app) and [MRT Core overview](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/mrtcore-overview).

When adding or changing text:

1. Update `Strings/en-US/Resources.resw` first, then add the matching key to every translated locale.
2. Keep every .NET composite-format placeholder, including format specifiers such as `{0:F4}` and `{1:g}`, at the same index in every translation. Run `dotnet test Tests/WallpaperSwitch.Tests.csproj` to check key coverage and formatting contracts.
3. Keep language tags in `Localization/LanguageCatalog.cs` synchronized with resource-folder names. The system-default option is a preference, not a resource folder; missing resources fall back to English.
4. Use `Localizer.Format` for values that include times, dates, or numbers. Explicit language choices set the matching formatting culture; system-default follows Windows. The language choice is saved in `settings.json` and requires restarting the app to take effect.
5. Preserve right-to-left flow for Arabic and Hebrew. `MainWindow` sets the root flow direction from the chosen culture; the UI MSTest verifies the position of a trailing startup control in both directions.

The standalone `StartupHost` helper uses standard .NET `ResourceManager` `.resx` resources in `StartupHost/HostText*.resx`. It receives the selected UI language for interactive startup changes and keeps its resources beside the protected helper; the SYSTEM service path does not read per-user settings or load dependencies from a user profile.

The UI MSTest launches a published build. Set `WALLPAPERSWITCH_TEST_APP` to a published `WallpaperSwitch.exe` path when it is outside the default `artifacts/publish-1.3.1` folder. Its settings file and single-instance name are isolated to the test process, and it leaves the user's saved settings and startup service registration untouched.
