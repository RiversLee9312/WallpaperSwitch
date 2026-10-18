# Licensing and redistribution

Original WallpaperSwitch code, documentation, and project-created artwork are
licensed under the [MIT License](../LICENSE). The copyright holder is
RiversLee9312 and contributors. Original third-party materials retain their
upstream copyrights and terms.

MIT permits commercial use, modification, and redistribution while requiring
the copyright and permission notice to remain with the software. It does not
require third-party Microsoft runtime binaries to be relicensed or disclosed as
source. This fits the project's MIT-licensed managed libraries and the separate
redistribution terms accompanying the Windows App SDK binaries.

The source license is distinct from the licenses for bundled dependencies:

- `THIRD_PARTY_NOTICES.md` indexes exact dependency versions and their notices.
- `Licenses/manifest.json` records package versions, source URLs, and document
  SHA-256 hashes. Files under `Licenses/` are copied without rewriting their
  original legal text, including upstream third-party notices.
- `DISTRIBUTION_TERMS.txt` presents the project license and unique bundled
  component license terms in the installer. Microsoft components remain subject
  to their original Microsoft software license terms; they are not relicensed
  under the project's MIT license.
- The app's **About → Licenses and notices** dialog reads these files locally.
  No internet connection is required to read the bundled legal documents.
- Inno Setup and the vendored Chinese installer translations retain their
  upstream notices. They are not claimed as original WallpaperSwitch work.

For a dependency update, restore the application and startup helper, then run:

```powershell
.\tools\Update-ThirdPartyNotices.ps1
```

The generator reads the restored dependency graphs and package metadata, copies
upstream license/notice files, and fails if a runtime dependency lacks usable
license text. The release build regenerates this inventory before publishing.
The Windows SDK build/reference packages are development tools rather than
bundled SDK distributions; their license URLs are recorded in the inventory.

Primary references:

- [MIT license explanation](https://choosealicense.com/licenses/mit/)
- [Windows App SDK package](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1)
- [H.NotifyIcon pinned upstream license](https://github.com/HavenDV/H.NotifyIcon/blob/6b97277d084f68bcf60f08fcd769503e8e703bfa/LICENSE.md)
- [Inno Setup license](https://jrsoftware.org/files/is/license.txt)
