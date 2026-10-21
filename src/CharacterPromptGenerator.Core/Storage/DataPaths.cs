using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>所有使用者資料的位置。預設在 %AppData%，可攜模式在 EXE 旁的 Data\。</summary>
public sealed class DataPaths
{
    public const string PortableFlagName = "portable.flag";

    public string DataDir { get; }
    public bool IsPortable { get; }
    /// <summary>可攜標記存在但無法寫入時的說明，給 UI 顯示。</summary>
    public string? Notice { get; init; }

    public string LibraryFile => Path.Combine(DataDir, "library.json");
    public string PresetsFile => Path.Combine(DataDir, "presets.json");
    public string SettingsFile => Path.Combine(DataDir, "settings.json");
    public string HistoryFile => Path.Combine(DataDir, "history.json");
    public string BackupDir => Path.Combine(DataDir, "Backups");

    public DataPaths(string dataDir, bool isPortable = false)
    {
        DataDir = Path.GetFullPath(dataDir);
        IsPortable = isPortable;
    }

    public static string DefaultAppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CrossingDead", "CharacterPromptGenerator");

    public static string PortableDir(string exeDir) => Path.Combine(exeDir, "Data");

    public static DataPaths Resolve(string exeDir)
    {
        if (File.Exists(Path.Combine(exeDir, PortableFlagName)))
        {
            var dir = PortableDir(exeDir);
            if (CanWrite(dir)) return new DataPaths(dir, isPortable: true);
            return new DataPaths(DefaultAppDataDir)
            {
                Notice = L.F("Paths_PortableNotWritable", dir),
            };
        }
        return new DataPaths(DefaultAppDataDir);
    }

    public static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
