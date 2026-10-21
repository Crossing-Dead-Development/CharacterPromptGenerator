using CharacterPromptGenerator.App.Infrastructure;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    object _currentPage;

    public MainViewModel(AppSession session)
    {
        Session = session;
        Draw = new DrawViewModel(session);
        Editor = new EditorViewModel(session);
        History = new HistoryViewModel(session) { FavoriteChanged = Draw.RefreshFavoriteState };
        Data = new DataViewModel(session);
        _currentPage = Draw;
        session.LoadResultRequested += (_, _) => CurrentPage = Draw;
    }

    public AppSession Session { get; }
    public DrawViewModel Draw { get; }
    public EditorViewModel Editor { get; }
    public HistoryViewModel History { get; }
    public DataViewModel Data { get; }

    public object CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!Set(ref _currentPage, value)) return;
            foreach (var n in new[] { nameof(IsDrawPage), nameof(IsEditorPage), nameof(IsHistoryPage), nameof(IsDataPage) })
                OnPropertyChanged(n);
            if (value == Data) Data.Refresh();
        }
    }

    // 側欄 RadioButton 繫結用
    public bool IsDrawPage { get => CurrentPage == Draw; set { if (value) CurrentPage = Draw; } }
    public bool IsEditorPage { get => CurrentPage == Editor; set { if (value) CurrentPage = Editor; } }
    public bool IsHistoryPage { get => CurrentPage == History; set { if (value) CurrentPage = History; } }
    public bool IsDataPage { get => CurrentPage == Data; set { if (value) CurrentPage = Data; } }

    public string DataPathText => (Session.Store.Paths.IsPortable ? "可攜模式 · " : "") + Session.Store.Paths.DataDir;
    public string VersionText => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0");
}
