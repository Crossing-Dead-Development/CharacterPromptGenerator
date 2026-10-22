using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Localization;
using CommunityToolkit.Maui.Storage;

namespace CharacterPromptGenerator.Mobile.Services;

/// <summary>
/// 透過系統的檔案選擇器與「儲存檔案」對話框和 App 以外交換檔案。
/// 不需要任何儲存權限；Android 的 content URI 一律以串流讀寫，不假設有實體路徑。
/// </summary>
public static class FileExchange
{
    // Android 的檔案管理員常把 .json／.csv 標成 application/octet-stream，所以不以 MIME 過濾，
    // 改由 ImportReader 依副檔名判斷並給出清楚的錯誤訊息
    static readonly FilePickerFileType WordBankTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["*/*"],
        [DevicePlatform.iOS] = ["public.json", "public.comma-separated-values-text", "public.plain-text"],
    });

    static readonly FilePickerFileType ZipTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["application/zip", "application/x-zip-compressed", "application/octet-stream"],
        [DevicePlatform.iOS] = ["public.zip-archive"],
    });

    public sealed record PickedFile(string FileName, byte[] Bytes);

    public static Task<PickedFile?> PickWordBank() => Pick(WordBankTypes, L.T("Common_Import"));

    public static Task<PickedFile?> PickBackup() => Pick(ZipTypes, L.T("Mobile_RestoreFullBackup"));

    /// <summary>讓使用者選一個檔案並讀成位元組；取消時回傳 null。超過大小上限時丟出例外。</summary>
    static async Task<PickedFile?> Pick(FilePickerFileType types, string title)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = types });
        if (result is null) return null;
        await using var stream = await result.OpenReadAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > TextFileLoader.MaxBytes)
                throw new IOException(L.F("File_TooLarge", buffer.Length / 1024 / 1024));
        }
        return new PickedFile(result.FileName, buffer.ToArray());
    }

    /// <summary>讓使用者選擇存檔位置並寫入；成功時回傳顯示用的位置，取消時回傳 null，失敗時丟出例外。</summary>
    public static async Task<string?> Save(string fileName, byte[] content)
    {
        using var stream = new MemoryStream(content);
        var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
        if (result.IsSuccessful) return result.FilePath;
        if (result.Exception is null or OperationCanceledException || IsCancellation(result.Exception)) return null;
        throw result.Exception;
    }

    /// <summary>用系統分享面板分享文字（傳訊息、存到備忘錄…）。</summary>
    public static Task ShareText(string text) =>
        Share.Default.RequestAsync(new ShareTextRequest { Text = text, Title = L.T("Mobile_ShareTitle") });

    static bool IsCancellation(Exception ex) => ex.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase);
}
