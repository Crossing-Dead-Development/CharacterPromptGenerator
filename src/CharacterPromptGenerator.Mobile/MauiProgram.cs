using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.ViewModels;
using CharacterPromptGenerator.Mobile.Views;

namespace CharacterPromptGenerator.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // 資料在第一次取用時才開啟；開啟失敗由 App.CreateWindow 接手顯示錯誤畫面
        builder.Services.AddSingleton(_ => AppServices.Open());
        builder.Services.AddTransient<DrawViewModel>();
        builder.Services.AddTransient<DrawPage>();
        builder.Services.AddTransient<DrawSettingsViewModel>();
        builder.Services.AddTransient<DrawSettingsPage>();

        return builder.Build();
    }
}
