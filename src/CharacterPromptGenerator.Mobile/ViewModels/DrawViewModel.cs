using System.Collections.ObjectModel;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CharacterPromptGenerator.Mobile.ViewModels;

/// <summary>結果卡片中的一個詞條。點一下切換鎖定。</summary>
public sealed partial class SlotViewModel(DrawSlot slot, Action lockChanged) : ObservableObject
{
    public string Text => slot.Text;
    public string? Emoji => slot.Emoji;
    public string Notes => slot.Notes;
    /// <summary>文字本身已含 Emoji 時不再另外顯示。</summary>
    public bool HasEmoji => !string.IsNullOrEmpty(slot.Emoji) && !slot.Text.Contains(slot.Emoji, StringComparison.Ordinal);
    public bool HasNotes => !string.IsNullOrWhiteSpace(slot.Notes);

    public bool Locked => slot.Locked;
    public string LockGlyph => slot.Locked ? "🔒" : "🔓";
    public string Description => $"{slot.Text}，{(slot.Locked ? L.T("Mobile_Locked") : L.T("Mobile_Unlocked"))}";

    [RelayCommand]
    void ToggleLock()
    {
        slot.Locked = !slot.Locked;
        OnPropertyChanged(nameof(Locked));
        OnPropertyChanged(nameof(LockGlyph));
        OnPropertyChanged(nameof(Description));
        lockChanged();
    }
}

public sealed class ResultGroupViewModel(string categoryId, string categoryName, IRelayCommand<string> reroll)
{
    public string CategoryId { get; } = categoryId;
    public string CategoryName { get; } = categoryName;
    public List<SlotViewModel> Items { get; } = new();
    public IRelayCommand<string> RerollCommand { get; } = reroll;
}

/// <summary>抽題主畫面。抽題規則全部在 Core 的 DrawSession，這裡只負責確認對話框與畫面狀態。</summary>
public sealed partial class DrawViewModel : ObservableObject
{
    readonly AppServices _app;

    public DrawViewModel(AppServices app)
    {
        _app = app;
        app.DataChanged += OnDataChanged;
    }

    void OnDataChanged(object? sender, EventArgs e) => Refresh();

    DrawSession Draw => _app.Draw;

    public ObservableCollection<ResultGroupViewModel> Groups { get; } = new();

    /// <summary>最近一次動作的提示（數量不足、已複製…）。</summary>
    public string Message
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(HasMessage));
        }
    } = "";

    public bool HasMessage => Message.Length > 0;
    public bool HasResult => Draw.HasResult;
    /// <summary>詞庫沒有任何詞條（預設狀態）：結果區改顯示匯入引導。</summary>
    public bool LibraryEmpty => _app.Store.Library.Entries.Count == 0;
    public bool ShowReadyHint => !HasResult && !LibraryEmpty;

    public string PresetText => _app.Presets.Find(_app.Store.Settings.LastPresetId) is { } p
        ? L.F("Mobile_PresetLabel", p.Name)
        : L.T("Mobile_NoPreset");

    public string SelectionSummary
    {
        get
        {
            var reqs = Draw.Requests().Where(r => _app.Store.Library.FindCategory(r.CategoryId)?.Enabled == true).ToList();
            return L.F("Draw_SelectionSummary", reqs.Count, reqs.Sum(r => r.Count));
        }
    }

    public string StatusText => !HasResult
        ? L.T("Mobile_StatusEmpty")
        : L.F("Draw_Status", Draw.Slots.Count, Draw.LockedCount, Draw.GeneratedAt) + (Draw.IsFavorite ? L.T("Draw_StatusFavorite") : "");

    public string PrimaryText => HasResult ? L.T("Draw_RerollUnlocked") : L.T("Draw_Generate");
    public string FavoriteText => Draw.IsFavorite ? L.T("Draw_Unfavorite") : L.T("Draw_Favorite");

    /// <summary>畫面出現時呼叫：設定頁、詞庫可能已經改過。</summary>
    public void Refresh()
    {
        RebuildGroups();
        NotifyAll();
    }

    // ── 抽題 ───────────────────────────────────────────────

    /// <summary>主要按鈕：還沒有結果時產生，有結果時重抽未鎖定。</summary>
    [RelayCommand]
    Task Primary() => Run(Draw.RerollUnlocked);

    [RelayCommand(CanExecute = nameof(HasResult))]
    async Task RegenerateAll()
    {
        var locked = Draw.LockedCount;
        if (locked > 0 && !await Dialogs.Confirm(L.F("Draw_ConfirmRegenerate", locked))) return;
        await Run(Draw.GenerateAll);
    }

    [RelayCommand]
    Task RerollCategory(string? categoryId) =>
        categoryId is null ? Task.CompletedTask : Run(() => Draw.RerollCategory(categoryId));

    /// <summary>抽題後寫入歷史；寫入失敗時結果照樣顯示，只提示歷史沒存到（與 Windows 版相同）。</summary>
    async Task Run(Action draw)
    {
        await _app.TryAsync(draw, L.T("What_History"));
        Message = string.Join("\n", Draw.Warnings);
        Refresh();
    }

    bool CanUnlockAll() => Draw.LockedCount > 0;

    [RelayCommand(CanExecute = nameof(CanUnlockAll))]
    void UnlockAll()
    {
        Draw.UnlockAll();
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    async Task Copy()
    {
        await Clipboard.Default.SetTextAsync(Draw.ToText());
        Message = L.T("Draw_Copied");
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    async Task Favorite()
    {
        await _app.TryAsync(() => Draw.ToggleFavorite(), L.T("What_Favorites"));
        NotifyAll();
    }

    [RelayCommand]
    Task OpenSettings() => Shell.Current.GoToAsync(DrawSettingsPage.Route);

    // ── 畫面狀態 ───────────────────────────────────────────

    void RebuildGroups()
    {
        Groups.Clear();
        foreach (var (id, name, slots) in Draw.Groups())
        {
            var group = new ResultGroupViewModel(id, name, RerollCategoryCommand);
            group.Items.AddRange(slots.Select(s => new SlotViewModel(s, NotifyAll)));
            Groups.Add(group);
        }
    }

    void NotifyAll()
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(LibraryEmpty));
        OnPropertyChanged(nameof(ShowReadyHint));
        OnPropertyChanged(nameof(PresetText));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PrimaryText));
        OnPropertyChanged(nameof(FavoriteText));
        RegenerateAllCommand.NotifyCanExecuteChanged();
        UnlockAllCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        FavoriteCommand.NotifyCanExecuteChanged();
    }
}
