using System.Text;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>先寫暫存檔並刷到磁碟，成功後再替換正式檔；正式檔的上一版保留為 .bak。</summary>
public static class AtomicFile
{
    static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string content, bool withBom = false, bool keepBak = true)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        var encoding = withBom ? new UTF8Encoding(true) : Utf8NoBom;

        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var preamble = encoding.GetPreamble();
            fs.Write(preamble);
            fs.Write(encoding.GetBytes(content));
            fs.Flush(flushToDisk: true);
        }

        try
        {
            if (File.Exists(path))
            {
                var bak = keepBak ? path + ".bak" : null;
                File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* 暫存檔刪不掉不影響正式檔 */ }
    }
}
