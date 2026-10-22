using System.IO.Compression;
using System.Text;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>完整備份 zip 中的 manifest.json：辨識檔案、版本與內容摘要。</summary>
public sealed class BackupManifest
{
    public const string FormatName = "CharacterPromptGenerator.FullBackup";
    public const int CurrentSchemaVersion = 1;

    public string Format { get; set; } = FormatName;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTime CreatedAt { get; set; }
    /// <summary>建立備份的平台（android／ios／windows），僅供參考。</summary>
    public string Platform { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public int Categories { get; set; }
    public int Entries { get; set; }
    public int Presets { get; set; }
    public int History { get; set; }
}

/// <summary>讀出並驗證過的完整備份內容，可以直接交給 <see cref="AppStore.RestoreFull"/>。</summary>
public sealed class FullBackupContent
{
    public required BackupManifest Manifest { get; init; }
    public required LibraryData Library { get; init; }
    public required List<Preset> Presets { get; init; }
    public required List<SavedResult> History { get; init; }
    /// <summary>備份中沒有 settings.json 時為 null。</summary>
    public AppSettings? Settings { get; init; }
}

/// <summary>
/// 完整備份：一個 zip，內含 manifest.json、library.json、presets.json、history.json、settings.json。
/// 每個 JSON 檔與 App 資料夾中的原始檔格式完全相同，所以在 Windows 上也可以直接解壓縮使用。
/// 讀取時任何一個檔案有問題就整份拒絕，不會只還原一半。
/// </summary>
public static class FullBackup
{
    public const long MaxEntryBytes = Import.TextFileLoader.MaxBytes;

    public static string SuggestedFileName(DateTime time) => $"character-prompt-backup_{time:yyyyMMdd_HHmm}.zip";

    public static void Write(AppStore store, Stream output, string platform = "", string appVersion = "")
    {
        var manifest = new BackupManifest
        {
            CreatedAt = DateTime.Now,
            Platform = platform,
            AppVersion = appVersion,
            Categories = store.Library.Categories.Count,
            Entries = store.Library.Entries.Count,
            Presets = store.Presets.Count,
            History = store.History.Count,
        };
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "manifest.json", JsonOptions.Serialize(manifest));
        Add(zip, "library.json", JsonOptions.Serialize(store.Library));
        Add(zip, "presets.json", JsonOptions.Serialize(new PresetFile { Presets = store.Presets }));
        Add(zip, "history.json", JsonOptions.Serialize(new HistoryFile { Results = store.History }));
        Add(zip, "settings.json", JsonOptions.Serialize(store.Settings));
    }

    static void Add(ZipArchive zip, string name, string json)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(new UTF8Encoding(false).GetBytes(json));
    }

    /// <summary>讀取並驗證完整備份；不修改任何資料。有問題時丟出 <see cref="LibraryValidationException"/>，訊息可直接顯示。</summary>
    public static FullBackupContent Read(Stream input)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            throw new LibraryValidationException(L.T("FullBackup_NotBackup"));
        }

        using (zip)
        {
            var manifestJson = ReadText(zip, "manifest.json") ?? throw new LibraryValidationException(L.T("FullBackup_NotBackup"));
            BackupManifest? manifest;
            try
            {
                manifest = JsonOptions.Deserialize<BackupManifest>(manifestJson);
            }
            catch
            {
                manifest = null;
            }
            if (manifest?.Format != BackupManifest.FormatName)
                throw new LibraryValidationException(L.T("FullBackup_NotBackup"));
            if (manifest.SchemaVersion > BackupManifest.CurrentSchemaVersion)
                throw new LibraryValidationException(L.F("FullBackup_Newer", manifest.SchemaVersion, BackupManifest.CurrentSchemaVersion));

            var libraryJson = ReadText(zip, "library.json") ?? throw new LibraryValidationException(L.F("FullBackup_Missing", "library.json"));
            LibraryData? library;
            string? error;
            try
            {
                if (!AppStore.TryParseLibrary(libraryJson, out library, out error))
                    throw new LibraryValidationException(L.F("FullBackup_BadFile", "library.json", error));
            }
            catch (StoreLoadException ex)
            {
                throw new LibraryValidationException(ex.Message);
            }
            LibraryValidator.Repair(library!);
            library!.SchemaVersion = LibraryData.CurrentSchemaVersion;
            LibraryValidator.EnsureValid(library);

            return new FullBackupContent
            {
                Manifest = manifest,
                Library = library,
                Presets = ReadOptional<PresetFile>(zip, "presets.json")?.Presets?.Where(p => p is not null).ToList() ?? new(),
                History = ReadOptional<HistoryFile>(zip, "history.json")?.Results?.Where(r => r is not null).ToList() ?? new(),
                Settings = ReadOptional<AppSettings>(zip, "settings.json"),
            };
        }
    }

    static T? ReadOptional<T>(ZipArchive zip, string name) where T : class
    {
        var json = ReadText(zip, name);
        if (json is null) return null;
        try
        {
            return JsonOptions.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            throw new LibraryValidationException(L.F("FullBackup_BadFile", name, ex.Message));
        }
    }

    static string? ReadText(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null) return null;
        if (entry.Length > MaxEntryBytes)
            throw new LibraryValidationException(L.F("FullBackup_TooLarge", name, MaxEntryBytes / 1024 / 1024));
        try
        {
            using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new LibraryValidationException(L.F("FullBackup_BadFile", name, ex.Message));
        }
    }
}
