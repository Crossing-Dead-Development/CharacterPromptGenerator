using System.IO;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class ImportViewModel : ObservableObject
{
    readonly AppSession _session;
    readonly string _path;
    ImportDocument? _doc;
    ImportPlan? _plan;
    bool _isLoading = true;
    bool _isReplace;
    bool _allowNewCategories = true;

    public ImportViewModel(AppSession session, string path)
    {
        _session = session;
        _path = path;
        ApplyCommand = new RelayCommand(Apply, () => !IsLoading && Plan?.CanApply == true);
    }

    /// <summary>由視窗載入時呼叫：在背景解析檔案，大檔案也不會卡住畫面。</summary>
    public async Task LoadAsync()
    {
        try
        {
            _doc = await Task.Run(() => ImportReader.ReadFile(_path));
        }
        catch (Exception ex)
        {
            _doc = ImportDocument.Fatal(ImportFormat.Txt, L.F("Import_ReadError", ex.Message));
        }
        IsLoading = false;
        await RebuildPlanAsync();
    }

    public event Action<bool>? CloseRequested;

    public string FileName => Path.GetFileName(_path);
    public string FilePath => _path;
    public string FormatText => _doc is null ? "" : _doc.Format switch
    {
        ImportFormat.Json => L.T("Import_FormatJson"),
        ImportFormat.Csv => L.T("Import_FormatCsv"),
        _ => L.T("Import_FormatTxt"),
    };

    public bool IsLoading
    {
        get => _isLoading;
        private set { Set(ref _isLoading, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsMerge
    {
        get => !_isReplace;
        set { if (value) IsReplace = false; }
    }

    public bool IsReplace
    {
        get => _isReplace;
        set
        {
            if (!Set(ref _isReplace, value)) return;
            OnPropertyChanged(nameof(IsMerge));
            _ = RebuildPlanAsync();
        }
    }

    public bool AllowNewCategories
    {
        get => _allowNewCategories;
        set { if (Set(ref _allowNewCategories, value)) _ = RebuildPlanAsync(); }
    }

    public ImportPlan? Plan
    {
        get => _plan;
        private set
        {
            Set(ref _plan, value);
            foreach (var n in new[] { nameof(FatalError), nameof(BlockReason), nameof(SummaryText), nameof(NewCategories), nameof(Added),
                         nameof(Skipped), nameof(Problems), nameof(ReplaceWarning), nameof(AddedHeader), nameof(SkippedHeader),
                         nameof(ProblemsHeader), nameof(NewCategoriesHeader) })
                OnPropertyChanged(n);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? FatalError => _doc?.FatalError;
    public string? BlockReason => _doc?.IsFatal == true ? null : Plan?.BlockReason;

    public string SummaryText
    {
        get
        {
            if (_doc is null || Plan is null) return "";
            if (_doc.IsFatal) return L.T("Import_CannotImport");
            return L.F("Import_Summary", _doc.Rows.Count, Plan.Added.Count, Plan.NewCategories.Count, Plan.Skipped.Count, Plan.Errors.Count);
        }
    }

    public string ReplaceWarning => IsReplace && Plan is not null
        ? L.F("Import_ReplaceWarning", Plan.RemovedEntries)
        : "";

    public IReadOnlyList<PlannedCategory> NewCategories => Plan?.NewCategories ?? new();
    public IReadOnlyList<PlannedEntry> Added => Plan?.Added ?? new();
    public IReadOnlyList<SkippedRow> Skipped => Plan?.Skipped ?? new();
    /// <summary>錯誤與警告合併顯示。</summary>
    public IReadOnlyList<ImportIssue> Problems =>
        Plan is null ? new List<ImportIssue>() : Plan.Errors.Concat(Plan.Warnings.Select(w => w with { Message = L.T("Import_NotePrefix") + w.Message })).ToList();

    public string NewCategoriesHeader => L.F("Import_NewCategoriesTab", NewCategories.Count);
    public string AddedHeader => L.F("Import_AddedTab", Added.Count);
    public string SkippedHeader => L.F("Import_SkippedTab", Skipped.Count);
    public string ProblemsHeader => L.F("Import_ProblemsTab", Problems.Count);

    public ICommand ApplyCommand { get; }

    async Task RebuildPlanAsync()
    {
        if (_doc is null) return;
        var doc = _doc;
        var library = _session.Library;
        var options = new ImportOptions
        {
            Mode = IsReplace ? ImportMode.Replace : ImportMode.Merge,
            AllowNewCategories = AllowNewCategories,
        };
        IsLoading = true;
        try
        {
            Plan = await Task.Run(() => ImportPlanner.Build(library, doc, options));
        }
        finally
        {
            IsLoading = false;
        }
    }

    void Apply()
    {
        if (Plan is not { CanApply: true } plan) return;

        if (plan.Errors.Count > 0 &&
            !Dialogs.Confirm(L.F("Import_ConfirmErrors", plan.Errors.Count, plan.Added.Count), L.T("Import_ConfirmTitle")))
            return;

        if (plan.Mode == ImportMode.Replace)
        {
            if (!Dialogs.Confirm(
                    L.F("Import_ConfirmReplace1", plan.BaseLibrary.Categories.Count, plan.BaseLibrary.Entries.Count,
                        plan.ResultLibrary.Categories.Count, plan.ResultLibrary.Entries.Count),
                    L.T("Import_ConfirmReplace1Title"), warning: true))
                return;
            if (!Dialogs.Confirm(L.T("Import_ConfirmReplace2"),
                    L.T("Import_ConfirmReplace2Title"), warning: true))
                return;
        }

        try
        {
            _session.Store.ApplyImport(plan);
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Import_Failed", ex.Message));
            return;
        }

        var latestBackup = _session.Store.ListBackups().FirstOrDefault();
        Dialogs.Info(
            L.F("Import_Done", plan.Added.Count, plan.NewCategories.Count, plan.Skipped.Count, plan.Errors.Count) +
            (latestBackup is null ? "" : L.F("Import_DoneBackup", latestBackup.FileName)) +
            L.T("Import_DoneTail"), L.T("Import_DoneTitle"));
        CloseRequested?.Invoke(true);
    }
}
