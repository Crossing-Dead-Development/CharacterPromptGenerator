using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CharacterPromptGenerator.Mobile.ViewModels;

/// <summary>一筆歷史紀錄：時間、收藏標記與抽題當下的文字快照。</summary>
public sealed record HistoryRow(SavedResult Result)
{
    public string TimeText => Result.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsFavorite => Result.Favorite;
    /// <summary>每個分類一行，與複製出去的內容相同（不含標題列）。</summary>
    public string Body => string.Join("\n", PromptFormatter.ToText(Result).Split('\n').Skip(1));
}

/// <summary>歷史分頁：依時間列出、只看收藏、複製、分享、收藏、載入到抽題畫面、刪除。</summary>
public sealed partial class HistoryViewModel(AppServices app) : ObservableObject
{
    public IReadOnlyList<HistoryRow> Items { get; private set; } = Array.Empty<HistoryRow>();
    public bool IsEmpty => Items.Count == 0;

    public bool FavoritesOnly
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            OnPropertyChanged(nameof(ShowAll));
            Load();
        }
    }

    public bool ShowAll => !FavoritesOnly;

    [RelayCommand]
    void UseAll() => FavoritesOnly = false;

    [RelayCommand]
    void UseFavorites() => FavoritesOnly = true;

    public string Summary
    {
        get
        {
            var all = app.History.All;
            return L.F("History_Summary", all.Count, all.Count(h => h.Favorite), app.Store.Settings.HistoryLimit);
        }
    }

    public void Load()
    {
        Items = app.History.List(FavoritesOnly).Select(r => new HistoryRow(r)).ToList();
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    async Task Tap(HistoryRow row)
    {
        var r = row.Result;
        string[] actions =
        [
            L.T("History_Copy"),
            L.T("Mobile_Share"),
            r.Favorite ? L.T("Draw_Unfavorite") : L.T("Draw_Favorite"),
            L.T("History_Load"),
            L.T("Common_Delete"),
        ];
        switch (await Dialogs.Choose(row.TimeText, actions))
        {
            case 0:
                await Clipboard.Default.SetTextAsync(PromptFormatter.ToText(r));
                await Dialogs.Toast(L.T("Draw_Copied"));
                break;
            case 1:
                await FileExchange.ShareText(PromptFormatter.ToText(r));
                break;
            case 2:
                await app.TryAsync(() => app.History.ToggleFavorite(r.Id), L.T("What_Favorites"));
                break;
            case 3:
                // 載入後全部鎖定，到抽題畫面可解除部分鎖定再重抽
                app.Draw.LoadSaved(r);
                await Shell.Current.GoToAsync("//draw");
                return;
            case 4:
                if (r.Favorite && !await Dialogs.Confirm(L.T("History_DeleteFavorite"))) return;
                await app.TryAsync(() => app.History.Delete(r.Id), L.T("What_History"));
                break;
        }
        Load();
    }

    [RelayCommand]
    async Task ClearNonFavorites()
    {
        var count = app.History.All.Count(h => !h.Favorite);
        if (count == 0) return;
        if (!await Dialogs.Confirm(L.F("History_Clear", count))) return;
        await app.TryAsync(() => app.History.ClearNonFavorites(), L.T("What_History"));
        Load();
    }
}
