# Android 版｜角色設計題目產生器

Windows 版的手機移植，使用同一套抽題規則與資料格式（共用 `CharacterPromptGenerator.Core`）。完全離線，不需要帳號、伺服器或任何權限。

> iOS 版已於 2026-10-22 取消，本專案只提供 Android 版。

| 項目 | 內容 |
| --- | --- |
| 最低版本 | Android 8.0（API 26） |
| 目標版本 | Android 16（API 36） |
| 安裝檔 | `dist\CharacterPromptGenerator_v<版本>_android.apk`，約 28 MB |
| 權限 | 無（不要求網路、儲存或任何使用者權限） |
| 介面語言 | 系統語言是中文（繁體、簡體都算）時顯示繁體中文，其他一律英文；可在「設定」手動切換 |
| 預設詞庫 | 空白，與 Windows 版發行版相同。詞庫由使用者自行匯入 |

---

## 1. 安裝

1. 把 APK 傳到手機（USB、雲端硬碟、即時通訊都可以）。
2. 在手機上點開 APK。第一次安裝時，Android 會要求允許「安裝未知應用程式」，請對你用來開啟 APK 的 App（例如「檔案」或瀏覽器）允許一次。
3. **更新版本**：直接安裝新的 APK 即可，詞庫、方案、歷史都會保留。
   - 新舊版本必須使用同一把簽署金鑰（見第 5 節），否則 Android 會拒絕安裝。

## 2. 第一次使用

App 一開始是空的。到「設定」→「📥 匯入詞庫」，選擇詞庫檔：

- JSON：完整詞庫（含分類設定與 ID），用於備份與跨裝置交換
- CSV：`category,text,emoji,notes,tags`，可用試算表編輯
- TXT：一行 `[分類名稱]`，下面每行一個詞條；`#` 開頭為註解

格式與 Windows 版完全相同，詳見 [README 第 3 節](../README.md#3-詞庫匯入格式)。匯入前會先預覽（新增、新分類、略過、錯誤）、檢查重複，並在套用前自動備份。

## 3. 畫面說明

| 分頁 | 內容 |
| --- | --- |
| 抽題 | 依分類顯示結果，點一下項目鎖定／解除；單一分類重抽；複製、分享、收藏；右上角「⚙ 抽題設定」調整分類與數量、管理方案；⋮ 選單有「全部重新產生」「解除全部鎖定」 |
| 詞庫 | 分類列表 → 詞條列表（搜尋、篩選、多選批次啟用／停用／移動／刪除）→ 詞條編輯；「⋯」選單有多行貼上新增與分類設定 |
| 歷史 | 抽題當下的文字快照；只看收藏；點一下可複製、分享、收藏、載入到抽題畫面、刪除 |
| 設定 | 匯入、匯出 JSON／CSV、完整備份與還原、App 內的備份、介面語言、版本資訊 |

## 4. 在 Windows 與手機之間搬資料

### 4.1 Windows → 手機

**只搬詞庫**：Windows 版「資料與備份」→「📤 匯出完整 JSON」，把檔案傳到手機，在 App「設定」→「匯入詞庫」。

**連方案、歷史、收藏一起搬**：Windows 版沒有完整備份功能，請手動打包：

1. 在 Windows 版「資料與備份」按「📂 開啟資料夾」（一般是 `%AppData%\CrossingDead\CharacterPromptGenerator\`）。
2. 選取 `library.json`、`presets.json`、`history.json`、`settings.json` 四個檔案 → 右鍵「傳送到 → 壓縮的 (zipped) 資料夾」。
3. 把 zip 傳到手機，在 App「設定」→「↩ 從完整備份還原」。

App 會先驗證整份 zip，任何一個檔案有問題就整份拒絕；還原前會把手機上目前的全部資料另存一份在 App 內。介面語言保留手機上的設定。

### 4.2 手機 → Windows

**只搬詞庫**：App「設定」→「📤 匯出 JSON」，在 Windows 版「匯入詞庫」。

**全部資料**：App「設定」→「📦 匯出完整備份」得到一個 zip。關閉 Windows 版，把 zip 中的四個 JSON 檔解壓縮，覆蓋到 Windows 版的資料夾（建議先把原本的資料夾複製一份）。`manifest.json` 不需要。

### 4.3 換手機

舊手機「📦 匯出完整備份」→ 存到雲端硬碟或電腦 → 新手機安裝 App →「↩ 從完整備份還原」。

Android 的系統自動備份（Google 帳號）也有開啟，但不保證每台手機都會還原，請以完整備份 zip 為主。

## 5. 建置 APK

### 5.1 需要的環境（全部免費）

| 項目 | 說明 |
| --- | --- |
| .NET SDK 10 | `dotnet --version` |
| maui-android workload | `dotnet workload install maui-android` |
| Microsoft Build of OpenJDK 21 | 預設尋找 `%LOCALAPPDATA%\Microsoft\OpenJDK\jdk-21*`；也可以用 `-JavaSdk` 或環境變數 `CPG_JAVA_SDK` 指定。不支援 JDK 27 |
| Android SDK | platform 36、build-tools 36；預設 `C:\Program Files (x86)\Android\android-sdk`，或用 `-AndroidSdk`／`CPG_ANDROID_SDK` 指定 |

專案路徑不可以有中文等非 ASCII 字元（Android 的 aapt2 會失敗，錯誤 APT2265）。

### 5.2 指令

```powershell
# 正式版：測試 → 建置 → 用正式金鑰簽署 → dist\CharacterPromptGenerator_v<版本>_android.apk
powershell -ExecutionPolicy Bypass -File build-android.ps1

# 加上 -Install 直接安裝到已連接的手機或模擬器（保留資料）
powershell -ExecutionPolicy Bypass -File build-android.ps1 -Install

# 偵錯版（.NET 偵錯金鑰簽署，dist\..._android_debug.apk）
powershell -ExecutionPolicy Bypass -File build-android.ps1 -Configuration Debug -SkipTests
```

Windows 版的 `build.ps1` 不受影響，也不需要 MAUI workload。

### 5.3 簽署金鑰（重要）

正式版 APK 用這把金鑰簽署：

| 項目 | 內容 |
| --- | --- |
| 金鑰檔 | `%USERPROFILE%\.crossingdead\cpg-release.keystore`（PKCS12，RSA 4096，有效 10000 天） |
| 別名 | `cpg` |
| 密碼 | `%USERPROFILE%\.crossingdead\cpg-release.keystore.password.txt`，或環境變數 `CPG_KEYSTORE_PASS` |
| 憑證 SHA-256 | `98:6A:8A:B7:39:C1:17:A6:F4:92:53:BA:81:C2:76:CC:82:69:3A:B4:E1:E9:C0:7D:EC:AF:24:E0:91:B1:D3:A6` |

**請把金鑰檔和密碼備份到安全的地方（例如加密的隨身碟或密碼管理器）。** Android 只接受用同一把金鑰簽署的更新；金鑰遺失後，使用者必須先解除安裝（資料會一併刪除）才能安裝新版本。金鑰不在 git repo 裡，也不要放進 repo。

換一台電腦建置時，把這兩個檔案複製到該電腦的 `%USERPROFILE%\.crossingdead\`，或用 `-Keystore <路徑>` 與 `CPG_KEYSTORE_PASS` 指定。

如果要重新產生金鑰（只有在還沒發佈給任何人之前才可以這樣做）：

```powershell
& "$env:LOCALAPPDATA\Microsoft\OpenJDK\jdk-21.0.12.1+1\bin\keytool.exe" -genkeypair -keystore "$env:USERPROFILE\.crossingdead\cpg-release.keystore" `
  -storetype PKCS12 -alias cpg -keyalg RSA -keysize 4096 -validity 10000 -dname "CN=Crossing Dead Development, O=Crossing Dead, C=TW"
```

### 5.4 版本號

在 `src\CharacterPromptGenerator.Mobile\CharacterPromptGenerator.Mobile.csproj`：

- `ApplicationDisplayVersion`：顯示用版本（例如 `0.2.0`），也用在 APK 檔名
- `ApplicationVersion`：整數，**每次發佈新版都要加 1**，否則 Android 不會當成更新

## 6. 專案結構

```
CharacterPromptGenerator.Mobile.slnx      Core + Mobile + Tests
build-android.ps1                         Android 建置、簽署、安裝
src/CharacterPromptGenerator.Core/        與 Windows 版共用（抽題、匯入匯出、驗證、備份、多語系）
src/CharacterPromptGenerator.Mobile/      .NET MAUI（只有 net10.0-android）
├─ Platforms/Android/                     AndroidManifest、未預期錯誤攔截
├─ Views/                                 抽題、抽題設定、詞庫、詞條、詞條編輯、多行貼上、分類設定、
│                                         歷史、設定、匯入預覽、App 內備份
├─ ViewModels/                            畫面狀態；規則一律呼叫 Core
├─ Services/                              AppServices（資料入口）、Dialogs、FileExchange（檔案選擇器、儲存、分享）
└─ Resources/                             圖示、啟動畫面、深色主題
```

首次啟動的預設詞庫直接引用 Windows 版的 `src\CharacterPromptGenerator.App\SeedData\`（空白），兩邊不會各自分岔。

資料位置：App 專屬儲存區（`/data/data/com.crossingdead.characterpromptgenerator/files/`），檔案與 Windows 版相同：`library.json`、`presets.json`、`history.json`、`settings.json`、`Backups\`。

## 7. 測試清單

### 7.1 自動化測試（`dotnet test tests\CharacterPromptGenerator.Tests`，182 項全部通過）

原有 115 項（抽題引擎、匯入、Emoji、儲存、多語系、範例壞檔）之外，為行動版新增：

| 檔案 | 內容 |
| --- | --- |
| `MobileCoreTests.cs` | Source Generator 序列化與原本輸出逐字相同、未登記型別不會改走反射；`ReadBytes`（BOM、非 UTF-8、空檔、錯誤副檔名）；`DrawSession`（鎖定重抽、單分類重抽、刪除分類、從歷史載入、收藏、恢復預設）；`PresetManager` |
| `DataManagementTests.cs` | `LibraryQuery` 搜尋與篩選；匯出位元組可原樣再匯入；完整備份往返、還原前安全備份、損毀或新版備份被拒絕、手動壓縮的 Windows 資料夾可還原 |
| `HistoryManagerTests.cs` | 收藏、刪除、清除未收藏、存檔失敗時還原、歷史快照不受詞庫修改影響 |
| `LocalizationTests.cs`（新增部分） | 系統語言判斷：繁中、簡中、港、新加坡 → 中文；英、日、韓、法、空白 → 英文；使用者選擇優先 |

### 7.2 在 Android 模擬器上實際操作（Pixel 7、Android 15、正式版 APK）

| 驗收標準（企劃書第八節） | 結果 |
| --- | --- |
| 1. 首次啟動正常載入既有大型詞庫 | ✅ Windows 版 2,111 筆詞庫直接讀取 |
| 2. 離線抽題 | ✅ APK 沒有網路權限 |
| 3. 鎖定後重抽，鎖定內容不變 | ✅ |
| 4. 新增詞條後立即可抽 | ✅ 在空白的正式版中新增分類與詞條（以注音輸入法輸入非 ASCII 文字），切到抽題分頁立即抽到 |
| 5. 關閉並重開 App 資料仍在 | ✅ |
| 6. JSON／CSV／TXT 處理繁中、日文、Emoji | ✅ 含 ZWJ Emoji 👨‍👩‍👧 與含逗號、引號的欄位 |
| 7. Windows 匯出的詞庫可在手機匯入 | ✅ |
| 8. 手機匯出的備份可交換使用 | ✅ 完整備份 zip 解壓後與 Windows 資料夾格式相同（Android 之間的還原已實測） |
| 9. 損毀或無效的資料不破壞資料 | ✅ 損毀檔自動從備份還原並保留原檔；新版格式的檔案拒絕開啟、檔案雜湊值不變；壞掉的匯入檔被擋下 |
| 10. 不同尺寸螢幕文字與按鈕不重疊 | ✅ 1080×2400 與 720×1280（360dp 寬）＋系統字體 1.5 倍 |
| 11. 不依賴外部網路 | ✅ |
| 12. 更新版本後資料不被初始化 | ✅ 覆蓋安裝（`adb install -r`）後資料保留 |

其他：系統分享面板、系統檔案選擇器與「儲存檔案」、介面語言即時切換、系統語言自動判斷（繁中、簡中 → 中文；日文、韓文 → 英文）。

## 8. 尚未完成與已知限制

- 只在模擬器上測試，**尚未在實體手機上測試**。建議實機確認：匯入、匯出到雲端硬碟、分享、中文輸入搜尋、多行貼上。
- 中文搜尋與多行貼上的實際輸入只做了畫面檢查（模擬器透過指令輸入完整中文詞很不方便），邏輯由自動化測試涵蓋。
- Windows 版沒有「還原完整備份」的選單；從手機搬全部資料到 Windows 需手動解壓縮（第 4.2 節）。
- 沒有上架 Google Play。目前以 APK 直接安裝。
- 抽題畫面上尚未收藏的這一輪結果不會跨 App 重新啟動保留（與 Windows 版相同），但每次抽題都已自動寫入歷史，可從歷史載入。
