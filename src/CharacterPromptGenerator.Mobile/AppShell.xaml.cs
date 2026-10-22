using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;

namespace CharacterPromptGenerator.Mobile;

public partial class AppShell : Shell
{
    IReadOnlyList<string>? _loadMessages;

    // 切換介面語言時會重建 Shell；路由只能註冊一次
    static AppShell()
    {
        Routing.RegisterRoute(DrawSettingsPage.Route, typeof(DrawSettingsPage));
        Routing.RegisterRoute(EntriesPage.Route, typeof(EntriesPage));
        Routing.RegisterRoute(EntryEditPage.Route, typeof(EntryEditPage));
        Routing.RegisterRoute(BulkAddPage.Route, typeof(BulkAddPage));
        Routing.RegisterRoute(CategoryEditPage.Route, typeof(CategoryEditPage));
        Routing.RegisterRoute(ImportPreviewPage.Route, typeof(ImportPreviewPage));
        Routing.RegisterRoute(BackupsPage.Route, typeof(BackupsPage));
    }

    public AppShell(IReadOnlyList<string> loadMessages)
    {
        InitializeComponent();
        _loadMessages = loadMessages.Count > 0 ? loadMessages : null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 載入時修復、從備份還原等狀況：主畫面出現後告知一次
        if (_loadMessages is not { } messages) return;
        _loadMessages = null;
        await Dialogs.Info(L.T("App_LoadNotes") + string.Join("\n", messages), L.T("App_LoadNotesTitle"));
    }
}
