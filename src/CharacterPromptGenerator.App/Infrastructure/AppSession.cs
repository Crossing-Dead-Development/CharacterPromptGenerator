using System.Diagnostics;
using System.IO;
using System.Reflection;
using CharacterPromptGenerator.App.Views;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.App.Infrastructure;

/// <summary>EXE 旁的 SeedData\ 優先，找不到時使用內嵌的副本。</summary>
public sealed class AppSeedSource : ISeedSource
{
    public string? ReadLibraryJson() => Read("default_library.json");
    public string? ReadPresetsJson() => Read("default_presets.json");

    static string? Read(string name)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "SeedData", name);
            if (File.Exists(path)) return File.ReadAllText(path);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SeedData." + name);
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

/// <summary>整個程式共用的狀態與服務。</summary>
public sealed class AppSession(AppStore store)
{
    public AppStore Store { get; } = store;
    public DrawEngine Engine { get; } = new();
    public static string ExeDir => AppContext.BaseDirectory;

    public event EventHandler? HistoryChanged;
    public event EventHandler? PresetsChanged;
    public event EventHandler<SavedResult>? LoadResultRequested;

    public LibraryData Library => Store.Library;

    public void RaiseHistoryChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);
    public void RaisePresetsChanged() => PresetsChanged?.Invoke(this, EventArgs.Empty);
    public void RequestLoadResult(SavedResult r) => LoadResultRequested?.Invoke(this, r);

    /// <summary>執行詞庫修改；失敗時顯示原因並回傳 false（資料保持原狀）。</summary>
    public bool TryMutate(Action<LibraryData> mutate, string? backupReason = null)
    {
        try
        {
            Store.MutateLibrary(mutate, backupReason);
            return true;
        }
        catch (LibraryValidationException ex)
        {
            Dialogs.Error(ex.Message);
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Session_SaveFailed", ex.Message));
        }
        return false;
    }

    public bool TrySave(Action save, string what)
    {
        try
        {
            save();
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Session_SaveWhatFailed", what, ex.Message));
            return false;
        }
    }

    // ── 匯入／匯出（多個頁面共用） ───────────────────────

    public void RunImport()
    {
        var path = Dialogs.OpenImportFile();
        if (path is null) return;
        var window = new ImportWindow(new ViewModels.ImportViewModel(this, path)) { Owner = Dialogs.Owner };
        window.ShowDialog();
    }

    public void ExportJson()
    {
        var path = Dialogs.SaveFile($"character-prompt-library_{DateTime.Now:yyyyMMdd}.json", L.T("Session_JsonFilter"));
        if (path is null) return;
        try
        {
            LibraryExporter.ExportJson(Library, path);
            Dialogs.Info(L.F("Session_ExportedJson", Library.Categories.Count, Library.Entries.Count, path));
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Session_ExportFailed", ex.Message));
        }
    }

    public void ExportCsv()
    {
        var path = Dialogs.SaveFile($"character-prompt-library_{DateTime.Now:yyyyMMdd}.csv", L.T("Session_CsvFilter"));
        if (path is null) return;
        try
        {
            LibraryExporter.ExportCsv(Library, path);
            Dialogs.Info(L.F("Session_ExportedCsv", Library.Entries.Count, path));
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Session_ExportFailed", ex.Message));
        }
    }

    /// <summary>顯示內嵌在 EXE 中的第三方授權（Fluent Emoji 的 MIT 授權等）。</summary>
    public static void ShowLicenses()
    {
        using var stream = typeof(AppSession).Assembly.GetManifestResourceStream("Licenses.THIRD-PARTY-NOTICES.md");
        if (stream is null) return;
        using var reader = new StreamReader(stream);
        Dialogs.Info(reader.ReadToEnd().Trim(), L.T("Data_LicensesTitle"));
    }

    /// <summary>重新啟動程式（切換語言、資料位置後使用）。</summary>
    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        System.Windows.Application.Current.Shutdown();
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Session_OpenFolderFailed", ex.Message));
        }
    }
}
