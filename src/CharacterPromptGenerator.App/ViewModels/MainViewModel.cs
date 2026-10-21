using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    object _currentPage;
    UiLanguage _selectedLanguage = L.Languages.First(l => l.Code == L.Current);

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

    public string DataPathText => (Session.Store.Paths.IsPortable ? L.T("Main_PortablePrefix") : "") + Session.Store.Paths.DataDir;
    // ── 介面語言：存進 settings.json，重新啟動後生效 ───────

    public IReadOnlyList<UiLanguage> Languages => L.Languages;

    public UiLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || !Set(ref _selectedLanguage, value)) return;
            Session.Store.Settings.Language = value.Code;
            if (!Session.TrySave(Session.Store.SaveSettings, L.T("What_Settings"))) return;
            if (value.Code == L.Current) return;
            // 用目前語言與新語言各說一次，選錯語言的人也看得懂
            var message = L.T("Main_LanguageRestart") + "\n\n" + L.T("Main_LanguageRestart", value.Code);
            if (Dialogs.Confirm(message, L.T("Main_LanguageTitle"))) AppSession.Restart();
        }
    }

    public string VersionText => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0");
}
