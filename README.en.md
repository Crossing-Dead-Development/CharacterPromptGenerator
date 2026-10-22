# Character Design Prompt Generator

[繁體中文](README.md) · **English**

**Crossing Dead Development · v0.3.0** · UI available in English / Traditional Chinese / Japanese

An offline character-design prompt roller for drawing streams. It randomly draws keywords and emoji from a customizable, categorized word bank and combines them into a character-design practice prompt. You can lock results you like, reroll parts of a prompt, save your own presets, keep a history with favorites, and maintain large word banks in the app or through JSON / CSV / TXT files.

> This is not an AI image generator or story writer. The program only rolls prompts; the interpretation is entirely up to the artist. Fully offline: no account, no network connection.

> 📱 **Android version**: same rolling rules and data formats; word banks move freely between computer and phone. Installing, moving data, building and signing are covered in [docs/Android.md](docs/Android.md) (Traditional Chinese).

---

## Contents

1. [Quick start](#1-quick-start)
2. [Using the app](#2-using-the-app)
3. [Word bank import formats](#3-word-bank-import-formats)
4. [Data location, backup and restore](#4-data-location-backup-and-restore)
5. [Building the Windows EXE](#5-building-the-windows-exe)
6. [Project structure](#6-project-structure)
7. [Design decisions](#7-design-decisions)
8. [Testing](#8-testing)
9. [Not yet done and known limitations](#9-not-yet-done-and-known-limitations)

---

## 1. Quick start

1. Download `CharacterPromptGenerator_v*_win-x64.exe` from [Releases](https://github.com/Crossing-Dead-Development/CharacterPromptGenerator/releases/latest) and run it. There is no installer, it is a single file, and you do not need to install .NET separately.
2. The word bank is **empty** on first launch. Download a word bank file from the same Release and import it:
   - English: `Character_Design_Prompt_Library_v1_EN.csv`
   - Traditional Chinese: `Character_Design_Prompt_Library_v1_TW.csv`
3. Press **📥 Import** in the middle of the Roll page (the same button is on the "Word bank" and "Data & backup" pages) and choose the downloaded CSV.
4. In the import preview, keep **Merge**, check the counts and press **✔ Apply import**. A backup is made first, and the new entries can be drawn right away.
5. On the **🎲 Roll** page, tick categories, set the counts and press **Generate prompt**.

To change the interface language, use the **🌐 Language / 介面語言** menu at the bottom of the sidebar. It takes effect after a restart.

Requirements: Windows 10 / 11 x64.

---

## 2. Using the app

### 2.1 Roll (main screen)

| Feature | Description |
|---|---|
| Category checkboxes and counts | Each category on the left can be ticked, and − / + sets how many to draw (0 is allowed). "N available" is the number of entries that can currently be drawn; it turns yellow when the count is higher. Settings are saved automatically. |
| 🎲 Generate prompt / Regenerate all | Draws every result again using the settings on the left. If anything is locked, you are asked first (regenerating clears the locks). |
| 🔒 Lock | Each result has its own Lock toggle. Locked results stay exactly the same when rerolling. |
| 🔁 Reroll unlocked (F5) | Keeps locked items and redraws the rest, preferring words that differ from the previous round. |
| 🎲 Reroll (per category) | Rerolls only the unlocked items in that category; other categories are untouched. |
| 🔓 Unlock all | Removes every lock at once. |
| 📋 Copy prompt (Ctrl+Shift+C) | Copies the prompt to the clipboard as "Category: word1, word2", including emoji. |
| ☆ Favorite | Marks the current result as a favorite. Favorites are kept forever. |
| Presets | Choose a preset and press Apply. Save as new, Overwrite, Rename and Delete let you edit presets freely. |

Rolling rules:
- The same category never draws a duplicate entry in one round (compared by ID and by text after trimming extra whitespace, case-insensitive).
- If there are not enough entries, all available ones are drawn and a yellow notice is shown. Nothing is repeated to fill the gap, and nothing hangs.
- Disabled entries and disabled categories are never drawn.
- Every roll is recorded in the history automatically (as a text snapshot).

### 2.2 📺 Stream display

Press "Stream display" on the main screen to open a separate window meant for OBS "Window Capture":

- Large text, Fluent 3D emoji images, dark background, showing only the prompt.
- Stays in sync with the main screen. **Click a result in the display window to lock / unlock it.**
- `A−` / `A+` (or Ctrl + - / Ctrl + +) changes the text size, and the setting is remembered.
- Press **H** or use the right-click menu to hide the controls so only the prompt is visible.
- F5 rerolls unlocked items; Ctrl+F5 regenerates everything.

### 2.3 📝 Word bank

- **Categories**: the list on the left shows "drawable / total". "＋ New category" creates one. Select a category to edit its name, description, enabled state and default count, and use ↑ ↓ to reorder.
- **Deleting a category**: shows how many entries will be deleted with it and asks for confirmation; a backup is made first. No orphaned entries are left behind. To keep the entries, use "Move to" first.
- **Entry table**: filter by category, keyword (searches text / emoji / notes / tags) and enabled state. Multi-select (Ctrl / Shift) to Enable, Disable, Move to or Delete in bulk.
- **Entry form**: click an entry to edit it; "Clear / new entry" starts a new one. If the category already has the same entry, you get an instant warning and saving is refused.
- **📋 Paste multiple lines**: one entry per line, added to the current category in one go; the number to add and any duplicates are shown first.
- Every change is **written to disk immediately**, survives a restart and can be drawn right away.

### 2.4 ⭐ History & favorites

- Lists a snapshot of every roll, with a "Favorites only" filter.
- Copy, favorite / unfavorite, delete, or "Load into Roll" (everything is locked after loading; unlock what you want to replace, then reroll).
- Records keep a text snapshot from the moment of the roll, so editing or deleting entries later does not break them.
- Up to 300 non-favorite records are kept (older ones are removed automatically); favorites are kept forever.

### 2.5 💾 Data & backup

Shows the data location, import / export, the backup list, manual backup, restore, portable mode, emoji pack status and third-party licenses. See [section 4](#4-data-location-backup-and-restore).

### 2.6 Emoji display (Microsoft Fluent Emoji 3D)

Emoji in roll results, the stream display, the word bank table, the entry form preview, the import preview and the history list are **always shown as Fluent Emoji 3D images (WPF `Image`), not with the Windows Segoe UI Emoji font**. Windows 10 and Windows 11 therefore show the same pictures, and newer emoji that the Windows 10 font lacks (🫧, 🪞, 🪼…) display correctly.

- **The word bank still stores only the original Unicode strings**; images are used for display only. Copied prompts, exports and history are plain text.
- **The image pack is embedded in the EXE**: `fluent-emoji-3d.zip` (3,145 images, 128×128). Works offline with no network or extra files.
- **Lookup rules**: images are found by Unicode code point sequence, ignoring variation selectors (FE0E / FE0F), with full support for skin tone modifiers and ZWJ sequences (e.g. 🧑🏾‍🎨, 🏳️‍🌈, #️⃣, flags). A missing skin tone falls back to the default one.
- **When the pack has no image for a ZWJ sequence** (e.g. 👨‍👩‍👧), its parts are shown side by side (👨👩👧), as Unicode recommends.
- **When no image exists at all**: a "?" placeholder is shown; hovering it shows the code points and the file name to use for a custom image. The program does not crash.
- **Emoji inside ordinary text** are also replaced with images (e.g. "🕯️ Candle" from a TXT import), while ©, ®, ™ and text-style symbols without FE0F stay as text.
- **Emoji you add yourself** go through the same mechanism. The emoji field in the entry form shows a live preview of the actual image on the right, and below it explains which image is used, the code points, and which file name to add if none is found. The input box itself is still a normal Windows text box (WPF text boxes cannot contain images), so trust the preview.
- **Adding or replacing images**: put PNG files in the `EmojiOverrides\` folder (the "Data & backup" page has a button to open it). Name them with lowercase hexadecimal code points joined by `-`, without fe0f, e.g. 🫟 → `1fadf.png`, 👨‍👩‍👧 → `1f468-200d-1f469-200d-1f467.png`. These images take priority over the built-in pack and take effect after a restart.

### 2.7 🌐 Interface language

The interface is available in **English**, **Traditional Chinese** and **Japanese**. Switch with **🌐 Language / 介面語言** at the bottom of the sidebar. Your choice is saved in `settings.json`, and you are asked whether to restart now (the prompt is shown in both the current and the new language). If no language has been chosen, the Windows display language decides: Chinese (Traditional or Simplified) → Traditional Chinese, Japanese → Japanese, anything else → English. The Japanese UI uses a Japanese font (Yu Gothic UI) so kanji use Japanese glyph forms.

- Switching the language **changes only the interface**; it does not translate the word bank. The release ships with an empty word bank; import one in your own language (for example `Character_Design_Prompt_Library_v1_EN.csv` with the English UI).
- UI strings live in `src\CharacterPromptGenerator.Core\Localization\Strings.resx` (Traditional Chinese), `Strings.en.resx` (English) and `Strings.ja.resx` (Japanese). XAML uses `{l:T Key}`; code uses `L.T("Key")` / `L.F("Key", args)`. Unit tests check that all three languages have the same keys and `{0}` placeholders, and that every key used in the source exists.
- Import formats (CSV header, TXT, JSON) do not depend on the language. CSV headers can be in English (`category,text,...`) or Chinese (`分類,詞條,...`).

---

## 3. Word bank import formats

Press **📥 Import** and choose a file. The program parses it and **shows a preview first**: entries to add, new categories, skipped duplicates, error line numbers and a summary. Choose a mode, then press **✔ Apply import**. Nothing is changed before you apply; Cancel discards the import.

Sample files are in the `samples\` folder (their content is in Chinese). `samples\broken\` contains various broken files you can use to see the error messages.

### 3.1 Import modes

| Mode | Behavior |
|---|---|
| **Merge** (default) | Keeps all existing data and adds only new words. Duplicates are **skipped** by default, so notes on existing entries are never overwritten. You can choose whether new categories from the file may be created. |
| **Replace the entire word bank** | Replaces the whole word bank with the file contents. Requires two confirmations and makes a backup first. Blocked if the word bank would end up empty. |

**Duplicate detection**: within the same category, text is compared case-insensitively after trimming leading/trailing whitespace and collapsing runs of whitespace. Emoji sequences (ZWJ, skin tones, variation selectors) are never altered. Entries with the same text are allowed in different categories. Importing the same file twice does not duplicate entries.

**ID conflicts**: if an ID in a JSON file is already used by a different entry, the new entry gets a new ID automatically; existing data is unaffected and the preview mentions it.

### 3.2 JSON (full word bank, for backup and exchange)

```json
{
  "schemaVersion": 1,
  "categories": [
    { "id": "cat-occupation", "name": "Occupation", "description": "", "enabled": true, "sortOrder": 8, "defaultDrawCount": 1 }
  ],
  "entries": [
    { "id": "e-001", "categoryId": "cat-occupation", "text": "Book restorer", "emoji": "📚", "notes": "", "tags": ["craft"], "enabled": true },
    { "category": "Core imagery & emoji", "text": "Paper crane", "emoji": "🕊️", "tags": "origami|wish" }
  ]
}
```

- `schemaVersion`: currently 1. Newer versions than the program supports are rejected (update the program).
- `categories`: optional. `name` is required; `id` is recommended so a category stays the same across computers.
- `entries`: required. `text` is required; the category is given by `categoryId` (matching an id in categories) **or** `category` (the category name).
- `emoji`, `notes`, `tags` (an array or an `a|b` string), `enabled` and `id` are optional.
- `//` comments and trailing commas are allowed, to make hand-written word banks easier.
- "Export full JSON" followed by a Replace import loses no data at all.

### 3.3 CSV (for spreadsheet editing)

```csv
category,text,emoji,notes,tags
Core imagery & emoji,Candle,🕯️,Melting wax and flame,light|religion
Age,Middle-aged,,Roughly forty to fifty years old,
Occupation,"Storyteller, also sells medicine",📜,"Wrap a field in double quotes when it contains commas or line breaks",performance
```

- The first line must be the header; `category` and `text` are required, the others can be omitted or left empty. Chinese headers are also accepted: 分類, 詞條, 表情 (emoji), 備註 (notes), 標籤 (tags), 啟用 (enabled).
- An optional `enabled` column (true / false); "Export CSV" includes it.
- `tags` are separated by `|`.
- Full support for double quotes, commas and line breaks inside fields, `""` escapes and a UTF-8 BOM.
- **The file must be UTF-8.** In Excel, choose "CSV UTF-8 (Comma delimited)". Big5 / ANSI files are rejected with an explanation instead of importing garbled text.

### 3.4 TXT (quickly pasting many entries)

```text
# Lines starting with a hash are comments; empty lines are skipped
[Core imagery & emoji]
🕯️ Candle
🦋 Moth

[Occupation]
Watchmaker
Librarian
```

- `[Category name]` sets the category; each line below it is one entry, and **the whole line is the entry text** (emoji stay in the text and are not split into the emoji field).
- Lines before any `[category]` are reported as errors.

### 3.5 Error handling

| Situation | Result |
|---|---|
| Empty file, JSON syntax error, version too new, not a word bank file, not UTF-8, unclosed quote, missing CSV header | The whole file is rejected with the reason; the word bank is completely unchanged |
| Problems on individual lines (missing fields, too many fields, no category) | The line is listed under errors with its line number; the other lines can still be imported (you are asked again before applying) |
| The import result fails the integrity check, or writing the file fails | Nothing is written; the word bank stays as it was |
| The word bank changed after the preview | Applying is refused; import again to get a new preview |

### 3.6 Export

- **Export full JSON**: all categories (with description, state, order and default count) and entries (with ID, emoji, notes, tags and state).
- **Export CSV**: `category,text,emoji,notes,tags,enabled`, UTF-8 with BOM, opens directly in Excel. CSV does not include category descriptions or IDs; use JSON for a full backup.

---

## 4. Data location, backup and restore

### 4.1 Data location

| Mode | Location |
|---|---|
| Normal mode (default) | `%AppData%\CrossingDead\CharacterPromptGenerator\` |
| Portable mode | `Data\` next to the EXE (enabled when a `portable.flag` file is next to the EXE) |

The actual path is shown at the bottom of the sidebar and on the "Data & backup" page. Folder contents:

| File | Contents |
|---|---|
| `library.json` | Word bank (categories and entries) |
| `library.json.bak` | The version before the last save (automatic) |
| `presets.json` | Presets |
| `settings.json` | Category settings on the main screen, stream display text size, interface language, etc. |
| `history.json` | History and favorites |
| `Backups\` | Timestamped backups |
| `EmojiOverrides\` | (Optional) your own added or replacement emoji images, see section 2.6 |
| `error.log` | Log of unexpected errors (only appears if one happens) |

- Data is kept separate from the EXE: **to update the program, just replace the EXE; your word bank is not overwritten.**
- On first launch (no library.json and no backups), an **empty word bank** and empty presets are created; this never resets afterwards. Import a word bank yourself (see [section 1](#1-quick-start)).
- Portable mode: on the "Data & backup" page, press "Switch to portable mode". The program first checks that the EXE folder is writable (Program Files usually is not), then copies your current data there and creates `portable.flag`; it takes effect after a restart. Switching back to normal mode works the same way. Data in the original location is never deleted.

### 4.2 Backups

- **Automatic backups**: before a merge import, replacing the word bank, restoring, deleting a category, bulk deleting (20 or more) and bulk adding (50 or more). The latest 40 are kept.
- **Manual backups**: "💾 Back up now"; kept forever.
- Every save is written to a temporary file and flushed to disk first, and only then replaces the real file, keeping the previous version as `.bak`. A power cut or failure in the middle of saving never leaves half-written data.

### 4.3 Restore

On the "Data & backup" page, select a backup and press "↩ Restore selected backup". The category / entry counts of the backup and the current word bank are shown for comparison before restoring. The current word bank is backed up first, so a restore can itself be undone.

### 4.4 If files are damaged

- If `library.json` cannot be read, the original is kept as `library.corrupt-<date>.json` (not deleted), the word bank is restored from `.bak` or the latest backup, and you are told at startup.
- If no usable backup exists, the program stops and tells you; it **never** overwrites your data with an empty word bank.
- If the word bank file was written by a newer version (higher schemaVersion), it is not opened and not modified.
- Entries that point to a missing category are moved to an automatically created "Uncategorized (auto-repaired)" category (disabled by default) instead of being deleted.

---

## 5. Building the Windows EXE

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows).

### One-step build (build → test → publish)

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Output:
- `publish\CharacterPromptGenerator.exe` (about 103 MB, **the only file**)
- `dist\CharacterPromptGenerator_v0.3.0_win-x64.exe` (the same EXE with the release file name)

The EXE embeds the .NET runtime, the English language resources, the Fluent Emoji pack, `THIRD-PARTY-NOTICES.md` (viewable in the app under "Data & backup → Third-party licenses") and the empty default data. The build script checks that the output folder contains only the EXE.

### Manual commands

```powershell
dotnet restore
dotnet build -c Release
dotnet test tests\CharacterPromptGenerator.Tests -c Release
dotnet publish src\CharacterPromptGenerator.App\CharacterPromptGenerator.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

If the EXE in `publish\` is running, the build script stops and tells you; close the program first, or add `-Output <other folder>` to publish somewhere else.

Only the EXE needs to be distributed. To ship a word bank that is loaded on first launch, put `SeedData\default_library.json` (and `default_presets.json`) next to the EXE; the program uses them instead of the embedded empty data.

### Regenerating the emoji pack (optional)

`src\CharacterPromptGenerator.App\Emoji\fluent-emoji-3d.zip` is already generated and included in the project, so you normally do not need to rebuild it. To update the pack or use a higher resolution:

```powershell
# 1. Download the sources (3D PNGs only; Windows needs core.protectNTFS=false because the source repo has file names containing *, which are not checked out)
git clone --filter=blob:none --no-checkout https://github.com/shuding/fluentui-emoji-unicode.git shuding
cd shuding; git sparse-checkout init --no-cone
Set-Content .git\info\sparse-checkout "/LICENSE`n/assets/[0-9a-f]*_3d.png"
git -c core.protectNTFS=false checkout main; cd ..
git clone --filter=blob:none --no-checkout https://github.com/microsoft/fluentui-emoji.git microsoft
cd microsoft; git sparse-checkout set --no-cone "/LICENSE" "/assets/*/metadata.json" "/assets/**/3D/*.png"; git checkout main; cd ..

# 2. Build the pack (the last argument is the pixel size, default 160; this project uses 128; 256 suits 4K streams, at about 3× the file size)
dotnet run tools\BuildEmojiPack.cs -- shuding microsoft src\CharacterPromptGenerator.App\Emoji\fluent-emoji-3d.zip 128
```

The tool prefers the shuding version, fills in missing sequences using the official `metadata.json`, and writes the MIT license and source commits to `LICENSE.txt` and `manifest.json` inside the zip.

During development, run `dotnet run --project src\CharacterPromptGenerator.App`. To avoid touching your real data in AppData, put a `portable.flag` in `bin\Debug\net10.0-windows\` and test data will be written to `Data\` there.

---

## 6. Project structure

```
CharacterPromptGenerator.slnx
├─ src/CharacterPromptGenerator.Core/      UI-independent core logic (net10.0, unit-tested)
│  ├─ Models/        Category, Entry, LibraryData, Preset, SavedResult, AppSettings
│  ├─ Drawing/       DrawEngine (drawing, locking, rerolling), PromptFormatter (copy text, snapshots)
│  ├─ Library/       LibraryEditor (all editing operations), LibraryValidator (validation and load-time repair)
│  ├─ Import/        TextFileLoader (encoding), CsvParser, ImportReader (JSON/CSV/TXT),
│  │                 ImportPlanner (preview and merge), LibraryExporter
│  ├─ Storage/       AppStore (single entry point for data), AtomicFile, DataPaths, JSON options
│  ├─ Localization/  L (string lookup, language choice), Strings.resx (Traditional Chinese), Strings.en.resx (English)
│  ├─ Emoji/         EmojiText (grapheme splitting, code point normalization, ZWJ decomposition), EmojiLibrary (pack and override lookup)
│  └─ Text/          TextNormalizer (duplicate keys), IdGenerator
├─ src/CharacterPromptGenerator.App/       WPF UI (net10.0-windows, MVVM)
│  ├─ ViewModels/    Draw, Editor, History, Data, Import, Main
│  ├─ Views/         Pages, import preview, stream display, multi-line paste, input dialog
│  ├─ Controls/      EmojiTextBlock (text control that shows emoji as images)
│  ├─ Infrastructure/AppSession, Dialogs, RelayCommand, EmojiImages (image decoding and cache), TExtension ({l:T Key} in XAML)
│  ├─ Themes/Dark.xaml
│  ├─ Emoji/         fluent-emoji-3d.zip, LICENSE-fluentui-emoji.txt (embedded in the EXE)
│  └─ SeedData/      default_library.json, default_presets.json (empty defaults, embedded in the EXE)
├─ src/CharacterPromptGenerator.Mobile/    Android version (.NET MAUI), see docs/Android.md
├─ tests/CharacterPromptGenerator.Tests/   xUnit tests (185, including the Core features added for mobile); Fixtures/ is the test word bank (2,111 entries)
├─ samples/                                 Sample JSON / CSV / TXT import files and broken examples
├─ tools/BuildEmojiPack.cs                  Tool that builds the emoji pack
├─ THIRD-PARTY-NOTICES.md                   Third-party license notices
├─ build.ps1                               Windows EXE
├─ build-android.ps1                       Android APK
└─ CharacterPromptGenerator.Mobile.slnx    Core + Android + tests (the Windows .slnx excludes Android and needs no MAUI workload)
```

Inspired by Aurora's BnS Material Tracker: sidebar navigation with a dark theme, a built-in data editor, JSON data kept separate from the program, and user data in AppData. The original project was not modified.

**Visuals**: the program icon is `src\CharacterPromptGenerator.App\Assets\CharacterPromptGenerator.ico` (shared by the EXE file, title bar, taskbar and stream display window). The UI uses an amethyst palette taken from the icon. All colors are defined at the top of `Themes\Dark.xaml`, and every text/background pair meets WCAG AA contrast (≥ 4.5:1). Warnings, errors and success keep yellow, red and green for their meaning.

---

## 7. Design decisions

Where the specification was ambiguous, the option that cannot damage data was preferred:

1. **Every change is "copy → modify → validate → atomic write → swap"** (`AppStore.MutateLibrary`). If any step fails, both memory and files stay as they were. Imports work the same way: the preview computes the complete new word bank, and applying swaps it in whole.
2. **Categories are linked by stable IDs**, so renaming does not affect entries, presets or settings. CSV / TXT have no IDs and match categories by name (with the same normalization rules).
3. **Merge imports always skip duplicates.** There is no "overwrite" option, so hand-written notes are never silently replaced. To change many existing entries, export JSON → edit → import with Replace (a backup is made automatically).
4. **In TXT, the whole line is the entry text**; emoji and notes are not guessed (as specified). So "🕯️ Candle" from TXT is not a duplicate of an entry with text "Candle" and emoji 🕯️. Use CSV or JSON for a separate emoji field.
5. **Adding a single entry that duplicates one in the same category is refused** (not just warned about), keeping categories free of duplicates so rolls never repeat in practice.
6. **Disabled category vs. ticked on the main screen**: disabling a category in the word bank takes it out of rolling entirely for now; ticking on the main screen only decides whether to draw from it this time. Newly imported categories are ticked by default; categories a preset does not mention are unticked when applying it.
7. **Every roll is recorded in the history automatically** (and can be favorited), rather than only when you press Favorite, so good prompts are not lost when you forget to save them mid-stream.
8. **"Regenerate all" clears locks** (with a confirmation when anything is locked); only "Reroll unlocked" keeps them. Rerolls prefer words different from the previous round.
9. **Deleting a category deletes its entries too** (the count is shown in the confirmation and a backup is made first), instead of leaving orphaned entries.
10. **JSON is written as readable UTF-8**: a custom JSON encoder writes Chinese text and emoji as-is (System.Text.Json would otherwise write every emoji as two \u escape sequences), so word banks are easy to read and edit by hand.
11. **Emoji are shown as images, not with a font**: Segoe UI Emoji on Windows 10 lacks emoji from Emoji 13 onwards, and WPF does not support color fonts, so Microsoft Fluent Emoji 3D PNGs (MIT) are used instead. The pack is a single zip embedded in the EXE (not three thousand small files), so a release is just one file; individual images can be replaced through `EmojiOverrides\`. FE0E / FE0F are ignored when matching, because the source pack names files inconsistently with respect to variation selectors (e.g. ❤️ is `2764`, but some gendered ZWJ sequences keep `fe0f`). Images are 128×128 to balance sharpness and size (the 256×256 originals are about 113 MB).
    - Before switching to images, it was confirmed that the boxes seen on Windows 10 were **not data corruption**. All word bank, backup and history files are valid UTF-8 with no U+FFFD replacement characters, and the code points of 🫧 (U+1FAE7) and 🪞 (U+1FA9E) are stored intact; the font simply lacked them. Imports already reject U+FFFD, and the entry form warns if the input contains it.
12. **The default word bank is empty**: word banks are released separately per language (English / Traditional Chinese CSV) and imported by the user, so the EXE is not tied to the content of any one language.
13. **The interface language is chosen at startup** and changing it requires a restart, rather than switching live: every string is looked up once when a screen is built, so no ViewModel has to handle language-change notifications. The language setting is read before any data is loaded, so even data-loading messages use the right language.
14. **Pragmatic MVVM**: ViewModels call `Dialogs` directly for messages and file dialogs, without an extra service-interface layer; all core logic is in the Core project and covered by tests.

---

## 8. Testing

### 8.1 Automated tests (`dotnet test`, all 185 pass; the 115 original Windows tests are listed below, the ones added for mobile are in [docs/Android.md](docs/Android.md#7-測試清單))

| Area | What is verified |
|---|---|
| Drawing | No duplicates within a category (including different IDs with the same text); draws as many as possible with a notice when short; empty categories do not crash; disabled entries / categories are never drawn |
| Locking and rerolling | Locked items keep their position and content after a reroll; rerolling one category leaves others untouched; new words are preferred; unlocked items can change; locked content survives deleting the entry |
| Duplicate detection | Leading/trailing, repeated and full-width spaces, letter case; ZWJ, skin tone and variation-selector emoji are not altered |
| CSV | Quotes, commas and line breaks in fields, `""` escapes, BOM, correct line numbers, unclosed quotes, missing header, too many fields |
| TXT | Category sections, comments, empty lines, lines before any category |
| JSON | Syntax errors, non-object root, missing entries, newer version, category names instead of IDs, tags as a string |
| Encoding | Empty file, whitespace only, Big5 rejected, UTF-8 BOM, UTF-16 |
| Merge / replace | Importing the same file twice does not duplicate; same text in different categories allowed; case-insensitive duplicates; skipped when new categories are not allowed; new IDs on ID conflicts; existing notes not overwritten; replacing with an empty word bank blocked; broken files leave the word bank unchanged |
| Round trips | JSON export → import is identical; CSV export → import keeps text, emoji, notes (with commas, line breaks and quotes), tags and enabled state |
| Persistence | The release defaults start with an empty word bank and no warnings; the test word bank has ≥ 2,000 entries and ≥ 10 per category on first launch; a second launch does not overwrite; entries added through the UI layer survive a restart and can be drawn; emoji are written and read back exactly |
| Safety | Memory and files unchanged when a change fails midway; damaged library.json restored from .bak with the damaged file kept; newer schema refused; orphaned entries rescued; applying refused if the word bank changed after the preview; backup and restore; history snapshots intact after deleting a category; only non-favorite history is pruned |
| Sample files | The three files in `samples\` import correctly into the test word bank (including skipped duplicates and idempotence); every file in `samples\broken\` is rejected and leaves the word bank unchanged |
| Emoji normalization | With and without FE0F map to the same image; ZWJ + FE0F (🏳️‍🌈), skin tone + ZWJ (🧑🏾‍🎨), keycaps (1️⃣ #️⃣), flags; missing skin tones fall back to the default; ZWJ sequences split into parts |
| Emoji splitting | ZWJ / skin tone / flag sequences are single emoji; splitting and rejoining gives the original string; ©, ☂ without FE0F and FE0E text style are not treated as emoji; U+FFFD is not treated as emoji |
| Emoji pack | The shipped pack has ≥ 3,000 images, the MIT LICENSE and a manifest; 🫧 🪞 🪼 and other emoji missing from Windows 10 all have images; every emoji in the test word bank and sample files has an image; the pack opens from a stream (the embedded resource); overrides take priority; a missing or damaged pack reports an error instead of throwing |
| Localization | Traditional Chinese and English have the same keys and `{0}` placeholders; every key used in the source exists; the English satellite assembly loads; language code resolution; the language in `settings.json` can be read before opening the data and does not throw when the file is damaged |

### 8.2 Manual checks (on Windows 10)

- `dotnet build` of the whole solution: 0 errors, 0 warnings. `dotnet publish` outputs a single EXE (about 103 MB), confirmed to embed the emoji pack, third-party licenses, default data and the English language resources.
- The development build was launched with both the English and the Traditional Chinese UI, and all four pages (Roll, Word bank, History, Data & backup) were checked from screenshots; no English text is cut off.
- Checked by hand in the development build: rolling and showing results, the stream display window (large text, hidden controls), the word bank page (category list and counts, table, search bar, batch actions, loading an entry into the form by clicking it), and the Import button opening the file picker.
- Emoji images (Windows 10): in roll results, the stream display, the word bank table and the form preview, 🫧 🪞 🪼, 🧑🏾‍🎨 and 🏳️‍🌈 all show as 3D images; 👨‍👩‍👧 shows as three parts side by side; 🫟 from Emoji 16 shows the "?" placeholder; ☂️ in text becomes an image while ©2026 stays text; the form's explanation correctly reflects the lookup result.

### 8.3 Not yet checked by hand in the real UI

The core logic of the following features is covered by automated tests, but they **have not yet been clicked through in the real window**. It is worth trying them the first time you use the app:

- The full import preview window and the Apply flow (including the two confirmations in Replace mode)
- The Lock button, per-category Reroll, and saving / overwriting / renaming / deleting presets
- Loading from the History page into Roll, and toggling favorites
- Manual backup, restore, switching portable mode and the automatic restart on the Data page
- The language switch in the sidebar and its restart prompt, the import guide on the Roll page when the word bank is empty, and the "Third-party licenses" window
- The multi-line paste window, the category delete confirmation and moving entries

---

## 9. Not yet done and known limitations

**Left for later versions as specified**: native XLSX import, drag-and-drop import, an advanced column mapping tool, OBS plugin integration, animation, AI features and online sync.

**Known limitations**:

- **The emoji pack covers the current range of Fluent Emoji (about Emoji 15.1).** Newer emoji (such as 🫟 from Emoji 16) and sequences Fluent does not have are shown as "?"; you can add your own images through `EmojiOverrides\`.
- **Text boxes cannot contain images** (a WPF limitation): in text boxes other than the entry form, search box and import preview, newer emoji may still appear as boxes on Windows 10; trust the preview next to them. Icons in the sidebar and on buttons still use the system font (monochrome).
- The full content on the right of the History page is a selectable text box and also uses the system font; the list on the left uses images.
- Changing the interface language requires a restart. Word bank content is not translated with the interface language.
- Presets store category IDs only; settings for deleted categories are ignored automatically.
- Search is a simple "contains" match. With very large word banks (tens of thousands of entries), the table is rebuilt after each change, which may cause a short delay; performance has not been tuned yet.
- Single edits on the main screen have no undo; use automatic / manual backups.
- The final name has not been decided yet.
- The window title bar color is set by Windows "Settings → Personalization → Colors" (for example, the system accent color when "Show accent color on title bars" is on) and is not affected by the app theme.
