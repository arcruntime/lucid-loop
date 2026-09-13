# Bundled HUD font

The existing `GymUI.Font` property loads and caches one `Fonts/NotoSansJP-Regular` resource for runtime uGUI labels. The original static Noto Sans JP Regular OpenType/CFF font is bundled unmodified. It is version 2.004, published by the official [Noto CJK project](https://github.com/notofonts/noto-cjk). This is the upstream Japanese regional subset, not a project-generated subset or a variable font.

## Download and license

- Pinned upstream commit: `165c01b46ea533872e002e0785ff17e44f6d97d8`.
- [Original font download](https://raw.githubusercontent.com/notofonts/noto-cjk/165c01b46ea533872e002e0785ff17e44f6d97d8/Sans/SubsetOTF/JP/NotoSansJP-Regular.otf).
- File size: **4,533,028 bytes** (about 4.32 MiB), one Regular weight.
- Font SHA-256: `dff723ba59d57d136764a04b9b2d03205544f7cd785a711442d6d2d085ac5073`.
- License: **SIL Open Font License 1.1**, preserved verbatim in `OFL-NotoSansJP.txt` from the [same upstream commit](https://raw.githubusercontent.com/notofonts/noto-cjk/165c01b46ea533872e002e0785ff17e44f6d97d8/LICENSE).
- Original license SHA-256: `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2`.
- Copyright notice from the font's embedded name table: **© 2014-2021 Adobe (http://www.adobe.com/).** The original font metadata, copyright, and license remain intact.

The machine-readable download URLs, hashes, version and inspection results are in `provenance.json`. The font is not sold separately. Keep its license and copyright notice with redistributed copies.

## Coverage and verification limits

Binary inspection with fontTools identified an `OTTO` static font with no `fvar` table, 17,936 glyphs, and 16,732 Unicode mappings. It maps all 95 printable ASCII characters, all 86 code points in U+3041–U+3096 (hiragana), all 90 code points in U+30A1–U+30FA (katakana), and 12,747 BMP ideographs in U+4E00–U+9FFF. Japanese punctuation and the sample `日本語漢字ひらがなカタカナ。、！？「」ループ証拠会話再開設定音量ABCxyz0123456789é—…` have mappings. Rare-name characters **髙 (U+9AD9)** and **﨑 (U+FA11)** are present.

There are 655 supplementary-plane mappings, including **𠮷 (U+20BB7)**. Their presence does not establish that Unity's legacy uGUI text path correctly renders every surrogate pair or variation sequence. This regional subset does not cover every rare kanji or arbitrary Unicode; for example, **😀 (U+1F600)** is absent. Emoji and scripts outside this font's coverage have no established fallback contract.

Actual Unity phone-preview validation passed kana, ordinary/rare BMP kanji, punctuation, Latin text, input text and transcript wrapping. The bottom transcript capture shows **𠮷 omitted by the legacy uGUI renderer**, despite its font mapping. Do not claim supplementary-plane coverage. Physical iPhone rendering and keyboard composition remain pending. See `docs/IMPLEMENTATION_VALIDATION.md` for captures and test evidence. Bundling a font does not establish Japanese speech-provider acceptance or change the game's language policy. The existing text API and all callers remain unchanged; only a missing bundled resource triggers the diagnostic LegacyRuntime fallback.
