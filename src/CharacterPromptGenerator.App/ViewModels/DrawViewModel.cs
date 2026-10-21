using System.Collections.ObjectModel;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.App.ViewModels;

/// <summary>主畫面左側：每個分類的勾選與抽取數量。</summary>
public sealed class CategoryDrawRow : ObservableObject
{
    readonly Action<CategoryDrawRow> _changed;
    bool _isChecked;
    int _count;

    public CategoryDrawRow(Category cat, int total, int drawable, DrawSetting setting, Action<CategoryDrawRow> changed)
    {
        Id = cat.Id;
        Name = cat.Name;
        Description = cat.Description;
        LibraryEnabled = cat.Enabled;
        Total = total;
        Drawable = drawable;
        _isChecked = setting.Enabled;
        _count = Math.Max(0, setting.Count);
        _changed = changed;
        IncreaseCommand = new RelayCommand(() => Count++);
        DecreaseCommand = new RelayCommand(() => Count--, () => Count > 0);
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool LibraryEnabled { get; }
    public int Total { get; }
    public int Drawable { get; }

    public string AvailabilityText => !LibraryEnabled ? "分類已停用" : $"可抽 {Drawable}";
    public bool IsShort => LibraryEnabled && IsChecked && Count > Drawable;
    public string ToolTipText =>
        (string.IsNullOrWhiteSpace(Description) ? Name : $"{Name}\n{Description}") +
        $"\n詞條 {Total} 筆，可抽 {Drawable} 筆" + (LibraryEnabled ? "" : "\n（此分類在詞庫中被停用，不會抽到）");

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (!Set(ref _isChecked, value)) return;
            OnPropertyChanged(nameof(IsShort));
            _changed(this);
        }
    }

    public int Count
    {
        get => _count;
        set
        {
            if (!Set(ref _count, Math.Clamp(value, 0, 99))) return;
            OnPropertyChanged(nameof(IsShort));
            _changed(this);
        }
    }

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
}

public sealed class SlotViewModel : ObservableObject
{
    readonly Action _lockChanged;

    public SlotViewModel(DrawSlot slot, Action lockChanged)
    {
        Slot = slot;
        _lockChanged = lockChanged;
        ToggleLockCommand = new RelayCommand(() => Locked = !Locked);
    }

    public DrawSlot Slot { get; }
    public string Text => Slot.Text;
    public string? Emoji => Slot.Emoji;
    public string Notes => Slot.Notes;
    public bool HasEmoji => !string.IsNullOrEmpty(Slot.Emoji) && !Slot.Text.Contains(Slot.Emoji, StringComparison.Ordinal);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Slot.Notes);

    public bool Locked
    {
        get => Slot.Locked;
        set
        {
            if (Slot.Locked == value) return;
            Slot.Locked = value;
            OnPropertyChanged();
            _lockChanged();
        }
    }

    public ICommand ToggleLockCommand { get; }
}

public sealed class ResultGroupViewModel(string categoryId, string categoryName, ICommand rerollCommand)
{
    public string CategoryId { get; } = categoryId;
    public string CategoryName { get; } = categoryName;
    public ObservableCollection<SlotViewModel> Items { get; } = new();
    public ICommand RerollCommand { get; } = rerollCommand;
}

public sealed class DrawViewModel : ObservableObject
{
    readonly AppSession _session;
    List<DrawSlot> _slots = new();
    DateTime? _generatedAt;
    string? _currentHistoryId;
    string _warnings = "";
    Preset? _selectedPreset;
    bool _suppressSettingSave;

    public DrawViewModel(AppSession session)
    {
        _session = session;
        GenerateAllCommand = new RelayCommand(GenerateAll);
        RerollUnlockedCommand = new RelayCommand(RerollUnlocked);
        UnlockAllCommand = new RelayCommand(UnlockAll, () => LockedCount > 0);
        CopyCommand = new RelayCommand(Copy, () => HasResult);
        FavoriteCommand = new RelayCommand(ToggleFavorite, () => HasResult);
        RerollCategoryCommand = new RelayCommand(p => RerollCategory(p as string));
        CheckAllCommand = new RelayCommand(() => SetAllChecked(true));
        CheckNoneCommand = new RelayCommand(() => SetAllChecked(false));
        ApplyPresetCommand = new RelayCommand(ApplyPreset, () => SelectedPreset is not null);
        SavePresetAsCommand = new RelayCommand(SavePresetAs);
        OverwritePresetCommand = new RelayCommand(OverwritePreset, () => SelectedPreset is not null);
        RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedPreset is not null);
        DeletePresetCommand = new RelayCommand(DeletePreset, () => SelectedPreset is not null);
        OpenDisplayCommand = new RelayCommand(() => Views.DisplayWindow.ShowFor(this));
        DisplayBiggerCommand = new RelayCommand(() => DisplayFontSize += 4);
        DisplaySmallerCommand = new RelayCommand(() => DisplayFontSize -= 4);

        session.Store.LibraryChanged += (_, _) => RebuildRows();
        session.LoadResultRequested += (_, r) => LoadSaved(r);
        RebuildRows();
        RebuildPresets();
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == session.Store.Settings.LastPresetId);
    }

    public ObservableCollection<CategoryDrawRow> Rows { get; } = new();
    public ObservableCollection<ResultGroupViewModel> Groups { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();

    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set => Set(ref _selectedPreset, value);
    }

    public string Warnings
    {
        get => _warnings;
        private set => Set(ref _warnings, value);
    }

    public bool HasResult => _slots.Count > 0;
    public int LockedCount => _slots.Count(s => s.Locked);
    public string GenerateButtonText => HasResult ? "🎲 全部重新產生" : "🎲 產生角色題目";
    public string StatusText => !HasResult
        ? "尚未抽題。勾選左側分類並設定數量後，按「產生角色題目」。"
        : $"共 {_slots.Count} 項，已鎖定 {LockedCount} 項 · {_generatedAt:HH:mm:ss} 產生" + (IsFavorite ? " · ⭐ 已收藏" : "");
    public bool IsFavorite => _currentHistoryId is not null &&
                              _session.Store.History.FirstOrDefault(h => h.Id == _currentHistoryId)?.Favorite == true;
    public string FavoriteButtonText => IsFavorite ? "⭐ 取消收藏" : "☆ 收藏";
    public string SelectionSummary
    {
        get
        {
            var active = Rows.Where(r => r.IsChecked && r.Count > 0 && r.LibraryEnabled).ToList();
            return $"已選 {active.Count} 類，共 {active.Sum(r => r.Count)} 項";
        }
    }

    public ICommand GenerateAllCommand { get; }
    public ICommand RerollUnlockedCommand { get; }
    public ICommand UnlockAllCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand FavoriteCommand { get; }
    public ICommand RerollCategoryCommand { get; }
    public ICommand CheckAllCommand { get; }
    public ICommand CheckNoneCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand SavePresetAsCommand { get; }
    public ICommand OverwritePresetCommand { get; }
    public ICommand RenamePresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand OpenDisplayCommand { get; }
    public ICommand DisplayBiggerCommand { get; }
    public ICommand DisplaySmallerCommand { get; }

    // ── 直播展示模式設定（保存在 settings.json） ─────────

    public double DisplayFontSize
    {
        get => _session.Store.Settings.DisplayFontSize;
        set
        {
            var v = Math.Clamp(value, 14, 96);
            if (Math.Abs(v - _session.Store.Settings.DisplayFontSize) < 0.1) return;
            _session.Store.Settings.DisplayFontSize = v;
            OnPropertyChanged();
            _session.TrySave(_session.Store.SaveSettings, "設定");
        }
    }

    public bool DisplayShowNotes
    {
        get => _session.Store.Settings.DisplayShowNotes;
        set
        {
            if (_session.Store.Settings.DisplayShowNotes == value) return;
            _session.Store.Settings.DisplayShowNotes = value;
            OnPropertyChanged();
            _session.TrySave(_session.Store.SaveSettings, "設定");
        }
    }

    public bool DisplayShowControls
    {
        get => _session.Store.Settings.DisplayShowControls;
        set
        {
            if (_session.Store.Settings.DisplayShowControls == value) return;
            _session.Store.Settings.DisplayShowControls = value;
            OnPropertyChanged();
            _session.TrySave(_session.Store.SaveSettings, "設定");
        }
    }

    // ── 分類設定 ───────────────────────────────────────────

    void RebuildRows()
    {
        var lib = _session.Library;
        Rows.Clear();
        foreach (var cat in lib.OrderedCategories())
        {
            var (total, drawable) = LibraryEditor.CountEntries(lib, cat);
            Rows.Add(new CategoryDrawRow(cat, total, drawable, _session.Store.GetDrawSetting(cat), OnRowChanged));
        }
        OnPropertyChanged(nameof(SelectionSummary));
    }

    void OnRowChanged(CategoryDrawRow row)
    {
        OnPropertyChanged(nameof(SelectionSummary));
        if (_suppressSettingSave) return;
        _session.Store.Settings.DrawSettings[row.Id] = new DrawSetting { Enabled = row.IsChecked, Count = row.Count };
        _session.TrySave(_session.Store.SaveSettings, "設定");
    }

    void SetAllChecked(bool value)
    {
        _suppressSettingSave = true;
        foreach (var r in Rows) r.IsChecked = value;
        _suppressSettingSave = false;
        foreach (var r in Rows) _session.Store.Settings.DrawSettings[r.Id] = new DrawSetting { Enabled = r.IsChecked, Count = r.Count };
        _session.TrySave(_session.Store.SaveSettings, "設定");
    }

    List<DrawRequest> Requests() =>
        Rows.Where(r => r.IsChecked && r.Count > 0).Select(r => new DrawRequest(r.Id, r.Count)).ToList();

    // ── 方案 ───────────────────────────────────────────────

    void RebuildPresets()
    {
        var keep = SelectedPreset?.Id;
        Presets.Clear();
        foreach (var p in _session.Store.Presets) Presets.Add(p);
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == keep);
    }

    void ApplyPreset()
    {
        if (SelectedPreset is null) return;
        _session.Store.ApplyPresetToSettings(SelectedPreset);
        _session.TrySave(_session.Store.SaveSettings, "設定");
        RebuildRows();
    }

    void SavePresetAs()
    {
        var name = Dialogs.Prompt("另存為新方案", "方案名稱：", SelectedPreset is null ? "我的方案" : SelectedPreset.Name + " 副本");
        name = TextNormalizer.Clean(name);
        if (name.Length == 0) return;
        var preset = _session.Store.PresetFromSettings(IdGenerator.New("preset"), name);
        _session.Store.Presets.Add(preset);
        _session.Store.Settings.LastPresetId = preset.Id;
        if (!_session.TrySave(_session.Store.SavePresets, "方案")) _session.Store.Presets.Remove(preset);
        RebuildPresets();
        SelectedPreset = Presets.FirstOrDefault(p => p.Id == preset.Id);
    }

    void OverwritePreset()
    {
        if (SelectedPreset is not { } p) return;
        if (!Dialogs.Confirm($"用目前左側的分類設定覆寫方案「{p.Name}」？")) return;
        var updated = _session.Store.PresetFromSettings(p.Id, p.Name);
        var idx = _session.Store.Presets.FindIndex(x => x.Id == p.Id);
        if (idx < 0) return;
        var old = _session.Store.Presets[idx];
        _session.Store.Presets[idx] = updated;
        if (!_session.TrySave(_session.Store.SavePresets, "方案")) _session.Store.Presets[idx] = old;
        RebuildPresets();
        SelectedPreset = Presets.FirstOrDefault(x => x.Id == p.Id);
    }

    void RenamePreset()
    {
        if (SelectedPreset is not { } p) return;
        var name = TextNormalizer.Clean(Dialogs.Prompt("重新命名方案", "新名稱：", p.Name));
        if (name.Length == 0 || name == p.Name) return;
        var old = p.Name;
        p.Name = name;
        if (!_session.TrySave(_session.Store.SavePresets, "方案")) p.Name = old;
        RebuildPresets();
        SelectedPreset = Presets.FirstOrDefault(x => x.Id == p.Id);
    }

    void DeletePreset()
    {
        if (SelectedPreset is not { } p) return;
        if (!Dialogs.Confirm($"刪除方案「{p.Name}」？（不影響詞庫）")) return;
        var idx = _session.Store.Presets.FindIndex(x => x.Id == p.Id);
        if (idx < 0) return;
        _session.Store.Presets.RemoveAt(idx);
        if (!_session.TrySave(_session.Store.SavePresets, "方案")) _session.Store.Presets.Insert(idx, p);
        RebuildPresets();
    }

    // ── 抽題 ───────────────────────────────────────────────

    public void GenerateAll()
    {
        var locked = LockedCount;
        if (locked > 0 && !Dialogs.Confirm($"目前有 {locked} 個鎖定的項目。\n全部重新產生會清除鎖定，要繼續嗎？\n\n（只想換掉沒鎖定的項目，請用「重抽未鎖定」）"))
            return;
        Apply(_session.Engine.Generate(_session.Library, Requests()));
    }

    public void RerollUnlocked()
    {
        if (!HasResult)
        {
            Apply(_session.Engine.Generate(_session.Library, Requests()));
            return;
        }
        Apply(_session.Engine.Reroll(_session.Library, Requests(), _slots));
    }

    void RerollCategory(string? categoryId)
    {
        if (categoryId is null) return;
        var reqs = Requests();
        var row = Rows.FirstOrDefault(r => r.Id == categoryId);
        if (row is null || !row.IsChecked || row.Count == 0)
        {
            // 左側沒勾選，但結果中仍有這個分類：維持目前的數量重抽
            reqs.RemoveAll(r => r.CategoryId == categoryId);
            reqs.Add(new DrawRequest(categoryId, Math.Max(1, _slots.Count(s => s.CategoryId == categoryId))));
        }
        if (_session.Library.FindCategory(categoryId) is null)
        {
            Dialogs.Info("這個分類已經從詞庫刪除，無法重抽。可以解除鎖定後再重抽其他項目。");
            return;
        }
        Apply(_session.Engine.Reroll(_session.Library, reqs, _slots, categoryId));
    }

    void Apply(DrawOutcome outcome)
    {
        var warnings = new List<string>(outcome.Warnings);
        if (Requests().Count == 0 && outcome.Slots.Count == 0)
            warnings.Insert(0, "沒有勾選任何分類，或抽取數量都是 0。");
        Warnings = string.Join("\n", warnings);

        _slots = outcome.Slots;
        _generatedAt = DateTime.Now;
        RebuildGroups();

        _currentHistoryId = null;
        if (_slots.Count > 0)
        {
            var snap = PromptFormatter.Snapshot(_slots, _generatedAt.Value);
            _currentHistoryId = snap.Id;
            _session.TrySave(() => _session.Store.AddHistory(snap), "歷史紀錄");
            _session.RaiseHistoryChanged();
        }
        NotifyResultChanged();
    }

    void LoadSaved(SavedResult saved)
    {
        _slots = saved.Items.Select(i => new DrawSlot
        {
            CategoryId = i.CategoryId,
            CategoryName = i.CategoryName,
            EntryId = i.EntryId,
            Text = i.Text,
            Emoji = i.Emoji,
            Notes = i.Notes,
            Locked = true,
        }).ToList();
        _generatedAt = saved.CreatedAt;
        _currentHistoryId = saved.Id;
        Warnings = "已從歷史紀錄載入，所有項目先設為鎖定。解除想換掉的項目後按「重抽未鎖定」。";
        RebuildGroups();
        NotifyResultChanged();
    }

    void RebuildGroups()
    {
        Groups.Clear();
        ResultGroupViewModel? current = null;
        foreach (var slot in _slots)
        {
            if (current is null || current.CategoryId != slot.CategoryId)
            {
                current = Groups.FirstOrDefault(g => g.CategoryId == slot.CategoryId);
                if (current is null)
                {
                    var name = _session.Library.FindCategory(slot.CategoryId)?.Name ?? slot.CategoryName;
                    current = new ResultGroupViewModel(slot.CategoryId, name, RerollCategoryCommand);
                    Groups.Add(current);
                }
            }
            current.Items.Add(new SlotViewModel(slot, NotifyResultChanged));
        }
    }

    void UnlockAll()
    {
        foreach (var g in Groups)
            foreach (var s in g.Items)
                s.Locked = false;
    }

    void Copy()
    {
        if (Dialogs.CopyToClipboard(PromptFormatter.ToText(_slots, _generatedAt)))
            Warnings = "已複製題目到剪貼簿。";
    }

    void ToggleFavorite()
    {
        if (!HasResult) return;
        var store = _session.Store;
        var record = _currentHistoryId is null ? null : store.History.FirstOrDefault(h => h.Id == _currentHistoryId);
        if (record is null)
        {
            // 歷史已被清掉：重新建立一筆快照
            record = PromptFormatter.Snapshot(_slots, _generatedAt ?? DateTime.Now);
            record.Favorite = true;
            _currentHistoryId = record.Id;
            _session.TrySave(() => store.AddHistory(record), "收藏");
        }
        else
        {
            record.Favorite = !record.Favorite;
            if (!_session.TrySave(store.SaveHistory, "收藏")) record.Favorite = !record.Favorite;
        }
        _session.RaiseHistoryChanged();
        NotifyResultChanged();
    }

    void NotifyResultChanged()
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(LockedCount));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(GenerateButtonText));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteButtonText));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>歷史頁改變收藏狀態後，更新按鈕文字。</summary>
    public void RefreshFavoriteState() => NotifyResultChanged();
}
