# Privacy and local data

WallpaperSwitch saves preferences in `%LOCALAPPDATA%\WallpaperSwitch\settings.json`.
These include wallpaper paths, schedules, the selected language, and, when location
features are enabled, latitude/longitude and refresh timestamps. Application
diagnostics are written to `startup.log` in the same directory; logs may contain
wallpaper paths and location coordinates. Review those files before sharing them.

The optional location feature uses Windows location services. Windows handles
location permission and may communicate with its location provider. You can use
custom schedules and leave automatic location refresh disabled.

Lock-screen conversion writes temporary images under the app's local
`lockscreen-cache` directory. Standard startup uses your Windows Run registry
entry. High-priority startup is opt-in and stores the registered user and app
path in Windows service configuration; changing that registration needs Windows
administrator consent.

WallpaperSwitch has no account, advertising service, or application analytics
backend. Opening the source/release link launches your browser and connects to
GitHub. Microsoft runtime components and Windows services may have their own
data practices and license terms; see the original notices in `Licenses/` and
[Microsoft's privacy statement](https://privacy.microsoft.com/privacystatement).

Uninstalling removes the startup registration and installed program. Your local
preferences and diagnostics are retained so that reinstalling can reuse them.
You can remove `%LOCALAPPDATA%\WallpaperSwitch` yourself after closing the app
if you no longer need that data.
