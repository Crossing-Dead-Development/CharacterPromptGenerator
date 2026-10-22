using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.App.ViewModels;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.App;

public partial class App : Application
{
    string? _logDir;

    public App()
    {
        // 介面語言要在 App.xaml 的資源（Themes\Dark.xaml）與任何資料載入訊息產生之前決定
        L.Use(AppStore.PeekLanguage(DataPaths.Resolve(AppSession.ExeDir)));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyLanguageFonts();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log(args.Exception); args.SetObserved(); };

        AppStore store;
        try
        {
            var paths = DataPaths.Resolve(AppSession.ExeDir);
            _logDir = paths.DataDir;
            store = AppStore.Open(paths, new AppSeedSource());
        }
        catch (StoreLoadException ex)
        {
            // 資料無法安全開啟：不建立任何新檔、不覆寫，直接告知並結束
            MessageBox.Show(ex.Message, L.T("App_CannotOpenTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show(L.F("App_StartFailed", ex.Message), L.T("App_StartFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Emoji 圖庫：EXE 旁的 Emoji\fluent-emoji-3d.zip，加上使用者可自行補圖的 EmojiOverrides\
        EmojiImages.Initialize(Path.Combine(store.Paths.DataDir, "EmojiOverrides"));
        if (EmojiImages.Library.PackError is { } emojiError)
            store.LoadMessages.Add(emojiError + L.T("App_EmojiFallback"));

        var session = new AppSession(store);
        var window = new MainWindow { DataContext = new MainViewModel(session) };
        MainWindow = window;
        window.Show();

        if (store.LoadMessages.Count > 0)
            Dialogs.Info(L.T("App_LoadNotes") + string.Join("\n", store.LoadMessages), L.T("App_LoadNotesTitle"));
    }

    /// <summary>
    /// 依介面語言設定字型與 xml:lang。中日文共用的漢字字形不同（例如「直」「骨」），
    /// 日文介面改用日文字型；Language 也會讓 WPF 的字型後援選擇對應語言的字形。
    /// </summary>
    void ApplyLanguageFonts()
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(L.Current)));
        if (L.Current == L.Japanese)
            Resources["UiFont"] = new FontFamily("Yu Gothic UI, Meiryo UI, Segoe UI, Segoe UI Emoji, Segoe UI Symbol");
    }

    void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        // 任何未預期的錯誤都不讓程式直接閃退；資料修改本身是原子的，不會寫出半套資料
        MessageBox.Show(L.F("App_Unexpected", e.Exception.Message),
            L.T("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var dir = _logDir ?? DataPaths.DefaultAppDataDir;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 記錄失敗時不再處理
        }
    }
}
