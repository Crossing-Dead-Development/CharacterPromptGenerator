using System.IO;
using System.Windows;
using System.Windows.Threading;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.App.ViewModels;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.App;

public partial class App : Application
{
    string? _logDir;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
            MessageBox.Show(ex.Message, "無法開啟詞庫", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show($"程式啟動失敗：{ex.Message}", "啟動失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Emoji 圖庫：EXE 旁的 Emoji\fluent-emoji-3d.zip，加上使用者可自行補圖的 EmojiOverrides\
        EmojiImages.Initialize(Path.Combine(store.Paths.DataDir, "EmojiOverrides"));
        if (EmojiImages.Library.PackError is { } emojiError)
            store.LoadMessages.Add(emojiError + "\nEmoji 會以「?」替代圖示顯示，詞庫內容不受影響。");

        var session = new AppSession(store);
        var window = new MainWindow { DataContext = new MainViewModel(session) };
        MainWindow = window;
        window.Show();

        if (store.LoadMessages.Count > 0)
            Dialogs.Info("開啟資料時有以下狀況：\n\n" + string.Join("\n", store.LoadMessages), "資料載入提醒");
    }

    void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception);
        // 任何未預期的錯誤都不讓程式直接閃退；資料修改本身是原子的，不會寫出半套資料
        MessageBox.Show($"發生未預期的錯誤，操作已取消：\n{e.Exception.Message}\n\n詳細內容已記錄在 error.log。",
            "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
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
