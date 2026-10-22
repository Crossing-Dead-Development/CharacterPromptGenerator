using System.Collections.ObjectModel;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CharacterPromptGenerator.Mobile.ViewModels;

/// <summary>設定分頁：匯入、匯出、完整備份、App 內備份、介面語言與版本資訊。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    readonly AppServices app;

    public SettingsViewModel(AppServices app)
    {
        this.app = app;
        app.DataChanged += OnDataChanged;
    }

    void OnDataChanged(object? sender, EventArgs e) => Load();

    AppStore Store => app.Store;

    public string Stats
    {
        get
        {
            var lib = Store.Library;
            return L.F("Data_Stats", lib.Categories.Count, lib.Entries.Count, lib.Entries.Count(e => e.Enabled), Store.Presets.Count, Store.History.Count);
        }
    }

    public string FullBackupStatus => Store.Settings.LastFullBackupAt is { } t ? L.F("Mobile_LastFullBackup", t) : L.T("Mobile_NeverFullBackup");
    public bool NeverBackedUp => Store.Settings.LastFullBackupAt is null;
    public string BackupsButtonText => L.F("Mobile_InternalBackupsCount", Store.ListBackups().Count + Store.ListFullBackups().Count);
    public string LanguageText => L.T("Main_LanguageTitle") + L.T("Prompt_CategorySep") + (L.Languages.FirstOrDefault(l => l.Code == Store.Settings.Language)?.DisplayName ?? L.T("Mobile_LanguageSystem"));
    public string AppVersionText => L.F("Mobile_AppVersion", AppInfo.Current.VersionString);
    public string DataVersionText => L.F("Mobile_DataVersion", LibraryData.CurrentSchemaVersion);

    public void Load()
    {
        foreach (var name in new[] { nameof(Stats), nameof(FullBackupStatus), nameof(NeverBackedUp), nameof(BackupsButtonText), nameof(LanguageText) })
            OnPropertyChanged(name);
    }

    // ── 匯入／匯出詞庫 ─────────────────────────────────────

    [RelayCommand]
    async Task Import()
    {
        ImportDocument doc;
        try
        {
            var file = await FileExchange.PickWordBank();
            if (file is null) return;
            doc = ImportReader.ReadBytes(file.FileName, file.Bytes);
        }
        catch (Exception ex)
        {
            await Dialogs.Error(L.F("Mobile_PickFailed", ex.Message));
            return;
        }
        await Shell.Current.GoToAsync(ImportPreviewPage.Route, new Dictionary<string, object> { ["document"] = doc });
    }

    [RelayCommand]
    Task ExportJson() => Export(LibraryExporter.SuggestedFileName("json", DateTime.Now), LibraryExporter.ToJsonBytes(Store.Library));

    [RelayCommand]
    Task ExportCsv() => Export(LibraryExporter.SuggestedFileName("csv", DateTime.Now), LibraryExporter.ToCsvBytes(Store.Library));

    /// <summary>寫出到使用者選的位置；成功時回傳 true，取消或失敗時回傳 false。</summary>
    static async Task<bool> Export(string fileName, byte[] content)
    {
        try
        {
            var path = await FileExchange.Save(fileName, content);
            if (path is null) return false;
            await Dialogs.Toast(L.F("Mobile_Exported", path));
            return true;
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Mobile_ExportFailed", ex.Message));
            return false;
        }
    }

    // ── 完整備份 ───────────────────────────────────────────

    [RelayCommand]
    async Task ExportFullBackup()
    {
        using var ms = new MemoryStream();
        FullBackup.Write(Store, ms, DeviceInfo.Current.Platform.ToString().ToLowerInvariant(), AppInfo.Current.VersionString);
        if (!await Export(FullBackup.SuggestedFileName(DateTime.Now), ms.ToArray())) return;
        Store.Settings.LastFullBackupAt = DateTime.Now;
        await app.TryAsync(Store.SaveSettings, L.T("What_Settings"));
        Load();
    }

    [RelayCommand]
    async Task RestoreFullBackup()
    {
        FullBackupContent content;
        try
        {
            var file = await FileExchange.PickBackup();
            if (file is null) return;
            content = FullBackup.Read(new MemoryStream(file.Bytes));
        }
        catch (Exception ex)
        {
            await Dialogs.Error(ex is LibraryValidationException ? ex.Message : L.F("Mobile_PickFailed", ex.Message));
            return;
        }

        var m = content.Manifest;
        var lib = Store.Library;
        if (!await Dialogs.Confirm(
                L.F("Mobile_ConfirmRestoreFull", m.CreatedAt,
                    content.Library.Categories.Count, content.Library.Entries.Count, content.Presets.Count, content.History.Count,
                    lib.Categories.Count, lib.Entries.Count, Store.Presets.Count, Store.History.Count),
                L.T("Data_RestoreTitle")))
            return;
        try
        {
            Store.RestoreFull(content);
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Data_RestoreFailed", ex.Message));
            return;
        }
        Load();
        await Dialogs.Info(L.T("Mobile_RestoredFull"));
    }

    [RelayCommand]
    Task OpenBackups() => Shell.Current.GoToAsync(BackupsPage.Route);

    // ── 介面語言 ───────────────────────────────────────────

    /// <summary>切換後立刻重建畫面（不需要重新啟動），抽題結果保留。</summary>
    [RelayCommand]
    async Task ChooseLanguage()
    {
        var options = new List<string> { L.T("Mobile_LanguageSystem") };
        options.AddRange(L.Languages.Select(l => l.DisplayName));
        var index = await Dialogs.Choose(L.T("Main_LanguageTitle"), options);
        if (index < 0) return;
        var code = index == 0 ? null : L.Languages[index - 1].Code;
        if (code == Store.Settings.Language) return;
        var old = Store.Settings.Language;
        Store.Settings.Language = code;
        if (!await app.TryAsync(Store.SaveSettings, L.T("What_Settings")))
        {
            Store.Settings.Language = old;
            return;
        }
        L.Use(code);
        Application.Current!.Windows[0].Page = new AppShell([]);
        await Shell.Current.GoToAsync("//settings");
    }
}

/// <summary>匯入預覽：選擇合併或取代，看清楚會新增、略過什麼之後才套用。</summary>
public sealed partial class ImportPreviewViewModel(AppServices app) : ObservableObject, IQueryAttributable
{
    ImportDocument? _doc;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _doc = query.TryGetValue("document", out var d) ? d as ImportDocument : null;
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FormatText));
        OnPropertyChanged(nameof(FatalError));
        OnPropertyChanged(nameof(IsFatal));
        OnPropertyChanged(nameof(IsNotFatal));
        OnPropertyChanged(nameof(ShowNewCategoryOption));
        _ = RebuildPlan();
    }

    public string FileName => _doc?.SourcePath ?? "";
    public string FormatText => _doc is null ? "" : L.T("Import_FormatLabel") + _doc.Format switch
    {
        ImportFormat.Json => L.T("Import_FormatJson"),
        ImportFormat.Csv => L.T("Import_FormatCsv"),
        _ => L.T("Import_FormatTxt"),
    };
    public string? FatalError => _doc?.FatalError;
    public bool IsFatal => _doc?.IsFatal == true;
    public bool IsNotFatal => !IsFatal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsBusy { get; set; } = true;

    public bool IsReplace
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsMerge));
            OnPropertyChanged(nameof(ShowNewCategoryOption));
            _ = RebuildPlan();
        }
    }

    public bool IsMerge => !IsReplace;
    public bool ShowNewCategoryOption => IsMerge && IsNotFatal;

    [RelayCommand]
    void UseMerge() => IsReplace = false;

    [RelayCommand]
    void UseReplace() => IsReplace = true;

    public bool AllowNewCategories
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) _ = RebuildPlan();
        }
    } = true;

    public ImportPlan? Plan { get; private set; }

    public string SummaryText => _doc is null || Plan is null || _doc.IsFatal
        ? ""
        : L.F("Import_Summary", _doc.Rows.Count, Plan.Added.Count, Plan.NewCategories.Count, Plan.Skipped.Count, Plan.Errors.Count);

    public string? BlockReason => _doc?.IsFatal == true ? null : Plan?.BlockReason;
    public bool HasBlockReason => BlockReason is not null;
    public string ReplaceWarning => IsReplace && Plan is not null ? L.F("Import_ReplaceWarning", Plan.RemovedEntries) : "";
    public bool HasReplaceWarning => ReplaceWarning.Length > 0;

    // ── 明細分頁 ───────────────────────────────────────────

    public int Tab
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            NotifyTabs();
        }
    } = 1;

    [RelayCommand]
    void SetTab(string index) => Tab = int.Parse(index);

    public string NewCategoriesHeader => L.F("Import_NewCategoriesTab", Plan?.NewCategories.Count ?? 0);
    public string AddedHeader => L.F("Import_AddedTab", Plan?.Added.Count ?? 0);
    public string SkippedHeader => L.F("Import_SkippedTab", Plan?.Skipped.Count ?? 0);
    public string ProblemsHeader => L.F("Import_ProblemsTab", (Plan?.Errors.Count ?? 0) + (Plan?.Warnings.Count ?? 0));
    public bool IsTab0 => Tab == 0;
    public bool IsTab1 => Tab == 1;
    public bool IsTab2 => Tab == 2;
    public bool IsTab3 => Tab == 3;

    /// <summary>目前分頁的明細，每筆一行文字。清單是虛擬化的，上千筆也不會卡。</summary>
    public IReadOnlyList<string> Lines => Plan is null ? Array.Empty<string>() : Tab switch
    {
        0 => Plan.NewCategories.Select(c => $"{c.Name}（{L.T("Import_EntryCount")} {c.EntryCount}）").ToList(),
        1 => Plan.Added.Select(a => $"{a.Line}. {(string.IsNullOrEmpty(a.Emoji) ? "" : a.Emoji + " ")}{a.Text} · {a.CategoryName}").ToList(),
        2 => Plan.Skipped.Select(s => $"{s.Line}. {s.Text} · {s.CategoryName}\n   {s.Reason}").ToList(),
        _ => Plan.Errors.Concat(Plan.Warnings.Select(w => w with { Message = L.T("Import_NotePrefix") + w.Message }))
            .Select(i => (i.Line is null ? "" : $"{L.T("Import_Line")} {i.Line}: ") + i.Message).ToList(),
    };

    void NotifyTabs()
    {
        foreach (var name in new[] { nameof(IsTab0), nameof(IsTab1), nameof(IsTab2), nameof(IsTab3), nameof(Lines),
                     nameof(NewCategoriesHeader), nameof(AddedHeader), nameof(SkippedHeader), nameof(ProblemsHeader) })
            OnPropertyChanged(name);
    }

    async Task RebuildPlan()
    {
        if (_doc is null) return;
        var doc = _doc;
        var library = app.Store.Library;
        var options = new ImportOptions { Mode = IsReplace ? ImportMode.Replace : ImportMode.Merge, AllowNewCategories = AllowNewCategories };
        IsBusy = true;
        try
        {
            Plan = await Task.Run(() => ImportPlanner.Build(library, doc, options));
        }
        finally
        {
            IsBusy = false;
        }
        foreach (var name in new[] { nameof(Plan), nameof(SummaryText), nameof(BlockReason), nameof(HasBlockReason), nameof(ReplaceWarning), nameof(HasReplaceWarning) })
            OnPropertyChanged(name);
        NotifyTabs();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    bool CanApply() => !IsBusy && Plan?.CanApply == true;

    [RelayCommand(CanExecute = nameof(CanApply))]
    async Task Apply()
    {
        if (Plan is not { CanApply: true } plan) return;
        // 預覽之後詞庫又被修改過（理論上不會發生）：重新計算，不覆蓋那些修改
        if (!ReferenceEquals(plan.BaseLibrary, app.Store.Library))
        {
            await RebuildPlan();
            return;
        }
        if (plan.Errors.Count > 0 &&
            !await Dialogs.Confirm(L.F("Import_ConfirmErrors", plan.Errors.Count, plan.Added.Count), L.T("Import_ConfirmTitle")))
            return;
        if (plan.Mode == ImportMode.Replace)
        {
            if (!await Dialogs.Confirm(
                    L.F("Import_ConfirmReplace1", plan.BaseLibrary.Categories.Count, plan.BaseLibrary.Entries.Count,
                        plan.ResultLibrary.Categories.Count, plan.ResultLibrary.Entries.Count),
                    L.T("Import_ConfirmReplace1Title")))
                return;
            if (!await Dialogs.Confirm(L.T("Import_ConfirmReplace2"), L.T("Import_ConfirmReplace2Title")))
                return;
        }

        try
        {
            app.Store.ApplyImport(plan);
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Import_Failed", ex.Message));
            return;
        }

        var latestBackup = app.Store.ListBackups().FirstOrDefault();
        await Dialogs.Info(
            L.F("Import_Done", plan.Added.Count, plan.NewCategories.Count, plan.Skipped.Count, plan.Errors.Count) +
            (latestBackup is null ? "" : L.F("Import_DoneBackup", latestBackup.FileName)) +
            L.T("Import_DoneTail"), L.T("Import_DoneTitle"));
        await Shell.Current.GoToAsync("..");
    }
}

public sealed record BackupRow(BackupInfo Info, bool IsFull)
{
    public string TimeText => Info.Time.ToString("yyyy-MM-dd HH:mm:ss");
    public string ReasonText => IsFull ? L.F("Mobile_FullBackupReason", Describe(Info.Reason)) : Describe(Info.Reason);

    static string Describe(string reason) => reason switch
    {
        "manual" => L.T("Backup_Manual"),
        "before-import" => L.T("Backup_BeforeImport"),
        "before-replace" => L.T("Backup_BeforeReplace"),
        "before-restore" => L.T("Backup_BeforeRestore"),
        "before-delete-category" => L.T("Backup_BeforeDeleteCategory"),
        "before-bulk-delete" => L.T("Backup_BeforeBulkDelete"),
        "before-bulk-add" => L.T("Backup_BeforeBulkAdd"),
        "before-repair" => L.T("Backup_BeforeRepair"),
        var r => r,
    };
    public string SizeText => $"{Info.Size / 1024.0:0.#} KB";
}

/// <summary>App 內的詞庫備份：立即備份、點一下還原。</summary>
public sealed partial class BackupsViewModel(AppServices app) : ObservableObject
{
    public ObservableCollection<BackupRow> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    public void Load()
    {
        Items.Clear();
        var rows = app.Store.ListBackups().Select(b => new BackupRow(b, false))
            .Concat(app.Store.ListFullBackups().Select(b => new BackupRow(b, true)))
            .OrderByDescending(r => r.Info.Time);
        foreach (var row in rows) Items.Add(row);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    async Task BackupNow()
    {
        try
        {
            app.Store.CreateManualBackup();
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Data_BackupFailed", ex.Message));
            return;
        }
        Load();
        await Dialogs.Toast(L.T("Mobile_BackedUp"));
    }

    [RelayCommand]
    async Task Restore(BackupRow row)
    {
        if (row.IsFull)
        {
            await RestoreFull(row);
            return;
        }
        LibraryData lib;
        try
        {
            lib = app.Store.ReadBackup(row.Info.Path);
        }
        catch (Exception ex)
        {
            await Dialogs.Error(ex.Message);
            return;
        }
        var current = app.Store.Library;
        if (!await Dialogs.Confirm(
                L.F("Data_ConfirmRestore", row.TimeText, row.ReasonText, lib.Categories.Count, lib.Entries.Count, current.Categories.Count, current.Entries.Count),
                L.T("Data_RestoreTitle")))
            return;
        try
        {
            app.Store.ReplaceLibrary(lib, "before-restore");
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Data_RestoreFailed", ex.Message));
            return;
        }
        Load();
        await Dialogs.Info(L.T("Data_Restored"));
    }

    /// <summary>App 內的完整備份（還原前自動存的那份）：整份資料還原，用來復原一次錯誤的還原。</summary>
    async Task RestoreFull(BackupRow row)
    {
        var store = app.Store;
        FullBackupContent content;
        try
        {
            await using var stream = File.OpenRead(row.Info.Path);
            content = FullBackup.Read(stream);
        }
        catch (Exception ex)
        {
            await Dialogs.Error(ex.Message);
            return;
        }
        if (!await Dialogs.Confirm(
                L.F("Mobile_ConfirmRestoreFull", content.Manifest.CreatedAt,
                    content.Library.Categories.Count, content.Library.Entries.Count, content.Presets.Count, content.History.Count,
                    store.Library.Categories.Count, store.Library.Entries.Count, store.Presets.Count, store.History.Count),
                L.T("Data_RestoreTitle")))
            return;
        try
        {
            store.RestoreFull(content);
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            await Dialogs.Error(L.F("Data_RestoreFailed", ex.Message));
            return;
        }
        Load();
        await Dialogs.Info(L.T("Mobile_RestoredFull"));
    }
}
