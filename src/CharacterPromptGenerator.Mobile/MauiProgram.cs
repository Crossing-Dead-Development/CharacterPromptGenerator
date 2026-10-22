using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.ViewModels;
using CharacterPromptGenerator.Mobile.Views;
using CommunityToolkit.Maui;

namespace CharacterPromptGenerator.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseMauiCommunityToolkit();

        // 資料在第一次取用時才開啟；開啟失敗由 App.CreateWindow 接手顯示錯誤畫面
        builder.Services.AddSingleton(_ => AppServices.Open());
        Page<DrawPage, DrawViewModel>(builder);
        Page<DrawSettingsPage, DrawSettingsViewModel>(builder);
        Page<LibraryPage, LibraryViewModel>(builder);
        Page<EntriesPage, EntriesViewModel>(builder);
        Page<EntryEditPage, EntryEditViewModel>(builder);
        Page<BulkAddPage, BulkAddViewModel>(builder);
        Page<CategoryEditPage, CategoryEditViewModel>(builder);
        Page<SettingsPage, SettingsViewModel>(builder);
        Page<ImportPreviewPage, ImportPreviewViewModel>(builder);
        Page<BackupsPage, BackupsViewModel>(builder);
        Page<HistoryPage, HistoryViewModel>(builder);

        return builder.Build();
    }

    static void Page<TPage, TViewModel>(MauiAppBuilder builder) where TPage : Page where TViewModel : class
    {
        builder.Services.AddTransient<TPage>();
        builder.Services.AddTransient<TViewModel>();
    }
}
