# Installer language resources

`WallpaperSwitch.iss` offers 32 languages: English, the 29 language files shipped with Inno Setup 6.7.3, and the two Chinese files in this folder. The language-specific messages for WallpaperSwitch are translated in the script's `[CustomMessages]` section.

Inno Setup does not include Chinese language files in its installed `Languages` directory. These two community-maintained translations are vendored from the Inno Setup source repository so builds do not depend on downloading them:

- Simplified Chinese: <https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl> (maintainer credit is preserved in the file header)
- Traditional Chinese: <https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseTraditional.isl> (maintainer credits are preserved in the file header)

When updating either file, keep its upstream translation credits and UTF-8 encoding intact.
