using System.Globalization;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Models;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>提供首次啟動用的內建示範資料（來自 EXE 旁的 JSON 或內嵌資源）。</summary>
public interface ISeedSource
{
    string? ReadLibraryJson();
    string? ReadPresetsJson();
}

/// <summary>無法安全開啟資料時丟出；呼叫端應停止並提示，絕不覆寫現有檔案。</summary>
public sealed class StoreLoadException(string message) : Exception(message);

public sealed record BackupInfo(string Path, DateTime Time, string Reason, long Size)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// 所有使用者資料的單一入口。每個修改都是「複製 → 修改 → 驗證 → 原子寫檔 → 換掉記憶體中的資料」，
/// 任何一步失敗都不會留下半套資料。
/// </summary>
public sealed class AppStore
{
    public const int MaxAutoBackups = 40;
    const string ManualReason = "manual";

    readonly ISeedSource _seed;

    public DataPaths Paths { get; }
    public LibraryData Library { get; private set; } = new();
    public List<Preset> Presets { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();
    public List<SavedResult> History { get; private set; } = new();
    public List<string> LoadMessages { get; } = new();
    public bool FirstRun { get; private set; }

    public event EventHandler? LibraryChanged;

    AppStore(DataPaths paths, ISeedSource seed)
    {
        Paths = paths;
        _seed = seed;
    }

    public static AppStore Open(DataPaths paths, ISeedSource seed)
    {
        Directory.CreateDirectory(paths.DataDir);
        Directory.CreateDirectory(paths.BackupDir);
        var store = new AppStore(paths, seed);
        if (paths.Notice is not null) store.LoadMessages.Add(paths.Notice);
        store.LoadLibrary();
        store.LoadPresets();
        store.LoadSettings();
        store.LoadHistory();
        return store;
    }

    // ── 詞庫 ───────────────────────────────────────────────

    void LoadLibrary()
    {
        var file = Paths.LibraryFile;
        if (File.Exists(file))
        {
            if (TryReadLibrary(file, out var lib, out var error))
            {
                AdoptLoaded(lib!, "library.json");
                return;
            }
            var corrupt = PreserveCorrupt(file);
            LoadMessages.Add($"詞庫檔無法讀取（{error}）。原檔已保留為 {Path.GetFileName(corrupt)}。");
            if (TryRecoverFromBackups()) return;
            throw new StoreLoadException(
                $"詞庫檔 library.json 損壞，且找不到可用的備份。\n\n錯誤：{error}\n\n" +
                $"原檔已複製為：{corrupt}\n請手動檢查資料夾：{Paths.DataDir}");
        }

        if (File.Exists(file + ".bak") || ListBackups().Count > 0)
        {
            LoadMessages.Add("找不到 library.json，但有備份存在，嘗試從備份還原。");
            if (TryRecoverFromBackups()) return;
            throw new StoreLoadException($"找不到 library.json，備份也無法讀取。請檢查資料夾：{Paths.DataDir}");
        }

        // 真正的首次啟動
        FirstRun = true;
        LibraryData seedLib;
        var json = _seed.ReadLibraryJson();
        var parsed = json is null ? null : ImportReader.ReadJson(json);
        if (parsed is null || parsed.IsFatal)
        {
            LoadMessages.Add("找不到內建示範詞庫或格式錯誤，已建立空白詞庫。");
            seedLib = new LibraryData();
        }
        else
        {
            var plan = ImportPlanner.Build(new LibraryData(), parsed, new ImportOptions { Mode = ImportMode.Replace });
            seedLib = plan.ResultLibrary;
        }
        LibraryValidator.Repair(seedLib);
        LibraryValidator.EnsureValid(seedLib);
        AtomicFile.WriteAllText(Paths.LibraryFile, JsonOptions.Serialize(seedLib));
        Library = seedLib;
    }

    bool TryReadLibrary(string file, out LibraryData? lib, out string? error)
    {
        lib = null;
        error = null;
        try
        {
            var json = File.ReadAllText(file);
            using (var doc = System.Text.Json.JsonDocument.Parse(json, new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true }))
            {
                if (doc.RootElement.TryGetProperty("schemaVersion", out var v) && v.TryGetInt32(out var ver) &&
                    ver > LibraryData.CurrentSchemaVersion)
                {
                    // 新版程式寫的資料：不可降級覆寫
                    throw new StoreLoadException(
                        $"詞庫檔的格式版本為 {ver}，比這個程式支援的 {LibraryData.CurrentSchemaVersion} 還新。\n" +
                        "請改用較新版的程式開啟。為保護資料，本程式不會修改這個檔案。");
                }
            }
            lib = JsonOptions.Deserialize<LibraryData>(json);
            if (lib is null) { error = "檔案內容為空"; return false; }
            return true;
        }
        catch (StoreLoadException) { throw; }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    void AdoptLoaded(LibraryData lib, string source)
    {
        lib.SchemaVersion = LibraryData.CurrentSchemaVersion;
        var notes = LibraryValidator.Repair(lib);
        Library = lib;
        if (notes.Count > 0)
        {
            LoadMessages.Add($"讀取 {source} 時做了自動修復：");
            LoadMessages.AddRange(notes.Select(n => "・" + n));
            if (File.Exists(Paths.LibraryFile))
                File.Copy(Paths.LibraryFile, BackupPath("before-repair"), overwrite: false);
            AtomicFile.WriteAllText(Paths.LibraryFile, JsonOptions.Serialize(Library));
        }
    }

    bool TryRecoverFromBackups()
    {
        var candidates = new List<string>();
        if (File.Exists(Paths.LibraryFile + ".bak")) candidates.Add(Paths.LibraryFile + ".bak");
        candidates.AddRange(ListBackups().Select(b => b.Path));
        foreach (var path in candidates)
        {
            if (!TryReadLibrary(path, out var lib, out _)) continue;
            LoadMessages.Add($"已從備份還原詞庫：{Path.GetFileName(path)}");
            LibraryValidator.Repair(lib!);
            lib!.SchemaVersion = LibraryData.CurrentSchemaVersion;
            AtomicFile.WriteAllText(Paths.LibraryFile, JsonOptions.Serialize(lib), keepBak: false);
            Library = lib;
            return true;
        }
        return false;
    }

    string PreserveCorrupt(string file)
    {
        var target = Path.Combine(Paths.DataDir,
            $"{Path.GetFileNameWithoutExtension(file)}.corrupt-{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(file)}");
        File.Copy(file, target, overwrite: true);
        return target;
    }

    /// <summary>在複本上修改詞庫，驗證並存檔成功後才生效。</summary>
    public T MutateLibrary<T>(Func<LibraryData, T> mutate, string? backupReason = null)
    {
        var copy = Library.Clone();
        var result = mutate(copy);
        Commit(copy, backupReason);
        return result;
    }

    public void MutateLibrary(Action<LibraryData> mutate, string? backupReason = null) =>
        MutateLibrary(lib => { mutate(lib); return 0; }, backupReason);

    /// <summary>整庫替換（匯入、還原用）。一定會先備份目前詞庫。</summary>
    public void ReplaceLibrary(LibraryData newLibrary, string backupReason)
    {
        Commit(newLibrary.Clone(), backupReason);
    }

    void Commit(LibraryData next, string? backupReason)
    {
        next.SchemaVersion = LibraryData.CurrentSchemaVersion;
        next.ExportedAt = null;
        LibraryValidator.EnsureValid(next);
        if (backupReason is not null) CreateBackup(backupReason);
        AtomicFile.WriteAllText(Paths.LibraryFile, JsonOptions.Serialize(next));
        Library = next;
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>套用匯入計畫。計畫若是依舊版詞庫算出來的，拒絕套用以免覆蓋期間的修改。</summary>
    public void ApplyImport(ImportPlan plan)
    {
        if (!plan.CanApply) throw new LibraryValidationException(plan.BlockReason ?? "這份匯入計畫無法套用。");
        if (!ReferenceEquals(plan.BaseLibrary, Library))
            throw new LibraryValidationException("詞庫在預覽之後已被修改，請重新匯入以產生新的預覽。");
        ReplaceLibrary(plan.ResultLibrary, plan.Mode == ImportMode.Replace ? "before-replace" : "before-import");
    }

    // ── 備份 ───────────────────────────────────────────────

    string BackupPath(string reason)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(Paths.BackupDir, $"library_{stamp}_{reason}.json");
        var n = 2;
        while (File.Exists(path)) path = Path.Combine(Paths.BackupDir, $"library_{stamp}_{reason}_{n++}.json");
        return path;
    }

    /// <summary>把目前的詞庫存一份到 Backups\。手動備份永遠保留，自動備份只留最近的數十份。</summary>
    public string CreateBackup(string reason)
    {
        Directory.CreateDirectory(Paths.BackupDir);
        var path = BackupPath(reason);
        AtomicFile.WriteAllText(path, JsonOptions.Serialize(Library), keepBak: false);
        PruneBackups();
        return path;
    }

    public string CreateManualBackup() => CreateBackup(ManualReason);

    void PruneBackups()
    {
        var auto = ListBackups().Where(b => b.Reason != ManualReason).Skip(MaxAutoBackups).ToList();
        foreach (var b in auto)
        {
            try { File.Delete(b.Path); } catch { /* 刪不掉就留著 */ }
        }
    }

    public List<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(Paths.BackupDir)) return new();
        var list = new List<BackupInfo>();
        foreach (var path in Directory.GetFiles(Paths.BackupDir, "library_*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(path); // library_yyyyMMdd_HHmmss_fff_reason
            var parts = name.Split('_');
            var time = File.GetLastWriteTime(path);
            var reason = "";
            if (parts.Length >= 5 &&
                DateTime.TryParseExact($"{parts[1]}_{parts[2]}_{parts[3]}", "yyyyMMdd_HHmmss_fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                time = parsed;
                reason = parts[4];
            }
            list.Add(new BackupInfo(path, time, reason, new FileInfo(path).Length));
        }
        return list.OrderByDescending(b => b.Time).ToList();
    }

    /// <summary>讀取備份檔並驗證，不修改目前資料。</summary>
    public LibraryData ReadBackup(string path)
    {
        if (!TryReadLibrary(path, out var lib, out var error))
            throw new LibraryValidationException($"備份檔無法讀取：{error}");
        LibraryValidator.Repair(lib!);
        lib!.SchemaVersion = LibraryData.CurrentSchemaVersion;
        LibraryValidator.EnsureValid(lib);
        return lib;
    }

    public void RestoreBackup(string path) => ReplaceLibrary(ReadBackup(path), "before-restore");

    // ── 方案 ───────────────────────────────────────────────

    void LoadPresets()
    {
        var file = Paths.PresetsFile;
        if (File.Exists(file))
        {
            try
            {
                var pf = JsonOptions.Deserialize<PresetFile>(File.ReadAllText(file));
                if (pf?.Presets is not null)
                {
                    Presets = pf.Presets.Where(p => p is not null).ToList();
                    return;
                }
            }
            catch (Exception ex)
            {
                var corrupt = PreserveCorrupt(file);
                LoadMessages.Add($"方案檔無法讀取（{ex.Message}），已保留為 {Path.GetFileName(corrupt)} 並改用內建方案。");
            }
        }
        try
        {
            var json = _seed.ReadPresetsJson();
            Presets = (json is null ? null : JsonOptions.Deserialize<PresetFile>(json))?.Presets ?? new();
        }
        catch
        {
            Presets = new();
        }
        SavePresets();
    }

    public void SavePresets() =>
        AtomicFile.WriteAllText(Paths.PresetsFile, JsonOptions.Serialize(new PresetFile { Presets = Presets }));

    // ── 設定 ───────────────────────────────────────────────

    void LoadSettings()
    {
        var file = Paths.SettingsFile;
        if (File.Exists(file))
        {
            try
            {
                var s = JsonOptions.Deserialize<AppSettings>(File.ReadAllText(file));
                if (s is not null)
                {
                    s.DrawSettings ??= new();
                    Settings = s;
                    return;
                }
            }
            catch (Exception ex)
            {
                PreserveCorrupt(file);
                LoadMessages.Add($"設定檔無法讀取（{ex.Message}），已改用預設設定。");
            }
        }
        Settings = new AppSettings();
        // 首次使用：以「標準」方案（或第一個方案）作為主畫面初始設定
        var preset = Presets.FirstOrDefault(p => p.Id == "preset-standard") ?? Presets.FirstOrDefault();
        if (preset is not null) ApplyPresetToSettings(preset);
        SaveSettings();
    }

    public void SaveSettings() => AtomicFile.WriteAllText(Paths.SettingsFile, JsonOptions.Serialize(Settings));

    public DrawSetting GetDrawSetting(Category cat) =>
        Settings.DrawSettings.TryGetValue(cat.Id, out var s)
            ? s
            : new DrawSetting { Enabled = cat.Enabled && cat.DefaultDrawCount > 0, Count = cat.DefaultDrawCount };

    public void ApplyPresetToSettings(Preset preset)
    {
        foreach (var cat in Library.Categories)
        {
            var item = preset.Items.FirstOrDefault(i => i.CategoryId == cat.Id);
            Settings.DrawSettings[cat.Id] = item is null
                ? new DrawSetting { Enabled = false, Count = Math.Max(1, cat.DefaultDrawCount) }
                : new DrawSetting { Enabled = item.Enabled, Count = Math.Max(0, item.Count) };
        }
        Settings.LastPresetId = preset.Id;
    }

    public Preset PresetFromSettings(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Items = Library.OrderedCategories().Select(c =>
        {
            var s = GetDrawSetting(c);
            return new PresetItem { CategoryId = c.Id, Enabled = s.Enabled, Count = s.Count };
        }).ToList(),
    };

    // ── 歷史／收藏 ─────────────────────────────────────────

    void LoadHistory()
    {
        var file = Paths.HistoryFile;
        if (!File.Exists(file)) return;
        try
        {
            History = JsonOptions.Deserialize<HistoryFile>(File.ReadAllText(file))?.Results?.Where(r => r is not null).ToList() ?? new();
        }
        catch (Exception ex)
        {
            var corrupt = PreserveCorrupt(file);
            LoadMessages.Add($"歷史紀錄檔無法讀取（{ex.Message}），已保留為 {Path.GetFileName(corrupt)}。");
            History = new();
        }
    }

    public void SaveHistory() =>
        AtomicFile.WriteAllText(Paths.HistoryFile, JsonOptions.Serialize(new HistoryFile { Results = History }));

    public void AddHistory(SavedResult result)
    {
        History.Insert(0, result);
        // 只淘汰未收藏的舊紀錄；收藏永遠保留
        var limit = Math.Max(10, Settings.HistoryLimit);
        var nonFav = 0;
        History.RemoveAll(r => !r.Favorite && ++nonFav > limit);
        SaveHistory();
    }
}
