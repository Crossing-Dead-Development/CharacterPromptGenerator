# Character Design Prompt Generator｜角色設計題目產生器

**繁體中文** · [English](README.en.md)

**Crossing Dead Development · v0.2.0** · 介面支援繁體中文／English

繪圖直播用的離線角色設計抽題工具：從可自訂的分類詞庫隨機抽出關鍵字與 Emoji，組成角色設計練習題目。可以鎖定喜歡的結果、局部重抽、自訂抽題方案、保存歷史與收藏，並透過程式介面或 JSON／CSV／TXT 檔案維護大型詞庫。

> 這不是 AI 生圖或自動寫故事的工具。程式只負責抽題，詮釋完全交給畫師。完全離線、無帳號、無網路連線。

---

## 目錄

1. [快速開始](#1-快速開始)
2. [操作說明](#2-操作說明)
3. [詞庫匯入格式](#3-詞庫匯入格式)
4. [資料位置、備份與還原](#4-資料位置備份與還原)
5. [建置 Windows EXE](#5-建置-windows-exe)
6. [專案結構](#6-專案結構)
7. [設計決策](#7-設計決策)
8. [測試狀況](#8-測試狀況)
9. [尚未完成與已知限制](#9-尚未完成與已知限制)

---

## 1. 快速開始

1. 從 [Releases](https://github.com/Crossing-Dead-Development/CharacterPromptGenerator/releases/latest) 下載 `CharacterPromptGenerator_v*_win-x64.exe`，直接執行（免安裝、只有一個檔案，不需要另外安裝 .NET）。
2. 第一次啟動時詞庫是**空的**。從同一個 Release 下載詞庫檔，再匯入：
   - 繁體中文：`Character_Design_Prompt_Library_v1_TW.csv`
   - English：`Character_Design_Prompt_Library_v1_EN.csv`
3. 按抽題頁中央的「📥 匯入詞庫」（或「詞庫管理」／「資料與備份」頁的同名按鈕），選擇下載的 CSV。
4. 匯入預覽中選「合併匯入」，確認數量後按「✔ 套用匯入」。匯入前會自動備份，新詞條立刻可以抽到。
5. 在「🎲 抽題」頁勾選分類、設定數量，按 **「產生角色題目」**。

想換介面語言：左側欄下方的「🌐 介面語言 / Language」選單，重新啟動後生效。

系統需求：Windows 10／11 x64。

---

## 2. 操作說明

### 2.1 抽題（主畫面）

| 功能 | 說明 |
|---|---|
| 分類勾選與數量 | 左側每個分類可勾選並用 −／＋ 設定抽取數量（可為 0）。「可抽 N」是目前可抽的詞條數，數量超過時會變黃色提示。設定自動保存。 |
| 🎲 產生角色題目／全部重新產生 | 依左側設定重新抽出全部結果。若有鎖定項目，會先詢問（全部重新產生會清除鎖定）。 |
| 🔒 鎖定 | 每個結果下方的「鎖定」可個別切換。鎖定的結果在重抽時完全不變。 |
| 🔁 重抽未鎖定（F5） | 保留鎖定項目，其餘重新抽。會優先抽與上一輪不同的詞。 |
| 🎲 重抽此類 | 只重抽該分類中未鎖定的項目，其他分類不動。 |
| 🔓 解除全部鎖定 | 一次解除所有鎖定。 |
| 📋 複製題目（Ctrl+Shift+C） | 以「分類：詞1、詞2」格式複製到剪貼簿，含 Emoji。 |
| ☆ 收藏 | 把目前結果標記為收藏。收藏永遠保留。 |
| 抽題方案 | 選擇方案後按「套用」。「另存新方案」「覆寫」「改名」「刪除」可自由編輯方案。 |

抽題規則：
- 同一分類、同一輪不會抽到重複詞條（以 ID 及「去除多餘空白、不分大小寫」後的文字判斷）。
- 詞條不足時會抽出所有可用詞條並顯示黃色提示，不會重複湊數、不會卡住。
- 停用的詞條或停用的分類不會被抽到。
- 每次抽題都自動記錄到歷史（文字快照）。

### 2.2 📺 直播展示模式

主畫面按「直播展示模式」開啟獨立視窗，專供 OBS「視窗擷取」使用：

- 大字體、Fluent 3D Emoji 圖片、深色背景，只顯示題目。
- 與主畫面即時同步；在展示視窗中**點一下結果即可鎖定／解除**。
- `A−`／`A＋`（或 Ctrl + -／Ctrl + +）調整字體大小，設定會被記住。
- 按 **H** 或右鍵選單隱藏控制列，讓畫面只剩題目。
- F5 重抽未鎖定、Ctrl+F5 全部重新產生。

### 2.3 📝 詞庫管理

- **分類**：左側列表顯示「可抽數／總數」。「＋ 新增分類」建立分類；選取分類後可修改名稱、說明、是否啟用、預設抽取數量，並用 ↑↓ 調整順序。
- **刪除分類**：會顯示將一併刪除的詞條數並要求確認，刪除前自動備份。不會產生沒有分類的孤兒詞條。若想保留詞條，先把它們「移到分類」。
- **詞條表格**：可依分類、關鍵字（搜尋文字／Emoji／備註／標籤）及啟用狀態篩選。可多選（Ctrl／Shift）後批次「啟用」「停用」「移到分類」「刪除」。
- **詞條表單**：點選詞條即可編輯；按「清空／改為新增」新增詞條。同分類已有相同詞條時會即時警告並拒絕儲存。
- **📋 多行貼上新增**：每行一個詞條，一次加入目前分類；會先顯示將新增數量與重複項目。
- 所有修改**立即寫入檔案**，重開程式仍然存在，也立即可以被抽到。

### 2.4 ⭐ 歷史與收藏

- 列出每次抽題的快照，可「只看收藏」。
- 可複製、收藏／取消收藏、刪除，或「載入到抽題畫面」（載入後全部先鎖定，解除想換的項目再重抽）。
- 紀錄保存的是當下的文字快照：之後修改或刪除詞條，舊紀錄也不會壞掉。
- 未收藏的紀錄最多保留 300 筆（舊的自動淘汰），收藏永遠保留。

### 2.5 💾 資料與備份

顯示資料位置、匯入／匯出、備份清單、手動備份、還原、可攜模式切換、Emoji 圖庫狀態與第三方授權。見[第 4 節](#4-資料位置備份與還原)。

### 2.6 Emoji 顯示（Microsoft Fluent Emoji 3D）

抽題結果、直播展示、詞庫表格、詞條表單預覽、匯入預覽、歷史列表中的 Emoji，**一律用 Fluent Emoji 3D 圖片（WPF `Image`）顯示，不使用 Windows 的 Segoe UI Emoji 字型**。因此 Windows 10 與 Windows 11 看到的是同一張圖，Windows 10 字型沒有的新 Emoji（🫧、🪞、🪼…）也能正常顯示。

- **詞庫仍然只保存原始的 Unicode 字串**，圖片只用於顯示。複製題目、匯出、歷史紀錄都是原本的文字。
- **圖庫內嵌在 EXE 中**：`fluent-emoji-3d.zip`（3,145 張，128×128），離線即可使用，不需要網路或額外檔案。
- **查圖規則**：以 Unicode 碼點序列找圖，比對時忽略變體選擇符（FE0E／FE0F），完整支援膚色修飾符與 ZWJ 組合（例如 🧑🏾‍🎨、🏳️‍🌈、#️⃣、國旗）。找不到某個膚色時改用預設膚色。
- **圖庫沒有某個 ZWJ 組合時**（例如 👨‍👩‍👧），依 Unicode 建議改為並排顯示各組成元素（👨👩👧）。
- **完全找不到圖片時**：顯示「?」替代圖示，滑鼠移上去會說明碼點與補圖用的檔名，程式不會當機。
- **一般文字中的 Emoji** 也會換成圖片（例如 TXT 匯入的「🕯️ 蠟燭」），但 ©、®、™ 及沒有 FE0F 的文字樣式符號會維持文字，不會被誤換。
- **自己新增的 Emoji** 走同一套機制。詞條表單的 Emoji 欄位右側會即時預覽實際顯示的圖片，下方會說明使用哪張圖、碼點，以及找不到時要補哪個檔名。輸入框本身仍是 Windows 文字輸入（WPF 文字框無法放圖片），以預覽為準。
- **自行補圖或替換**：把 PNG 放到資料夾的 `EmojiOverrides\`（「資料與備份」頁有按鈕可開啟），檔名為「小寫十六進位碼點以 `-` 連接、去掉 fe0f」，例如 🫟 → `1fadf.png`、👨‍👩‍👧 → `1f468-200d-1f469-200d-1f467.png`。這裡的圖片優先於內建圖庫，重新啟動後生效。

### 2.7 🌐 介面語言

介面支援**繁體中文**與 **English**，在左側欄下方的「🌐 介面語言 / Language」切換。選擇後會存進 `settings.json`，並詢問是否立即重新啟動（提示同時以兩種語言顯示）。沒有設定過時，Windows 顯示語言是中文就用繁體中文，其他一律英文。

- 切換語言**只改變介面文字**，不會翻譯詞庫內容。英文介面請搭配英文詞庫（`Character_Design_Prompt_Library_v1_EN.csv`）。
- 介面文字放在 `src\CharacterPromptGenerator.Core\Localization\Strings.resx`（繁中）與 `Strings.en.resx`（英文）。XAML 用 `{l:T Key}`、程式用 `L.T("Key")`／`L.F("Key", 參數)` 取用；單元測試會檢查兩種語言的 key 與 `{0}` 參數一致，且原始碼用到的每個 key 都存在。
- 匯入格式（CSV 標頭、TXT、JSON）與語言無關；CSV 標頭可用英文（`category,text,...`）或中文（`分類,詞條,...`）。

---

## 3. 詞庫匯入格式

按「📥 匯入詞庫」選擇檔案 → 程式解析並**先顯示預覽**（將新增的詞條、新分類、略過的重複項、錯誤行號、總數摘要）→ 選擇模式 → 「✔ 套用匯入」。在套用之前不會修改任何資料，按「取消」即放棄。

範例檔在 `samples\` 資料夾，`samples\broken\` 內是各種錯誤檔案，可以用來確認錯誤提示。

### 3.1 匯入模式

| 模式 | 行為 |
|---|---|
| **合併匯入**（預設） | 保留所有現有資料，只加入新詞。重複詞預設**略過**，不會覆寫既有詞條的備註。可勾選是否允許建立檔案中的新分類。 |
| **取代整個詞庫** | 整個詞庫換成檔案內容。需要兩次確認，套用前自動備份。若取代後詞庫是空的會被阻止。 |

**重複判斷**：同一分類下，去除首尾空白、連續空白壓成一個後，不分大小寫比較文字。Emoji 序列（ZWJ、膚色、變體選擇符）不會被修改。不同分類允許同名詞條。同一份檔案重複匯入不會造成詞條倍增。

**ID 衝突**：JSON 中的 ID 若已被不同內容的詞條使用，會自動給新詞條新的 ID，舊資料不受影響，並在預覽中提示。

### 3.2 JSON（完整詞庫，備份與交換用）

```json
{
  "schemaVersion": 1,
  "categories": [
    { "id": "cat-occupation", "name": "職業／身分", "description": "", "enabled": true, "sortOrder": 8, "defaultDrawCount": 1 }
  ],
  "entries": [
    { "id": "e-001", "categoryId": "cat-occupation", "text": "古書修復師", "emoji": "📚", "notes": "", "tags": ["工匠"], "enabled": true },
    { "category": "核心意象與 Emoji", "text": "紙鶴", "emoji": "🕊️", "tags": "摺紙|祈願" }
  ]
}
```

- `schemaVersion`：目前為 1。比程式新的版本會被拒絕（請更新程式）。
- `categories`：選填。`name` 必填；`id` 建議填，用於跨電腦保持同一分類。
- `entries`：必填。`text` 必填；分類用 `categoryId`（對應 categories 的 id）**或** `category`（分類名稱）指定。
- `emoji`、`notes`、`tags`（陣列或 `a|b` 字串）、`enabled`、`id` 皆為選填。
- 允許 `//` 註解與結尾逗號，方便手寫大型詞庫。
- 「匯出完整 JSON」後再匯入（取代模式）完全不遺失資料。

### 3.3 CSV（試算表編輯用）

```csv
category,text,emoji,notes,tags
核心意象與 Emoji,蠟燭,🕯️,融蠟和火焰,光源|宗教
年齡／年齡感,中年,,角色年齡約四十至五十歲,
職業／身分,"說書人, 兼賣藥",📜,"欄位內有逗號或換行時，用雙引號包住",表演
```

- 第一行必須是標頭；`category`、`text` 必填，其他可省略或留空。標頭也接受中文：分類、詞條、Emoji→表情、備註、標籤、啟用。
- 選填 `enabled` 欄（true／false），「匯出 CSV」會附上這欄。
- `tags` 以 `|` 分隔。
- 完整支援雙引號、欄位內逗號與換行、`""` 跳脫、UTF-8 BOM。
- **必須是 UTF-8**。Excel 請選「CSV UTF-8（逗號分隔）」。Big5／ANSI 檔會被拒絕並提示，不會匯入亂碼。

### 3.4 TXT（快速貼上大量詞條）

```text
# 井號開頭是註解，空行會被略過
[核心意象與 Emoji]
🕯️ 蠟燭
🦋 飛蛾

[職業／身分]
鐘錶匠
圖書管理員
```

- `[分類名稱]` 指定分類，下面每行一個詞條，**整行就是詞條文字**（Emoji 會保留在文字裡，不會拆成 Emoji 欄位）。
- 寫在任何 `[分類]` 之前的行會被列為錯誤。

### 3.5 錯誤處理

| 狀況 | 結果 |
|---|---|
| 空檔、JSON 語法錯誤、版本太新、不是詞庫檔、非 UTF-8、引號沒結束、CSV 缺標頭 | 整份拒絕，顯示原因，詞庫完全不變 |
| 個別行有問題（缺欄位、欄位過多、沒指定分類） | 該行列入「錯誤」並標示行號，其他行仍可匯入（套用前會再確認一次） |
| 匯入結果未通過完整性檢查、寫檔失敗 | 不寫入任何資料，詞庫保持原狀 |
| 預覽之後詞庫又被修改 | 拒絕套用，請重新匯入以取得新預覽 |

### 3.6 匯出

- **匯出完整 JSON**：所有分類（含說明、狀態、順序、預設數量）及詞條（含 ID、Emoji、備註、標籤、狀態）。
- **匯出 CSV**：`category,text,emoji,notes,tags,enabled`，UTF-8 含 BOM，可直接用 Excel 開啟。CSV 不含分類說明與 ID，完整備份請用 JSON。

---

## 4. 資料位置、備份與還原

### 4.1 資料位置

| 模式 | 位置 |
|---|---|
| 一般模式（預設） | `%AppData%\CrossingDead\CharacterPromptGenerator\` |
| 可攜模式 | EXE 同一層的 `Data\`（EXE 旁有 `portable.flag` 檔時啟用） |

實際路徑顯示在側邊欄下方與「資料與備份」頁。資料夾內容：

| 檔案 | 內容 |
|---|---|
| `library.json` | 詞庫（分類與詞條） |
| `library.json.bak` | 上一次存檔前的版本（自動） |
| `presets.json` | 抽題方案 |
| `settings.json` | 主畫面分類設定、展示模式字體、介面語言等 |
| `history.json` | 歷史與收藏 |
| `Backups\` | 時間戳記備份 |
| `EmojiOverrides\` | （選用）自行補充或替換的 Emoji 圖片，見 2.6 節 |
| `error.log` | 未預期錯誤的紀錄（有發生時才會出現） |

- 資料與 EXE 分離：**更新程式只要替換 EXE，詞庫不會被覆蓋**。
- 第一次啟動（找不到 library.json 且沒有任何備份）時建立**空白詞庫**與空白方案；之後不會再重置。詞庫請自行匯入（見[第 1 節](#1-快速開始)）。
- 可攜模式：在「資料與備份」頁按「切換為可攜模式」，程式會先檢查 EXE 資料夾能否寫入（Program Files 通常不行），再把目前資料複製過去並建立 `portable.flag`，重新啟動後生效。切回一般模式同理。原位置的資料都不會被刪除。

### 4.2 備份

- **自動備份**：合併匯入前、取代詞庫前、還原前、刪除分類前、大量刪除（20 筆以上）前、大量新增（50 筆以上）前。保留最近 40 份。
- **手動備份**：「💾 立即備份」，永遠保留。
- 每次存檔都先寫暫存檔、刷到磁碟，成功後才替換正式檔，並保留上一版為 `.bak`。存檔中途斷電或失敗，不會留下半套資料。

### 4.3 還原

「資料與備份」頁選擇一筆備份 →「↩ 還原選取的備份」。會顯示備份與目前詞庫的分類／詞條數比較，確認後還原；還原前會先備份目前詞庫，所以還原本身也可以復原。

### 4.4 檔案損壞時

- `library.json` 無法讀取時：原檔另存為 `library.corrupt-日期.json`（不刪除），自動改從 `.bak` 或最新的備份還原，並在啟動時告知。
- 找不到任何可用備份時：程式停止並告知，**不會**用空白詞庫覆蓋。
- 詞庫檔由較新版程式寫入時（schemaVersion 較高）：拒絕開啟，不修改檔案。
- 讀取時發現指向不存在分類的詞條：移到自動建立的「未分類（自動修復）」分類（預設停用），不會刪除。

---

## 5. 建置 Windows EXE

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（Windows）。

### 一鍵建置（建置 → 測試 → 發行）

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

產出：
- `publish\CharacterPromptGenerator.exe`（約 103 MB，**只有這一個檔案**）
- `dist\CharacterPromptGenerator_v0.2.0_win-x64.exe`（同一個 EXE，發行用檔名）

EXE 內嵌了 .NET runtime、英文語系資源、Fluent Emoji 圖庫、`THIRD-PARTY-NOTICES.md`（程式內「資料與備份 → 第三方授權」可檢視）與空白的預設資料。建置腳本會檢查輸出資料夾只有 EXE。

### 手動指令

```powershell
dotnet restore
dotnet build -c Release
dotnet test tests\CharacterPromptGenerator.Tests -c Release
dotnet publish src\CharacterPromptGenerator.App\CharacterPromptGenerator.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

如果 `publish\` 裡的 EXE 正在執行，建置腳本會停下來提示；請先關閉程式，或加上 `-Output 其他資料夾` 輸出到別處。

發行時只需要提供 EXE。若要讓首次啟動自帶某份詞庫，可在 EXE 旁放 `SeedData\default_library.json`（與 `default_presets.json`），程式會優先使用它們，而不是內嵌的空白資料。

### 重新產生 Emoji 圖庫（選用）

`src\CharacterPromptGenerator.App\Emoji\fluent-emoji-3d.zip` 已經產生好並納入專案，平常不需要重做。要更新圖庫或改用更高解析度時：

```powershell
# 1. 下載來源（只抓 3D PNG；Windows 需要 core.protectNTFS=false，因為原庫有含 * 的檔名，但不會被取出）
git clone --filter=blob:none --no-checkout https://github.com/shuding/fluentui-emoji-unicode.git shuding
cd shuding; git sparse-checkout init --no-cone
Set-Content .git\info\sparse-checkout "/LICENSE`n/assets/[0-9a-f]*_3d.png"
git -c core.protectNTFS=false checkout main; cd ..
git clone --filter=blob:none --no-checkout https://github.com/microsoft/fluentui-emoji.git microsoft
cd microsoft; git sparse-checkout set --no-cone "/LICENSE" "/assets/*/metadata.json" "/assets/**/3D/*.png"; git checkout main; cd ..

# 2. 產生圖庫（最後一個參數是像素，預設 160；本專案使用 128，4K 直播可改 256，檔案約變為 3 倍）
dotnet run tools\BuildEmojiPack.cs -- shuding microsoft src\CharacterPromptGenerator.App\Emoji\fluent-emoji-3d.zip 128
```

工具會優先使用 shuding 版本，再依官方 `metadata.json` 補上缺少的序列，並把 MIT 授權與來源 commit 寫入 zip 內的 `LICENSE.txt`、`manifest.json`。

開發時執行：`dotnet run --project src\CharacterPromptGenerator.App`。如果不想動到 AppData 的正式資料，在 `bin\Debug\net10.0-windows\` 放一個 `portable.flag`，測試資料就會寫到該處的 `Data\`。

---

## 6. 專案結構

```
CharacterPromptGenerator.slnx
├─ src/CharacterPromptGenerator.Core/      與 UI 無關的核心邏輯（net10.0，可單元測試）
│  ├─ Models/        Category、Entry、LibraryData、Preset、SavedResult、AppSettings
│  ├─ Drawing/       DrawEngine（抽選、鎖定、重抽）、PromptFormatter（複製文字、快照）
│  ├─ Library/       LibraryEditor（所有編輯操作）、LibraryValidator（驗證與載入修復）
│  ├─ Import/        TextFileLoader（編碼）、CsvParser、ImportReader（JSON/CSV/TXT）、
│  │                 ImportPlanner（預覽與合併）、LibraryExporter
│  ├─ Storage/       AppStore（唯一資料入口）、AtomicFile、DataPaths、JSON 設定
│  ├─ Localization/  L（取字串、語言選擇）、Strings.resx（繁中）、Strings.en.resx（英文）
│  ├─ Emoji/         EmojiText（字素切分、碼點正規化、ZWJ 拆解）、EmojiLibrary（圖庫與自訂圖查詢）
│  └─ Text/          TextNormalizer（重複判斷鍵）、IdGenerator
├─ src/CharacterPromptGenerator.App/       WPF 介面（net10.0-windows，MVVM）
│  ├─ ViewModels/    Draw、Editor、History、Data、Import、Main
│  ├─ Views/         各頁面、匯入預覽、直播展示、多行貼上、輸入對話框
│  ├─ Controls/      EmojiTextBlock（以 Image 顯示 Emoji 的文字控制項）
│  ├─ Infrastructure/AppSession、Dialogs、RelayCommand、EmojiImages（圖片解碼與快取）、TExtension（XAML 的 {l:T Key}）
│  ├─ Themes/Dark.xaml
│  ├─ Emoji/         fluent-emoji-3d.zip、LICENSE-fluentui-emoji.txt（內嵌進 EXE）
│  └─ SeedData/      default_library.json、default_presets.json（空白預設資料，內嵌進 EXE）
├─ tests/CharacterPromptGenerator.Tests/   xUnit 測試（115 項）；Fixtures/ 是測試用詞庫（2111 筆）
├─ samples/                                 JSON／CSV／TXT 範例匯入檔與錯誤範例
├─ tools/BuildEmojiPack.cs                  產生 Emoji 圖庫的工具
├─ THIRD-PARTY-NOTICES.md                   第三方素材授權聲明
└─ build.ps1
```

參考了 Aurora's BnS Material Tracker 的做法：側邊欄導覽＋深色主題、內建資料編輯器、JSON 資料與程式分離、使用者資料放 AppData。原專案未做任何修改。

**視覺**：程式圖示為 `src\CharacterPromptGenerator.App\Assets\CharacterPromptGenerator.ico`（EXE 檔案、視窗標題列、工作列、直播展示視窗共用）。介面採用取自圖示的紫水晶色調，所有顏色集中在 `Themes\Dark.xaml` 開頭，文字與背景的對比皆符合 WCAG AA（≥ 4.5:1）。警告、錯誤、成功維持黃、紅、綠，以保留語意。

---

## 7. 設計決策

遇到規格有歧義時，優先選擇「不破壞資料」的做法：

1. **所有修改都是「複製 → 修改 → 驗證 → 原子寫檔 → 替換」**（`AppStore.MutateLibrary`）。任何一步失敗，記憶體與檔案都維持原狀。匯入也一樣：預覽階段算出完整的新詞庫，套用時整份替換。
2. **分類以穩定 ID 關聯**，改名不影響詞條、方案、設定。CSV／TXT 沒有 ID，以分類名稱（同樣的正規化規則）比對。
3. **合併匯入遇到重複時一律略過**，不提供「覆寫」選項，避免無聲蓋掉手寫的備註。要大幅修改既有詞條，請匯出 JSON → 編輯 → 用「取代」匯入（會自動備份）。
4. **TXT 整行就是詞條文字**，不猜測 Emoji 或備註（依規格）。因此 TXT 的「🕯️ 蠟燭」與詞庫裡 text=「蠟燭」、emoji=🕯️ 的詞條不會被視為重複。需要分開的 Emoji 欄位請用 CSV 或 JSON。
5. **單筆新增遇到同分類重複詞時拒絕儲存**（而不只是警告），維持「同分類不重複」的資料品質，抽題時也就不會出現實質重複。
6. **分類停用 vs. 主畫面勾選**：詞庫中的「停用」代表這個分類暫時完全不參與抽題；主畫面的勾選只是這次要不要抽。新匯入的分類預設會被勾選，套用方案時方案沒提到的分類則不勾選。
7. **歷史自動記錄每次抽題**（可收藏），而不是只在按收藏時才保存，以免直播中忘記保存好題目。
8. **「全部重新產生」會清除鎖定**（有鎖定時先確認），「重抽未鎖定」才保留鎖定。重抽時優先抽與上一輪不同的詞。
9. **刪除分類會一併刪除其詞條**（確認時顯示數量、事先備份），而不是保留孤兒詞條。
10. **JSON 使用可閱讀的 UTF-8**：自訂 JSON 編碼器讓中文與 Emoji 原樣寫入（System.Text.Json 預設會把每個 Emoji 寫成兩組 \u 開頭的跳脫碼），方便日後直接閱讀或手寫詞庫。
11. **Emoji 用圖片顯示，不靠字型**：Windows 10 的 Segoe UI Emoji 缺少 Emoji 13 以後的字，WPF 也不支援彩色字型，所以改用 Microsoft Fluent Emoji 3D PNG（MIT）。圖庫打包成一個 zip 內嵌在 EXE 中（不拆成三千個小檔），發行只需要一個檔案；要替換個別圖片可用 `EmojiOverrides\`。比對時去掉 FE0E／FE0F，因為原始圖庫的檔名對變體選擇符的處理並不一致（例如 ❤️ 是 `2764`，但部分性別 ZWJ 序列保留 `fe0f`）。圖片為 128×128，以平衡清晰度與檔案大小（256×256 原圖約 113 MB）。
    - 開始改用圖片前已先確認：先前在 Windows 10 上看到的方框**不是資料損壞**。所有詞庫、備份、歷史檔案都是有效的 UTF-8，沒有 U+FFFD 替代字元，🫧（U+1FAE7）、🪞（U+1FA9E）的碼點完整保存，只是字型缺字。匯入時遇到 U+FFFD 本來就會拒絕；詞條表單若輸入含 U+FFFD 的內容也會警告。
12. **預設詞庫為空白**：詞庫依語言分開發行（繁中／英文 CSV），由使用者自行匯入，EXE 不綁定任何一種語言的內容。
13. **介面語言在啟動時決定**，切換後重新啟動，而不是即時切換：所有字串只需在建立畫面時取用一次，不必在每個 ViewModel 處理語言變更通知；語言設定在載入任何資料之前就先讀出，連資料載入的提示訊息也使用正確的語言。
14. **務實的 MVVM**：ViewModel 直接呼叫 `Dialogs` 顯示訊息與檔案對話框，沒有再抽一層服務介面；核心邏輯全在 Core 專案並有測試。

---

## 8. 測試狀況

### 8.1 自動化測試（`dotnet test`，115 項全部通過）

| 範圍 | 驗證內容 |
|---|---|
| 抽選 | 同分類不重複（含不同 ID 但文字相同）、數量不足時抽出最大可用並提示、空分類不崩潰、停用詞條／分類不會被抽到 |
| 鎖定與重抽 | 鎖定項目重抽後位置與內容不變、單一分類重抽不影響其他分類、優先抽新詞、解鎖後可以換掉、鎖定內容在詞條刪除後仍保留 |
| 重複判斷 | 首尾／連續／全形空白、大小寫；ZWJ、膚色、變體選擇符 Emoji 不被破壞 |
| CSV | 引號、欄位內逗號與換行、`""` 跳脫、BOM、行號正確、未結束引號、缺標頭、欄位過多 |
| TXT | 分類段落、註解、空行、分類前的孤行 |
| JSON | 語法錯誤、根節點不是物件、缺 entries、版本過新、用分類名稱代替 ID、tags 字串格式 |
| 編碼 | 空檔、全空白、Big5 被拒、UTF-8 BOM、UTF-16 |
| 合併／取代 | 同檔重複合併不倍增、跨分類同名允許、不分大小寫重複、不允許新分類時略過、ID 衝突改發新 ID、不覆寫既有備註、取代成空詞庫被阻止、壞檔不改變詞庫 |
| 可逆性 | JSON 匯出→匯入完全相同；CSV 匯出→匯入保留文字、Emoji、備註（含逗號換行引號）、標籤、啟用狀態 |
| 持久性 | 發行版首次啟動為空白詞庫且無警告；測試詞庫首次啟動 ≥2000 筆、每類 ≥10 筆；第二次啟動不覆蓋；UI 層新增的詞條重開後存在且可抽到；Emoji 原樣寫入檔案並正確讀回 |
| 防呆 | 修改中途失敗時記憶體與檔案都不變、library.json 損壞時從 .bak 還原並保留損壞檔、新版 schema 拒絕開啟、孤兒詞條被救回、預覽後詞庫變動則拒絕套用、備份與還原、歷史快照在分類刪除後仍完整、歷史只淘汰未收藏 |
| 範例檔 | `samples\` 三個範例檔對測試詞庫實際匯入（含重複略過與冪等）、`samples\broken\` 各錯誤檔被正確拒絕且詞庫不變 |
| Emoji 正規化 | FE0F 有無都對到同一張圖、ZWJ＋FE0F（🏳️‍🌈）、膚色＋ZWJ（🧑🏾‍🎨）、鍵帽（1️⃣ #️⃣）、國旗；缺少的膚色改用預設膚色；ZWJ 組合拆成元素 |
| Emoji 切分 | ZWJ／膚色／國旗為單一 Emoji；切分後重新串接與原字串完全相同；©、沒有 FE0F 的 ☂、FE0E 文字樣式不被當成 Emoji；U+FFFD 不被當成 Emoji |
| Emoji 圖庫 | 實際發行的圖庫有 ≥3000 張圖、MIT LICENSE 與 manifest；🫧 🪞 🪼 等 Windows 10 缺字的 Emoji 都有圖；測試詞庫與所有範例檔中的每個 Emoji 都有圖；可從資料流（內嵌資源）開啟；自訂圖優先；圖庫遺失或損壞時回報錯誤而不丟例外 |
| 語系 | 繁中／英文 key 與 `{0}` 參數一致；原始碼用到的每個 key 都存在；英文附屬組件可載入；語言代碼解析；`settings.json` 的語言設定可在開啟資料前讀出、損壞時不丟例外 |

### 8.2 實際執行驗證（在本機 Windows 10 上）

- `dotnet build` 全方案 0 錯誤 0 警告；`dotnet publish` 輸出只有單一 EXE（約 103 MB），已確認內嵌 Emoji 圖庫、第三方授權、預設資料與英文語系資源。
- 開發版以英文與繁中介面實際啟動並截圖檢查四個頁面（抽題、詞庫管理、歷史、資料與備份），英文文字沒有被截斷。
- 開發版實際操作確認：主畫面抽題與結果顯示、直播展示視窗（大字體、隱藏控制列）、詞庫管理頁（分類列表與計數、表格、搜尋列、批次操作列、點選詞條載入表單）、匯入按鈕開啟檔案選擇視窗。
- Emoji 圖片（Windows 10）：抽題結果、直播展示、詞庫表格與表單預覽中，🫧 🪞 🪼、🧑🏾‍🎨、🏳️‍🌈 都正確顯示為 3D 圖片；👨‍👩‍👧 並排顯示為三個元素；Emoji 16 的 🫟 顯示「?」替代圖示；文字中的 ☂️ 換成圖片而 ©2026 維持文字；表單的說明文字正確反映查圖結果。

### 8.3 尚未以實際介面操作逐項確認

以下功能的核心邏輯都有自動化測試，但**還沒有在真實視窗中逐一點過**，建議第一次使用時試一下：

- 匯入預覽視窗的完整畫面與「套用」流程（含取代模式的兩次確認）
- 鎖定按鈕、「重抽此類」、方案的另存／覆寫／改名／刪除
- 歷史頁的載入到抽題畫面、收藏切換
- 資料頁的手動備份、還原、可攜模式切換與自動重新啟動
- 側欄的語言切換與重新啟動提示、空白詞庫時抽題頁的匯入引導、「第三方授權」視窗
- 多行貼上視窗、分類刪除確認、詞條移動

---

## 9. 尚未完成與已知限制

**依規格留待之後的版本**：XLSX 原生匯入、拖放檔案匯入、進階欄位對應工具、OBS 外掛整合、動畫、AI 相關功能、線上同步。

**已知限制**：

- **Emoji 圖庫涵蓋到 Fluent Emoji 目前的範圍（約 Emoji 15.1）**。更新的 Emoji（例如 Emoji 16 的 🫟）與 Fluent 沒有的組合會顯示「?」，可以用 `EmojiOverrides\` 自行補圖。
- **文字輸入框不能放圖片**（WPF 限制）：詞條表單、搜尋框、匯入預覽以外的文字框中，新 Emoji 在 Windows 10 仍可能顯示為方框，請以旁邊的預覽為準。側邊欄、按鈕上的介面圖示仍使用系統字型（單色）。
- 歷史頁右側的完整內容是可選取複製的文字框，同樣使用系統字型；左側列表已改用圖片。
- 介面語言切換後需要重新啟動。詞庫內容不會隨介面語言翻譯。
- 方案只記錄分類 ID；刪除分類後，方案中對應的設定會自動忽略。
- 搜尋是單純的「包含」比對；大型詞庫（數萬筆）時每次修改會重建表格，可能有短暫延遲，尚未做效能調校。
- 主畫面的單筆編輯沒有「復原」功能；請利用自動／手動備份。
- 正式名稱尚未決定。
- 視窗標題列的顏色由 Windows「設定 → 個人化 → 色彩」決定（例如開啟「在標題列顯示輔色」時會是系統輔色），不受程式主題影響。
