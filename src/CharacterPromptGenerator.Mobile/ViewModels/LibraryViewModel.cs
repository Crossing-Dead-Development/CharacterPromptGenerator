using System.Collections.ObjectModel;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using Entry = CharacterPromptGenerator.Core.Models.Entry;
using CharacterPromptGenerator.Core.Text;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CharacterPromptGenerator.Mobile.ViewModels;

/// <summary>詞庫分頁的一列分類。Id 為 null 表示「全部分類」。</summary>
public sealed record CategoryItem(string? Id, string Name, string Description, string CountText, bool Enabled)
{
    public bool HasDescription => Description.Length > 0;
    public double Opacity => Enabled ? 1 : 0.5;
}

/// <summary>詞庫分頁：分類列表與詞條數。點分類進入詞條列表。</summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    readonly AppServices app;

    public LibraryViewModel(AppServices app)
    {
        this.app = app;
        app.DataChanged += OnDataChanged;
    }

    void OnDataChanged(object? sender, EventArgs e) => Load();

    public ObservableCollection<CategoryItem> Categories { get; } = new();

    public string Summary
    {
        get
        {
            var lib = app.Store.Library;
            var drawable = lib.Categories.Sum(c => LibraryEditor.CountEntries(lib, c).Drawable);
            return L.F("Mobile_LibrarySummary", lib.Categories.Count, lib.Entries.Count, drawable);
        }
    }

    public bool IsEmpty => app.Store.Library.Categories.Count == 0;

    public void Load()
    {
        var lib = app.Store.Library;
        Categories.Clear();
        if (lib.Categories.Count > 0)
            Categories.Add(new CategoryItem(null, L.T("Editor_AllCategories"), "", lib.Entries.Count.ToString(), true));
        foreach (var cat in lib.OrderedCategories())
        {
            var (total, drawable) = LibraryEditor.CountEntries(lib, cat);
            var name = cat.Enabled ? cat.Name : $"{cat.Name}（{L.T("Draw_CategoryDisabled")}）";
            Categories.Add(new CategoryItem(cat.Id, name, cat.Description, L.F("Mobile_CategoryCount", drawable, total), cat.Enabled));
        }
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    Task Open(CategoryItem item) =>
        Shell.Current.GoToAsync(EntriesPage.Route, new Dictionary<string, object> { ["categoryId"] = item.Id ?? "" });

    [RelayCommand]
    async Task AddCategory()
    {
        var name = await Dialogs.Prompt(L.T("Editor_NewCategoryTitle"), L.T("Editor_CategoryNameLabel"));
        if (name is null) return;
        Category? created = null;
        if (!await app.TryMutate(lib => created = LibraryEditor.AddCategory(lib, name))) return;
        Load();
        await Shell.Current.GoToAsync(EntriesPage.Route, new Dictionary<string, object> { ["categoryId"] = created!.Id });
    }
}

/// <summary>詞條列表中的一列。</summary>
public sealed partial class EntryItem(Entry entry, string categoryName, bool categoryEnabled, bool showCategory) : ObservableObject
{
    public string Id => entry.Id;
    public string Text => entry.Text;
    public string? Emoji => entry.Emoji;
    public bool HasEmoji => !string.IsNullOrEmpty(entry.Emoji);
    public string Notes => entry.Notes;
    public bool HasNotes => !string.IsNullOrWhiteSpace(entry.Notes);
    public string TagsText => string.Join(" ", entry.Tags.Select(t => "#" + t));
    public bool HasTags => entry.Tags.Count > 0;
    public string CategoryName { get; } = categoryName;
    public bool ShowCategory { get; } = showCategory;
    public bool Enabled => entry.Enabled;
    /// <summary>不會被抽到的原因：詞條停用或分類停用。可以抽到時為空白。</summary>
    public string StatusText => !entry.Enabled ? L.T("Editor_StatusDisabled") : categoryEnabled ? "" : L.T("Editor_StatusCategoryOff");
    public bool HasStatus => StatusText.Length > 0;
    public double Opacity => entry.Enabled && categoryEnabled ? 1 : 0.55;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectGlyph))]
    public partial bool IsSelected { get; set; }

    public string SelectGlyph => IsSelected ? "☑" : "☐";
}

/// <summary>詞條列表：搜尋、狀態篩選、多選批次操作（啟用、停用、移動、刪除）。</summary>
public sealed partial class EntriesViewModel : ObservableObject, IQueryAttributable
{
    readonly AppServices app;

    public EntriesViewModel(AppServices app)
    {
        this.app = app;
        app.DataChanged += OnDataChanged;
    }

    void OnDataChanged(object? sender, EventArgs e) => Load();

    string? _categoryId;
    LibraryQuery? _query;
    readonly HashSet<string> _selected = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var id = query.TryGetValue("categoryId", out var v) ? v as string : null;
        _categoryId = string.IsNullOrEmpty(id) ? null : id;
    }

    public bool IsRealCategory => _categoryId is not null;
    public string Title => _categoryId is null ? L.T("Editor_AllCategories") : app.Store.Library.FindCategory(_categoryId)?.Name ?? "?";

    public string Search
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) ApplyFilter();
        }
    } = "";

    public EntryStatusFilter Status
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(IsAll));
            OnPropertyChanged(nameof(IsEnabledOnly));
            OnPropertyChanged(nameof(IsDisabledOnly));
            ApplyFilter();
        }
    }

    public bool IsAll => Status == EntryStatusFilter.All;
    public bool IsEnabledOnly => Status == EntryStatusFilter.Enabled;
    public bool IsDisabledOnly => Status == EntryStatusFilter.Disabled;

    [RelayCommand]
    void SetStatus(string value) => Status = Enum.Parse<EntryStatusFilter>(value);

    public IReadOnlyList<EntryItem> Items { get; private set; } = Array.Empty<EntryItem>();
    public bool IsEmpty => Items.Count == 0;

    public string CountSummary => L.F("Editor_CountSummary", Items.Count, _query?.Total ?? 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectButtonText))]
    public partial bool SelectMode { get; set; }

    public string SelectButtonText => SelectMode ? L.T("Mobile_Done") : L.T("Mobile_Select");

    public string SelectionText => _selected.Count switch
    {
        0 => L.T("Editor_NoneSelected"),
        1 => L.T("Editor_OneSelected"),
        var n => L.F("Editor_ManySelected", n),
    };

    public bool HasSelection => _selected.Count > 0;

    /// <summary>每次頁面出現時重新讀取詞庫（可能在編輯頁、匯入後被修改）。</summary>
    public void Load()
    {
        if (_categoryId is not null && app.Store.Library.FindCategory(_categoryId) is null) _categoryId = null;
        _query = new LibraryQuery(app.Store.Library);
        _selected.IntersectWith(app.Store.Library.Entries.Select(e => e.Id));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsRealCategory));
        ApplyFilter();
    }

    void ApplyFilter()
    {
        if (_query is null) return;
        var lib = app.Store.Library;
        var cats = lib.Categories.ToDictionary(c => c.Id);
        Items = _query.Filter(_categoryId, Search, Status)
            .Select(e =>
            {
                var cat = cats.GetValueOrDefault(e.CategoryId);
                return new EntryItem(e, cat?.Name ?? "?", cat?.Enabled ?? false, _categoryId is null) { IsSelected = _selected.Contains(e.Id) };
            })
            .ToList();
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountSummary));
        NotifySelection();
    }

    void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(HasSelection));
    }

    // ── 點選 ───────────────────────────────────────────────

    [RelayCommand]
    Task Tap(EntryItem item)
    {
        if (!SelectMode)
            return Shell.Current.GoToAsync(EntryEditPage.Route, new Dictionary<string, object> { ["entryId"] = item.Id });
        item.IsSelected = !item.IsSelected;
        if (item.IsSelected) _selected.Add(item.Id);
        else _selected.Remove(item.Id);
        NotifySelection();
        return Task.CompletedTask;
    }

    [RelayCommand]
    void ToggleSelectMode()
    {
        SelectMode = !SelectMode;
        if (SelectMode) return;
        _selected.Clear();
        foreach (var i in Items) i.IsSelected = false;
        NotifySelection();
    }

    [RelayCommand]
    void SelectAllShown()
    {
        foreach (var i in Items)
        {
            i.IsSelected = true;
            _selected.Add(i.Id);
        }
        NotifySelection();
    }

    // ── 新增 ───────────────────────────────────────────────

    [RelayCommand]
    Task AddEntry() =>
        Shell.Current.GoToAsync(EntryEditPage.Route, new Dictionary<string, object> { ["categoryId"] = _categoryId ?? "" });

    [RelayCommand]
    async Task More()
    {
        var actions = new List<string> { L.T("Editor_BulkButton") };
        if (IsRealCategory) actions.Add(L.T("Mobile_CategorySettings"));
        switch (await Dialogs.Choose(Title, actions))
        {
            case 0:
                var catId = _categoryId ?? await ChooseCategory(L.T("Bulk_CategoryLabel"));
                if (catId is not null)
                    await Shell.Current.GoToAsync(BulkAddPage.Route, new Dictionary<string, object> { ["categoryId"] = catId });
                break;
            case 1:
                await Shell.Current.GoToAsync(CategoryEditPage.Route, new Dictionary<string, object> { ["categoryId"] = _categoryId! });
                break;
        }
    }

    async Task<string?> ChooseCategory(string title)
    {
        var cats = app.Store.Library.OrderedCategories().ToList();
        if (cats.Count == 0) return null;
        var index = await Dialogs.Choose(title, cats.Select(c => c.Name).ToList());
        return index < 0 ? null : cats[index].Id;
    }

    // ── 批次操作 ───────────────────────────────────────────

    [RelayCommand]
    async Task EnableSelected() => await SetEnabled(true);

    [RelayCommand]
    async Task DisableSelected() => await SetEnabled(false);

    async Task SetEnabled(bool enabled)
    {
        var ids = _selected.ToList();
        if (ids.Count == 0) return;
        if (await app.TryMutate(lib => LibraryEditor.SetEntriesEnabled(lib, ids, enabled))) Load();
    }

    [RelayCommand]
    async Task MoveSelected()
    {
        var ids = _selected.ToList();
        if (ids.Count == 0) return;
        var target = await ChooseCategory(L.T("Mobile_ChooseTargetCategory"));
        if (target is null) return;
        (int Moved, int Skipped) result = default;
        if (!await app.TryMutate(lib => result = LibraryEditor.MoveEntries(lib, ids, target))) return;
        Load();
        if (result.Skipped > 0) await Dialogs.Info(L.F("Editor_Moved", result.Moved, result.Skipped));
    }

    [RelayCommand]
    async Task DeleteSelected()
    {
        var ids = _selected.ToList();
        if (ids.Count == 0) return;
        var msg = ids.Count == 1
            ? L.F("Editor_DeleteEntry", app.Store.Library.Entries.First(e => e.Id == ids[0]).Text)
            : L.F("Editor_DeleteEntries", ids.Count);
        if (!await Dialogs.Confirm(msg + L.T("Editor_DeleteEntriesNote"), L.T("Editor_DeleteEntryTitle"))) return;
        if (!await app.TryMutate(lib => LibraryEditor.DeleteEntries(lib, ids), ids.Count >= 20 ? "before-bulk-delete" : null)) return;
        _selected.Clear();
        Load();
    }
}

/// <summary>新增或編輯一筆詞條。新增成功後保留分類、清空內容，方便連續輸入。</summary>
public sealed partial class EntryEditViewModel(AppServices app) : ObservableObject, IQueryAttributable
{
    string? _entryId;
    List<Category> _categories = new();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _categories = app.Store.Library.OrderedCategories().ToList();
        CategoryNames = _categories.Select(c => c.Name).ToList();
        _entryId = query.TryGetValue("entryId", out var e) && e is string id && id.Length > 0 ? id : null;
        var entry = _entryId is null ? null : app.Store.Library.Entries.FirstOrDefault(x => x.Id == _entryId);
        if (entry is null)
        {
            _entryId = null;
            var catId = query.TryGetValue("categoryId", out var c) ? c as string : null;
            CategoryIndex = Math.Max(0, _categories.FindIndex(x => x.Id == catId));
        }
        else
        {
            CategoryIndex = _categories.FindIndex(x => x.Id == entry.CategoryId);
            Text = entry.Text;
            Emoji = entry.Emoji ?? "";
            Notes = entry.Notes;
            Tags = TextNormalizer.JoinTags(entry.Tags);
            Enabled = entry.Enabled;
        }
        OnPropertyChanged(nameof(CategoryNames));
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SaveText));
    }

    public bool IsEditing => _entryId is not null;
    public string Title => IsEditing ? L.T("Editor_EditEntryTitle") : L.T("Editor_NewEntryTitle");
    public string SaveText => IsEditing ? L.T("Editor_SaveChanges") : L.T("Editor_Add");

    public List<string> CategoryNames { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateWarning), nameof(HasDuplicate))]
    public partial int CategoryIndex { get; set; } = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuplicateWarning), nameof(HasDuplicate))]
    public partial string Text { get; set; } = "";

    [ObservableProperty] public partial string Emoji { get; set; } = "";
    [ObservableProperty] public partial string Notes { get; set; } = "";
    [ObservableProperty] public partial string Tags { get; set; } = "";
    [ObservableProperty] public partial bool Enabled { get; set; } = true;

    string? CategoryId => CategoryIndex >= 0 && CategoryIndex < _categories.Count ? _categories[CategoryIndex].Id : null;

    public string DuplicateWarning
    {
        get
        {
            if (CategoryId is not { } catId || string.IsNullOrWhiteSpace(Text)) return "";
            var dup = LibraryEditor.FindDuplicate(app.Store.Library, catId, Text, _entryId);
            return dup is null ? "" : L.F("Editor_DupWarning", dup.Text);
        }
    }

    public bool HasDuplicate => DuplicateWarning.Length > 0;

    [RelayCommand]
    async Task Save()
    {
        if (CategoryId is not { } catId)
        {
            await Dialogs.Error(L.T("Editor_ChooseEntryCategory"));
            return;
        }
        // 標籤可以用 |、半形或全形逗號分隔（與 Windows 版相同）
        var tags = TextNormalizer.ParseTags(Tags.Replace('，', '|').Replace(',', '|'));
        if (_entryId is { } id)
        {
            if (!await app.TryMutate(lib => LibraryEditor.UpdateEntry(lib, id, catId, Text, Emoji, Notes, tags, Enabled))) return;
            await Dialogs.Toast(L.T("Mobile_Saved"));
            await Shell.Current.GoToAsync("..");
            return;
        }
        Entry? added = null;
        if (!await app.TryMutate(lib => added = LibraryEditor.AddEntry(lib, catId, Text, Emoji, Notes, tags, Enabled))) return;
        await Dialogs.Toast(L.F("Mobile_EntryAdded", added!.Text));
        Text = Emoji = Notes = Tags = "";
        Enabled = true;
    }

    [RelayCommand]
    async Task Delete()
    {
        if (_entryId is not { } id) return;
        if (!await Dialogs.Confirm(L.F("Editor_DeleteEntry", Text) + L.T("Editor_DeleteEntriesNote"), L.T("Editor_DeleteEntryTitle"))) return;
        if (!await app.TryMutate(lib => LibraryEditor.DeleteEntries(lib, [id]))) return;
        await Shell.Current.GoToAsync("..");
    }
}

/// <summary>多行貼上：每行一個詞條，即時預覽會新增幾筆、哪些重複。</summary>
public sealed partial class BulkAddViewModel(AppServices app) : ObservableObject, IQueryAttributable
{
    string _categoryId = "";
    LibraryEditor.BulkAddPreview? _preview;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _categoryId = query.TryGetValue("categoryId", out var v) ? v as string ?? "" : "";
        OnPropertyChanged(nameof(CategoryText));
        UpdatePreview();
    }

    public string CategoryText => L.T("Bulk_CategoryLabel") + (app.Store.Library.FindCategory(_categoryId)?.Name ?? "?");

    [ObservableProperty]
    public partial string Input { get; set; } = "";

    partial void OnInputChanged(string value) => UpdatePreview();

    public string PreviewText => L.F("Bulk_WillAdd", _preview?.ToAdd.Count ?? 0);
    public string DuplicateText { get; private set; } = "";
    public bool HasDuplicates => DuplicateText.Length > 0;
    public string AddText => _preview is { ToAdd.Count: > 0 } p ? L.F("Bulk_AddCount", p.ToAdd.Count) : L.T("Bulk_Add");

    void UpdatePreview()
    {
        _preview = LibraryEditor.PreviewBulkAdd(app.Store.Library, _categoryId, Input);
        var parts = new List<string>();
        if (_preview.DuplicateExisting.Count > 0)
            parts.Add(L.F("Bulk_DupExisting", _preview.DuplicateExisting.Count, Sample(_preview.DuplicateExisting)));
        if (_preview.DuplicateInInput.Count > 0)
            parts.Add(L.F("Bulk_DupInput", _preview.DuplicateInInput.Count, Sample(_preview.DuplicateInInput)));
        DuplicateText = string.Join("\n", parts);
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(DuplicateText));
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(AddText));
        AddCommand.NotifyCanExecuteChanged();
    }

    static string Sample(IReadOnlyList<string> items) =>
        string.Join(L.T("Common_ListSep"), items.Take(15)) + (items.Count > 15 ? L.T("Common_Ellipsis") : "");

    bool CanAdd() => _preview is { ToAdd.Count: > 0 };

    [RelayCommand(CanExecute = nameof(CanAdd))]
    async Task Add()
    {
        var texts = _preview!.ToAdd;
        var added = 0;
        if (!await app.TryMutate(lib => added = LibraryEditor.BulkAdd(lib, _categoryId, texts), texts.Count >= 50 ? "before-bulk-add" : null))
            return;
        await Dialogs.Info(L.F("Bulk_Added", added));
        await Shell.Current.GoToAsync("..");
    }
}

/// <summary>分類設定：名稱、說明、啟用、預設數量、排序與刪除。</summary>
public sealed partial class CategoryEditViewModel(AppServices app) : ObservableObject, IQueryAttributable
{
    string _categoryId = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _categoryId = query.TryGetValue("categoryId", out var v) ? v as string ?? "" : "";
        var cat = app.Store.Library.FindCategory(_categoryId);
        if (cat is null) return;
        Name = cat.Name;
        Description = cat.Description;
        Enabled = cat.Enabled;
        DefaultCount = cat.DefaultDrawCount;
        OnPropertyChanged(nameof(PositionText));
    }

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial bool Enabled { get; set; } = true;

    [ObservableProperty]
    public partial int DefaultCount { get; set; } = 1;

    partial void OnDefaultCountChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, Core.Drawing.DrawSession.MaxCount);
        if (clamped != value) DefaultCount = clamped;
    }

    [RelayCommand]
    void Increase() => DefaultCount++;

    [RelayCommand]
    void Decrease() => DefaultCount--;

    /// <summary>目前在分類列表中的位置，例如「3 / 21」。</summary>
    public string PositionText
    {
        get
        {
            var ordered = app.Store.Library.OrderedCategories().ToList();
            return $"{ordered.FindIndex(c => c.Id == _categoryId) + 1} / {ordered.Count}";
        }
    }

    [RelayCommand]
    async Task Save()
    {
        if (await app.TryMutate(lib => LibraryEditor.UpdateCategory(lib, _categoryId, Name, Description, Enabled, DefaultCount)))
            await Dialogs.Toast(L.T("Mobile_Saved"));
    }

    [RelayCommand]
    async Task MoveUp() => await Move(-1);

    [RelayCommand]
    async Task MoveDown() => await Move(1);

    async Task Move(int delta)
    {
        if (await app.TryMutate(lib => LibraryEditor.MoveCategory(lib, _categoryId, delta)))
            OnPropertyChanged(nameof(PositionText));
    }

    [RelayCommand]
    async Task Delete()
    {
        var lib = app.Store.Library;
        var cat = lib.FindCategory(_categoryId);
        if (cat is null) return;
        var count = lib.Entries.Count(e => e.CategoryId == cat.Id);
        var msg = count == 0 ? L.F("Editor_DeleteEmptyCategory", cat.Name) : L.F("Mobile_DeleteCategory", cat.Name, count);
        if (!await Dialogs.Confirm(msg, L.T("Editor_DeleteCategoryTitle"))) return;
        if (!await app.TryMutate(l => LibraryEditor.DeleteCategory(l, _categoryId), "before-delete-category")) return;
        await Shell.Current.GoToAsync("//library");
    }
}
