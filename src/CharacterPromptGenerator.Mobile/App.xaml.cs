using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Storage;
using CharacterPromptGenerator.Mobile.Services;
using CharacterPromptGenerator.Mobile.Views;

namespace CharacterPromptGenerator.Mobile;

public partial class App : Application
{
    readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        _services = services;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppServices.Log(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { AppServices.Log(e.Exception); e.SetObserved(); };
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page page;
        try
        {
            var app = _services.GetRequiredService<AppServices>();
            page = new AppShell(app.Store.LoadMessages);
        }
        catch (StoreLoadException ex)
        {
            // 資料無法安全開啟：不建立任何新檔、不覆寫，只顯示原因
            page = new StartupErrorPage(L.T("App_CannotOpenTitle"), ex.Message);
        }
        catch (Exception ex)
        {
            AppServices.Log(ex);
            page = new StartupErrorPage(L.T("App_StartFailedTitle"), L.F("App_StartFailed", ex.Message));
        }
        return new Window(page) { Title = L.T("Mobile_AppName") };
    }
}
