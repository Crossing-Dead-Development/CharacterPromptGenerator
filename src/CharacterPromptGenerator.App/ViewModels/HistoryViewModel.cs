using System.Collections.ObjectModel;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class HistoryItem(SavedResult result)
{
    public SavedResult Result { get; } = result;
    public string Id => Result.Id;
    public string TimeText => Result.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string Star => Result.Favorite ? "⭐" : "";
    public string Summary => string.Join(L.T("Common_ListSep"), Result.Items.Take(8).Select(i => PromptFormatter.ItemText(i.Text, i.Emoji))) +
                             (Result.Items.Count > 8 ? L.F("History_More", Result.Items.Count) : "");
    public string FullText => PromptFormatter.ToText(Result);
}

public sealed class HistoryViewModel : ObservableObject
{
    readonly AppSession _session;
    bool _favoritesOnly;
    HistoryItem? _selected;

    public HistoryViewModel(AppSession session)
    {
        _session = session;
        CopyCommand = new RelayCommand(() => Dialogs.CopyToClipboard(Selected!.FullText), () => Selected is not null);
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite, () => Selected is not null);
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        LoadCommand = new RelayCommand(() => _session.RequestLoadResult(Selected!.Result), () => Selected is not null);
        ClearCommand = new RelayCommand(ClearNonFavorites);
        session.HistoryChanged += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<HistoryItem> Items { get; } = new();

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set { if (Set(ref _favoritesOnly, value)) Refresh(); }
    }

    public HistoryItem? Selected
    {
        get => _selected;
        set
        {
            Set(ref _selected, value);
            OnPropertyChanged(nameof(SelectedText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string SelectedText => Selected?.FullText ?? L.T("History_SelectHint");
    public string Summary
    {
        get
        {
            var all = _session.Store.History;
            return L.F("History_Summary", all.Count, all.Count(h => h.Favorite), _session.Store.Settings.HistoryLimit);
        }
    }

    public ICommand CopyCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ClearCommand { get; }

    /// <summary>主畫面收藏狀態要跟著更新時由 MainViewModel 接上。</summary>
    public Action? FavoriteChanged { get; set; }

    void Refresh()
    {
        var keep = Selected?.Id;
        Items.Clear();
        foreach (var r in _session.Store.History.Where(h => !FavoritesOnly || h.Favorite))
            Items.Add(new HistoryItem(r));
        Selected = Items.FirstOrDefault(i => i.Id == keep);
        OnPropertyChanged(nameof(Summary));
    }

    void ToggleFavorite()
    {
        if (Selected is null) return;
        var r = Selected.Result;
        r.Favorite = !r.Favorite;
        if (!_session.TrySave(_session.Store.SaveHistory, L.T("What_Favorites"))) r.Favorite = !r.Favorite;
        Refresh();
        FavoriteChanged?.Invoke();
    }

    void Delete()
    {
        if (Selected is null) return;
        var r = Selected.Result;
        if (r.Favorite && !Dialogs.Confirm(L.T("History_DeleteFavorite"), warning: true)) return;
        var idx = _session.Store.History.IndexOf(r);
        _session.Store.History.Remove(r);
        if (!_session.TrySave(_session.Store.SaveHistory, L.T("What_History")) && idx >= 0) _session.Store.History.Insert(idx, r);
        Refresh();
        FavoriteChanged?.Invoke();
    }

    void ClearNonFavorites()
    {
        var count = _session.Store.History.Count(h => !h.Favorite);
        if (count == 0) return;
        if (!Dialogs.Confirm(L.F("History_Clear", count), warning: true)) return;
        var backup = _session.Store.History.ToList();
        _session.Store.History.RemoveAll(h => !h.Favorite);
        if (!_session.TrySave(_session.Store.SaveHistory, L.T("What_History")))
        {
            _session.Store.History.Clear();
            _session.Store.History.AddRange(backup);
        }
        Refresh();
        FavoriteChanged?.Invoke();
    }
}
