using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.App.Views;
using CharacterPromptGenerator.Core.Emoji;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class CategoryListItem
{
    public string? Id { get; init; } // null = 全部分類
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int Total { get; init; }
    public int Drawable { get; init; }
    public string CountText => Id is null ? $"{Total}" : $"{Drawable}/{Total}";
    public double Opacity => Enabled ? 1 : 0.5;
}

public sealed class EntryRow
{
    public required string Id { get; init; }
    public required string CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string Text { get; init; }
    public string Emoji { get; init; } = "";
    public string Notes { get; init; } = "";
    public string TagsText { get; init; } = "";
    public bool Enabled { get; init; }
    public bool CategoryEnabled { get; init; }
    public string StatusText => !Enabled ? "停用" : CategoryEnabled ? "啟用" : "分類停用";
    internal string SearchKey { get; init; } = "";
}

public sealed class EditorViewModel : ObservableObject
{
    readonly AppSession _session;
    ObservableCollection<EntryRow> _entries = new();
    ICollectionView _entriesView;
    CategoryListItem? _selectedCategory;
    string _search = "";
    int _statusFilter;
    IReadOnlyList<EntryRow> _selectedEntries = Array.Empty<EntryRow>();

    // 分類表單
    string _catName = "", _catDescription = "";
    bool _catEnabled = true;
    int _catDefaultCount = 1;

    // 詞條表單
    string? _editingEntryId;
    string _formText = "", _formEmoji = "", _formNotes = "", _formTags = "";
    bool _formEnabled = true;
    string? _formCategoryId;
    string? _moveTargetCategoryId;

    public EditorViewModel(AppSession session)
    {
        _session = session;
        _entriesView = CollectionViewSource.GetDefaultView(_entries);

        NewCategoryCommand = new RelayCommand(NewCategory);
        SaveCategoryCommand = new RelayCommand(SaveCategory, () => IsRealCategorySelected);
        DeleteCategoryCommand = new RelayCommand(DeleteCategory, () => IsRealCategorySelected);
        MoveCategoryUpCommand = new RelayCommand(() => MoveCategory(-1), () => IsRealCategorySelected);
        MoveCategoryDownCommand = new RelayCommand(() => MoveCategory(1), () => IsRealCategorySelected);

        NewEntryCommand = new RelayCommand(NewEntry);
        SaveEntryCommand = new RelayCommand(SaveEntry);
        DeleteEntriesCommand = new RelayCommand(DeleteEntries, () => _selectedEntries.Count > 0);
        EnableEntriesCommand = new RelayCommand(() => SetEnabled(true), () => _selectedEntries.Count > 0);
        DisableEntriesCommand = new RelayCommand(() => SetEnabled(false), () => _selectedEntries.Count > 0);
        MoveEntriesCommand = new RelayCommand(MoveEntries, () => _selectedEntries.Count > 0 && MoveTargetCategoryId is not null);
        BulkAddCommand = new RelayCommand(BulkAdd);
        ImportCommand = new RelayCommand(_session.RunImport);
        ExportJsonCommand = new RelayCommand(_session.ExportJson);
        ExportCsvCommand = new RelayCommand(_session.ExportCsv);
        ClearSearchCommand = new RelayCommand(() => Search = "");

        session.Store.LibraryChanged += (_, _) => Rebuild();
        Rebuild();
        SelectedCategory = Categories.FirstOrDefault();
    }

    public ObservableCollection<CategoryListItem> Categories { get; } = new();
    /// <summary>實際的分類（給下拉選單用）。</summary>
    public ObservableCollection<CategoryListItem> RealCategories { get; } = new();
    public ICollectionView EntriesView
    {
        get => _entriesView;
        private set => Set(ref _entriesView, value);
    }

    public CategoryListItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!Set(ref _selectedCategory, value)) return;
            OnPropertyChanged(nameof(IsRealCategorySelected));
            LoadCategoryForm();
            _entriesView.Refresh();
            OnPropertyChanged(nameof(CountSummary));
            if (_editingEntryId is null && value?.Id is not null) FormCategoryId = value.Id;
        }
    }

    public bool IsRealCategorySelected => SelectedCategory?.Id is not null;

    public string Search
    {
        get => _search;
        set
        {
            if (!Set(ref _search, value)) return;
            _entriesView.Refresh();
            OnPropertyChanged(nameof(CountSummary));
        }
    }

    /// <summary>0 = 全部，1 = 只看啟用，2 = 只看停用。</summary>
    public int StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (!Set(ref _statusFilter, value)) return;
            _entriesView.Refresh();
            OnPropertyChanged(nameof(CountSummary));
        }
    }

    public string CountSummary
    {
        get
        {
            var shown = _entriesView.Cast<object>().Count();
            return $"顯示 {shown} 筆 / 詞庫共 {_entries.Count} 筆";
        }
    }

    public string CatName { get => _catName; set => Set(ref _catName, value); }
    public string CatDescription { get => _catDescription; set => Set(ref _catDescription, value); }
    public bool CatEnabled { get => _catEnabled; set => Set(ref _catEnabled, value); }
    public int CatDefaultCount { get => _catDefaultCount; set => Set(ref _catDefaultCount, Math.Clamp(value, 0, 99)); }

    public string FormTitle => _editingEntryId is null ? "新增詞條" : "編輯詞條";
    public string SaveEntryText => _editingEntryId is null ? "➕ 新增" : "💾 儲存修改";
    public string FormText { get => _formText; set { if (Set(ref _formText, value)) OnPropertyChanged(nameof(DuplicateWarning)); } }
    public string FormEmoji
    {
        get => _formEmoji;
        set { if (Set(ref _formEmoji, value)) OnPropertyChanged(nameof(FormEmojiInfo)); }
    }

    /// <summary>Emoji 欄位下方的說明：用哪張圖、碼點、找不到時怎麼補圖。</summary>
    public string FormEmojiInfo
    {
        get
        {
            var value = FormEmoji.Trim();
            if (value.Length == 0) return "";
            if (value.Contains((char)0xFFFD))
                return "⚠ 含有 U+FFFD 替代字元（原本的字已經遺失），請重新輸入正確的 Emoji。";
            var lines = new List<string>();
            foreach (var cluster in EmojiText.Clusters(value).Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var lookup = EmojiImages.Library.Lookup(cluster);
                var codes = EmojiText.Describe(cluster);
                lines.Add(lookup.Source switch
                {
                    EmojiImageSource.Pack => $"✓ Fluent 3D 圖片（{codes}）",
                    EmojiImageSource.Override => $"✓ 自訂圖片 EmojiOverrides\\{EmojiLibrary.OverrideFileName(lookup.Key!)}（{codes}）",
                    _ when EmojiText.ZwjComponents(cluster) is { Count: > 1 } parts && parts.All(p => EmojiImages.Library.Lookup(p).Found) =>
                        $"✓ 圖庫沒有這個組合，改為並排顯示 {parts.Count} 個組成元素（{codes}）",
                    _ when EmojiText.IsDefinitelyEmoji(cluster) =>
                        $"⚠ 沒有圖片（{codes}），會顯示「?」。可放 EmojiOverrides\\{EmojiLibrary.OverrideFileName(EmojiText.Key(cluster))} 補圖。",
                    _ => $"「{cluster}」不是 Emoji，會以文字顯示（{codes}）",
                });
            }
            return string.Join("\n", lines);
        }
    }
    public string FormNotes { get => _formNotes; set => Set(ref _formNotes, value); }
    public string FormTags { get => _formTags; set => Set(ref _formTags, value); }
    public bool FormEnabled { get => _formEnabled; set => Set(ref _formEnabled, value); }
    public string? FormCategoryId
    {
        get => _formCategoryId;
        set { if (Set(ref _formCategoryId, value)) OnPropertyChanged(nameof(DuplicateWarning)); }
    }

    public string DuplicateWarning
    {
        get
        {
            if (FormCategoryId is null || string.IsNullOrWhiteSpace(FormText)) return "";
            var dup = LibraryEditor.FindDuplicate(_session.Library, FormCategoryId, FormText, _editingEntryId);
            return dup is null ? "" : $"⚠ 此分類已有相同詞條「{dup.Text}」";
        }
    }

    public string? MoveTargetCategoryId { get => _moveTargetCategoryId; set => Set(ref _moveTargetCategoryId, value); }
    public string SelectionText => _selectedEntries.Count switch
    {
        0 => "未選取詞條",
        1 => "已選取 1 筆",
        var n => $"已選取 {n} 筆（可批次操作）",
    };

    public ICommand NewCategoryCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand MoveCategoryUpCommand { get; }
    public ICommand MoveCategoryDownCommand { get; }
    public ICommand NewEntryCommand { get; }
    public ICommand SaveEntryCommand { get; }
    public ICommand DeleteEntriesCommand { get; }
    public ICommand EnableEntriesCommand { get; }
    public ICommand DisableEntriesCommand { get; }
    public ICommand MoveEntriesCommand { get; }
    public ICommand BulkAddCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ClearSearchCommand { get; }

    // ── 重建 ───────────────────────────────────────────────

    void Rebuild()
    {
        var lib = _session.Library;
        var selectedCatId = SelectedCategory?.Id;
        var hadSelection = SelectedCategory is not null;

        Categories.Clear();
        RealCategories.Clear();
        Categories.Add(new CategoryListItem { Id = null, Name = "📚 全部分類", Total = lib.Entries.Count });
        var catNames = new Dictionary<string, Category>();
        foreach (var cat in lib.OrderedCategories())
        {
            var (total, drawable) = LibraryEditor.CountEntries(lib, cat);
            var item = new CategoryListItem { Id = cat.Id, Name = cat.Name, Enabled = cat.Enabled, Total = total, Drawable = drawable };
            Categories.Add(item);
            RealCategories.Add(item);
            catNames[cat.Id] = cat;
        }

        var rows = lib.Entries.Select(e =>
        {
            var cat = catNames.GetValueOrDefault(e.CategoryId);
            var tags = TextNormalizer.JoinTags(e.Tags);
            return new EntryRow
            {
                Id = e.Id,
                CategoryId = e.CategoryId,
                CategoryName = cat?.Name ?? "?",
                Text = e.Text,
                Emoji = e.Emoji ?? "",
                Notes = e.Notes,
                TagsText = tags,
                Enabled = e.Enabled,
                CategoryEnabled = cat?.Enabled ?? false,
                SearchKey = TextNormalizer.Key($"{e.Text} {e.Emoji} {e.Notes} {tags}"),
            };
        })
        .OrderBy(r => catNames.GetValueOrDefault(r.CategoryId)?.SortOrder ?? int.MaxValue)
        .ToList();

        _entries = new ObservableCollection<EntryRow>(rows);
        var view = new ListCollectionView(_entries) { Filter = FilterEntry };
        EntriesView = view;
        _entriesView = view;

        // 還原分類選擇；不觸發 setter 以免清掉表單
        _selectedCategory = hadSelection
            ? Categories.FirstOrDefault(c => c.Id == selectedCatId) ?? Categories[0]
            : null;
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(IsRealCategorySelected));
        LoadCategoryForm();
        OnPropertyChanged(nameof(CountSummary));
        OnPropertyChanged(nameof(DuplicateWarning));
        if (_editingEntryId is not null && lib.Entries.All(e => e.Id != _editingEntryId)) ClearEntryForm();
        if (FormCategoryId is not null && lib.FindCategory(FormCategoryId) is null) FormCategoryId = SelectedCategory?.Id;
    }

    bool FilterEntry(object o)
    {
        var row = (EntryRow)o;
        if (SelectedCategory?.Id is { } catId && row.CategoryId != catId) return false;
        if (StatusFilter == 1 && !row.Enabled) return false;
        if (StatusFilter == 2 && row.Enabled) return false;
        var key = TextNormalizer.Key(Search);
        return key.Length == 0 || row.SearchKey.Contains(key, StringComparison.Ordinal);
    }

    // ── 分類 ───────────────────────────────────────────────

    void LoadCategoryForm()
    {
        var cat = SelectedCategory?.Id is { } id ? _session.Library.FindCategory(id) : null;
        CatName = cat?.Name ?? "";
        CatDescription = cat?.Description ?? "";
        CatEnabled = cat?.Enabled ?? true;
        CatDefaultCount = cat?.DefaultDrawCount ?? 1;
    }

    void NewCategory()
    {
        var name = Dialogs.Prompt("新增分類", "分類名稱：");
        if (name is null) return;
        Category? created = null;
        if (_session.TryMutate(lib => created = LibraryEditor.AddCategory(lib, name)) && created is not null)
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == created.Id);
    }

    void SaveCategory()
    {
        if (SelectedCategory?.Id is not { } id) return;
        _session.TryMutate(lib => LibraryEditor.UpdateCategory(lib, id, CatName, CatDescription, CatEnabled, CatDefaultCount));
    }

    void DeleteCategory()
    {
        if (SelectedCategory?.Id is not { } id) return;
        var cat = _session.Library.FindCategory(id);
        if (cat is null) return;
        var count = _session.Library.Entries.Count(e => e.CategoryId == id);
        var msg = count == 0
            ? $"刪除分類「{cat.Name}」？"
            : $"刪除分類「{cat.Name}」會一併刪除其中的 {count} 筆詞條。\n\n" +
              "刪除前會自動備份，必要時可以在「資料與備份」頁還原。\n" +
              "若想保留詞條，請先選取詞條並「移到分類」。\n\n確定要刪除嗎？";
        if (!Dialogs.Confirm(msg, "刪除分類", warning: true)) return;
        if (_session.TryMutate(lib => LibraryEditor.DeleteCategory(lib, id), backupReason: "before-delete-category"))
            SelectedCategory = Categories.FirstOrDefault();
    }

    void MoveCategory(int delta)
    {
        if (SelectedCategory?.Id is not { } id) return;
        _session.TryMutate(lib => LibraryEditor.MoveCategory(lib, id, delta));
    }

    // ── 詞條 ───────────────────────────────────────────────

    /// <summary>由 View 在 DataGrid 選取變更時呼叫（SelectedItems 無法直接繫結）。</summary>
    public void SetSelection(IReadOnlyList<EntryRow> rows)
    {
        _selectedEntries = rows;
        OnPropertyChanged(nameof(SelectionText));
        CommandManager.InvalidateRequerySuggested();
        if (rows.Count == 1) LoadEntryForm(rows[0].Id);
    }

    void LoadEntryForm(string id)
    {
        var e = _session.Library.Entries.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        _editingEntryId = e.Id;
        FormText = e.Text;
        FormEmoji = e.Emoji ?? "";
        FormNotes = e.Notes;
        FormTags = TextNormalizer.JoinTags(e.Tags);
        FormEnabled = e.Enabled;
        FormCategoryId = e.CategoryId;
        OnFormModeChanged();
    }

    void ClearEntryForm()
    {
        _editingEntryId = null;
        FormText = FormEmoji = FormNotes = FormTags = "";
        FormEnabled = true;
        FormCategoryId = SelectedCategory?.Id ?? FormCategoryId ?? RealCategories.FirstOrDefault()?.Id;
        OnFormModeChanged();
    }

    void OnFormModeChanged()
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SaveEntryText));
        OnPropertyChanged(nameof(DuplicateWarning));
    }

    void NewEntry() => ClearEntryForm();

    void SaveEntry()
    {
        if (FormCategoryId is not { } catId)
        {
            Dialogs.Error("請先選擇詞條所屬的分類。");
            return;
        }
        var tags = TextNormalizer.ParseTags(FormTags.Replace('，', '|').Replace(',', '|'));
        if (_editingEntryId is null)
        {
            Entry? added = null;
            if (_session.TryMutate(lib => added = LibraryEditor.AddEntry(lib, catId, FormText, FormEmoji, FormNotes, tags, FormEnabled)))
            {
                // 連續新增：保留分類，清空內容
                var keepCat = catId;
                ClearEntryForm();
                FormCategoryId = keepCat;
            }
        }
        else
        {
            var id = _editingEntryId;
            _session.TryMutate(lib => LibraryEditor.UpdateEntry(lib, id, catId, FormText, FormEmoji, FormNotes, tags, FormEnabled));
        }
    }

    void DeleteEntries()
    {
        var ids = _selectedEntries.Select(r => r.Id).ToList();
        if (ids.Count == 0) return;
        var msg = ids.Count == 1 ? $"刪除詞條「{_selectedEntries[0].Text}」？" : $"刪除選取的 {ids.Count} 筆詞條？";
        if (!Dialogs.Confirm(msg + "\n\n（已存在的歷史紀錄與收藏不受影響）", "刪除詞條", warning: true)) return;
        _session.TryMutate(lib => LibraryEditor.DeleteEntries(lib, ids), backupReason: ids.Count >= 20 ? "before-bulk-delete" : null);
    }

    void SetEnabled(bool enabled)
    {
        var ids = _selectedEntries.Select(r => r.Id).ToList();
        _session.TryMutate(lib => LibraryEditor.SetEntriesEnabled(lib, ids, enabled));
    }

    void MoveEntries()
    {
        if (MoveTargetCategoryId is not { } target) return;
        var ids = _selectedEntries.Select(r => r.Id).ToList();
        (int Moved, int Skipped) result = default;
        if (_session.TryMutate(lib => result = LibraryEditor.MoveEntries(lib, ids, target)) && result.Skipped > 0)
            Dialogs.Info($"已移動 {result.Moved} 筆；{result.Skipped} 筆因目標分類已有相同詞條而略過。");
    }

    void BulkAdd()
    {
        var catId = SelectedCategory?.Id ?? FormCategoryId;
        if (catId is null)
        {
            Dialogs.Error("請先在左側選擇要加入的分類。");
            return;
        }
        var dlg = new BulkAddWindow(_session, catId) { Owner = Dialogs.Owner };
        dlg.ShowDialog();
    }
}
