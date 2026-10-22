using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Mobile.Services;

/// <summary>首次啟動的預設詞庫與方案：內嵌在 App 中（與 Windows 版共用同一份檔案）。</summary>
public sealed class EmbeddedSeedSource : ISeedSource
{
    public string? ReadLibraryJson() => Read("default_library.json");
    public string? ReadPresetsJson() => Read("default_presets.json");

    static string? Read(string name)
    {
        try
        {
            using var stream = typeof(EmbeddedSeedSource).Assembly.GetManifestResourceStream("SeedData." + name);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>整個 App 共用的資料與抽題狀態。資料放在 App 專屬儲存區，不需要任何權限。</summary>
public sealed class AppServices
{
    AppServices(AppStore store)
    {
        Store = store;
        Draw = new DrawSession(store);
        Presets = new PresetManager(store);
    }

    public AppStore Store { get; }
    public DrawSession Draw { get; }
    public PresetManager Presets { get; }

    public static string DataDir => FileSystem.AppDataDirectory;

    /// <summary>開啟資料。無法安全開啟時丟出 <see cref="StoreLoadException"/>，不會建立或覆寫任何檔案。</summary>
    public static AppServices Open()
    {
        var paths = new DataPaths(DataDir);
        // 介面語言要在載入資料（會產生提示訊息）之前決定；沒有設定時跟隨系統語言
        L.Use(AppStore.PeekLanguage(paths));
        return new AppServices(AppStore.Open(paths, new EmbeddedSeedSource()));
    }

    /// <summary>執行會寫檔的動作；失敗時顯示原因並回傳 false。資料修改本身是原子的，不會留下半套資料。</summary>
    public async Task<bool> TryAsync(Action action, string what)
    {
        try
        {
            action();
            return true;
        }
        catch (LibraryValidationException ex)
        {
            await Dialogs.Error(ex.Message);
        }
        catch (Exception ex)
        {
            Log(ex);
            await Dialogs.Error(L.F("Session_SaveWhatFailed", what, ex.Message));
        }
        return false;
    }

    public static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(Path.Combine(DataDir, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 記錄失敗時不再處理
        }
    }
}
