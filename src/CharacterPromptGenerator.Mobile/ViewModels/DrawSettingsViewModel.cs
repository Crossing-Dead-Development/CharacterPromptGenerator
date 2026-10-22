using System.Collections.ObjectModel;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CharacterPromptGenerator.Mobile.ViewModels;

/// <summary>抽題設定中的一個分類：開關與抽取數量。</summary>
public sealed partial class CategoryRowViewModel : ObservableObject
{
    readonly Action<CategoryRowViewModel> _changed;

    public CategoryRowViewModel(Category cat, int drawable, DrawSetting setting, Action<CategoryRowViewModel> changed)
    {
        Id = cat.Id;
        Name = cat.Name;
        Description = cat.Description;
        LibraryEnabled = cat.Enabled;
        Drawable = drawable;
        _isChecked = setting.Enabled;
        _count = Math.Clamp(setting.Count, 0, Core.Drawing.DrawSession.MaxCount);
        _changed = changed;
    }

    bool _isChecked;
    int _count;

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool LibraryEnabled { get; }
    public int Drawable { get; }

    public string AvailabilityText => !LibraryEnabled ? L.T("Draw_CategoryDisabled") : L.F("Draw_Available", Drawable);
    /// <summary>勾選的數量超過可抽的詞條數：顯示警告色。</summary>
    public bool IsShort => LibraryEnabled && IsChecked && Count > Drawable;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (!SetProperty(ref _isChecked, value)) return;
            OnPropertyChanged(nameof(IsShort));
            _changed(this);
        }
    }

    public int Count
    {
        get => _count;
        set
        {
            if (!SetProperty(ref _count, Math.Clamp(value, 0, Core.Drawing.DrawSession.MaxCount))) return;
            OnPropertyChanged(nameof(IsShort));
            DecreaseCommand.NotifyCanExecuteChanged();
            IncreaseCommand.NotifyCanExecuteChanged();
            _changed(this);
        }
    }

    bool CanDecrease() => Count > 0;
    bool CanIncrease() => Count < Core.Drawing.DrawSession.MaxCount;

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    void Decrease() => Count--;

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    void Increase() => Count++;
}

/// <summary>抽題設定頁：方案的選擇與管理，以及每個分類的開關與數量。修改後立即存檔。</summary>
public sealed partial class DrawSettingsViewModel : ObservableObject
{
    readonly AppServices app;

    public DrawSettingsViewModel(AppServices app)
    {
        this.app = app;
        app.DataChanged += OnDataChanged;
    }

    void OnDataChanged(object? sender, EventArgs e) => Load();

    bool _loading;

    public ObservableCollection<CategoryRowViewModel> Rows { get; } = new();

    Preset? CurrentPreset => app.Presets.Find(app.Store.Settings.LastPresetId);

    public string PresetText => CurrentPreset is { } p ? L.F("Mobile_PresetLabel", p.Name) : L.T("Mobile_NoPreset");
    public bool HasPresets => app.Presets.Presets.Count > 0;

    public string SelectionSummary
    {
        get
        {
            var active = Rows.Where(r => r.IsChecked && r.Count > 0 && r.LibraryEnabled).ToList();
            return L.F("Draw_SelectionSummary", active.Count, active.Sum(r => r.Count));
        }
    }

    public void Load()
    {
        _loading = true;
        Rows.Clear();
        var lib = app.Store.Library;
        foreach (var cat in lib.OrderedCategories())
        {
            var (_, drawable) = LibraryEditor.CountEntries(lib, cat);
            Rows.Add(new CategoryRowViewModel(cat, drawable, app.Store.GetDrawSetting(cat), OnRowChanged));
        }
        _loading = false;
        NotifyPreset();
    }

    async void OnRowChanged(CategoryRowViewModel row)
    {
        OnPropertyChanged(nameof(SelectionSummary));
        if (_loading) return;
        await app.TryAsync(() => app.Draw.SetDrawSetting(row.Id, row.IsChecked, row.Count), L.T("What_Settings"));
    }

    void NotifyPreset()
    {
        OnPropertyChanged(nameof(PresetText));
        OnPropertyChanged(nameof(HasPresets));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    // ── 分類 ───────────────────────────────────────────────

    [RelayCommand]
    async Task CheckAll()
    {
        await app.TryAsync(() => app.Draw.SetAllEnabled(true), L.T("What_Settings"));
        Load();
    }

    [RelayCommand]
    async Task CheckNone()
    {
        await app.TryAsync(() => app.Draw.SetAllEnabled(false), L.T("What_Settings"));
        Load();
    }

    [RelayCommand]
    async Task ResetDefaults()
    {
        if (!await Dialogs.Confirm(L.T("Mobile_ConfirmReset"))) return;
        await app.TryAsync(app.Draw.ResetToDefaults, L.T("What_Settings"));
        Load();
    }

    // ── 方案 ───────────────────────────────────────────────

    [RelayCommand]
    async Task ChoosePreset()
    {
        if (!HasPresets)
        {
            await Dialogs.Info(L.T("Mobile_NoPresetsYet"));
            return;
        }
        var presets = app.Presets.Presets.ToList();
        var index = await Dialogs.Choose(L.T("Mobile_ChoosePreset"), presets.Select(p => p.Name).ToList());
        if (index < 0) return;
        await app.TryAsync(() => app.Presets.Apply(presets[index].Id), L.T("What_Settings"));
        Load();
    }

    [RelayCommand]
    async Task SavePresetAs()
    {
        var initial = CurrentPreset is { } p ? p.Name + L.T("Draw_PresetCopySuffix") : L.T("Draw_DefaultPresetName");
        var name = await Dialogs.Prompt(L.T("Draw_SavePresetTitle"), L.T("Draw_PresetNameLabel"), initial);
        if (name is null) return;
        await app.TryAsync(() => app.Presets.SaveAs(name), L.T("What_Presets"));
        NotifyPreset();
    }

    [RelayCommand]
    async Task PresetActions()
    {
        if (CurrentPreset is not { } p)
        {
            await Dialogs.Info(HasPresets ? L.T("Mobile_NoPreset") : L.T("Mobile_NoPresetsYet"));
            return;
        }
        string[] actions = [L.T("Draw_Overwrite"), L.T("Draw_Rename"), L.T("Common_Delete")];
        switch (await Dialogs.Choose(p.Name, actions))
        {
            case 0:
                if (await Dialogs.Confirm(L.F("Mobile_OverwritePreset", p.Name)))
                    await app.TryAsync(() => app.Presets.Overwrite(p.Id), L.T("What_Presets"));
                break;
            case 1:
                var name = await Dialogs.Prompt(L.T("Draw_RenamePresetTitle"), L.T("Draw_NewNameLabel"), p.Name);
                if (name is not null) await app.TryAsync(() => app.Presets.Rename(p.Id, name), L.T("What_Presets"));
                break;
            case 2:
                if (await Dialogs.Confirm(L.F("Draw_DeletePreset", p.Name)))
                    await app.TryAsync(() => app.Presets.Delete(p.Id), L.T("What_Presets"));
                break;
        }
        NotifyPreset();
    }
}
