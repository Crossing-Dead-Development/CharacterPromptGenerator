using System.IO;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Import;

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
            _doc = ImportDocument.Fatal(ImportFormat.Txt, $"讀取檔案時發生錯誤：{ex.Message}");
        }
        IsLoading = false;
        await RebuildPlanAsync();
    }

    public event Action<bool>? CloseRequested;

    public string FileName => Path.GetFileName(_path);
    public string FilePath => _path;
    public string FormatText => _doc is null ? "" : _doc.Format switch
    {
        ImportFormat.Json => "JSON（完整詞庫格式）",
        ImportFormat.Csv => "CSV（category,text,emoji,notes,tags）",
        _ => "TXT（[分類] + 每行一個詞條）",
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
            if (_doc.IsFatal) return "檔案無法匯入。";
            return $"讀到 {_doc.Rows.Count} 筆詞條 → 將新增 {Plan.Added.Count} 筆、" +
                   $"建立 {Plan.NewCategories.Count} 個新分類、略過 {Plan.Skipped.Count} 筆（重複或不允許的分類）、" +
                   $"錯誤 {Plan.Errors.Count} 列。";
        }
    }

    public string ReplaceWarning => IsReplace && Plan is not null
        ? $"⚠ 取代模式會刪除目前詞庫的全部 {Plan.RemovedEntries} 筆詞條與所有分類，換成檔案內容。套用前會自動備份。"
        : "";

    public IReadOnlyList<PlannedCategory> NewCategories => Plan?.NewCategories ?? new();
    public IReadOnlyList<PlannedEntry> Added => Plan?.Added ?? new();
    public IReadOnlyList<SkippedRow> Skipped => Plan?.Skipped ?? new();
    /// <summary>錯誤與警告合併顯示。</summary>
    public IReadOnlyList<ImportIssue> Problems =>
        Plan is null ? new List<ImportIssue>() : Plan.Errors.Concat(Plan.Warnings.Select(w => w with { Message = "（提醒）" + w.Message })).ToList();

    public string NewCategoriesHeader => $"新分類 ({NewCategories.Count})";
    public string AddedHeader => $"將新增 ({Added.Count})";
    public string SkippedHeader => $"略過 ({Skipped.Count})";
    public string ProblemsHeader => $"錯誤與提醒 ({Problems.Count})";

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
            !Dialogs.Confirm($"有 {plan.Errors.Count} 列格式錯誤，這些列不會被匯入。\n其他 {plan.Added.Count} 筆仍要匯入嗎？", "匯入確認"))
            return;

        if (plan.Mode == ImportMode.Replace)
        {
            if (!Dialogs.Confirm(
                    $"確定要「取代整個詞庫」嗎？\n\n目前詞庫：{plan.BaseLibrary.Categories.Count} 個分類、{plan.BaseLibrary.Entries.Count} 筆詞條\n" +
                    $"取代後：{plan.ResultLibrary.Categories.Count} 個分類、{plan.ResultLibrary.Entries.Count} 筆詞條",
                    "取代整個詞庫（第 1 次確認）", warning: true))
                return;
            if (!Dialogs.Confirm("最後確認：目前的詞庫會先自動備份，然後被完全取代。\n要繼續嗎？",
                    "取代整個詞庫（第 2 次確認）", warning: true))
                return;
        }

        try
        {
            _session.Store.ApplyImport(plan);
        }
        catch (Exception ex)
        {
            Dialogs.Error($"匯入沒有完成，目前詞庫保持原狀。\n\n{ex.Message}");
            return;
        }

        var latestBackup = _session.Store.ListBackups().FirstOrDefault();
        Dialogs.Info(
            $"匯入完成！\n\n新增詞條：{plan.Added.Count} 筆\n新建分類：{plan.NewCategories.Count} 個\n" +
            $"略過（重複等）：{plan.Skipped.Count} 筆\n失敗（格式錯誤）：{plan.Errors.Count} 列\n\n" +
            (latestBackup is null ? "" : $"匯入前的詞庫已備份為：\n{latestBackup.FileName}\n\n") +
            "新詞條已經可以抽到，不需重新啟動。", "匯入完成");
        CloseRequested?.Invoke(true);
    }
}
