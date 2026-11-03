using System.Security.Cryptography;
using System.Text;

namespace MacroGrid.Core.Plugins.Images;

/// <summary>
/// One plugin's pictures on disk: <c>&lt;plugin folder&gt;/.image-cache/&lt;sha256 of the id&gt;.bin</c>, the raw bytes only. At most <see cref="DefaultMaxBytes"/> in total and
/// <see cref="DefaultMaxFiles"/> files; the least recently used go first. The order is kept in memory (a file system's own access time is not reliable) and
/// seeded from the files' write times when the folder is first read; a read also touches the write time so the order survives a restart.
/// </summary>
public sealed class PluginImageCache(string pluginDir, TimeProvider? time = null, long maxBytes = PluginImageCache.DefaultMaxBytes, int maxFiles = PluginImageCache.DefaultMaxFiles)
{
    public const string FolderName = ".image-cache";
    public const long DefaultMaxBytes = 64L * 1024 * 1024;
    public const int DefaultMaxFiles = 2000;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (long Size, long Stamp)> _index = new(StringComparer.Ordinal);
    private bool _loaded;
    private long _total;
    private long _lastStamp;

    // Recently read pictures, so a layout or a fast dynamic rule does not read the disk on every look. Emptied whenever a file changes or goes.
    private const long MemoryBudgetBytes = 4L * 1024 * 1024;
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private readonly Dictionary<string, byte[]> _memory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _touched = new(StringComparer.Ordinal);
    private long _memoryBytes;

    public string Folder { get; } = Path.Combine(pluginDir, FolderName);

    public int Count
    {
        get { lock (_lock) { EnsureLoaded(); return _index.Count; } }
    }

    public long TotalBytes
    {
        get { lock (_lock) { EnsureLoaded(); return _total; } }
    }

    public static string FileNameFor(string imageId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(imageId))) + ".bin";

    public bool Contains(string imageId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _index.ContainsKey(FileNameFor(imageId));
        }
    }

    /// <summary>The picture's bytes, or null when it is not cached. A file that vanished or cannot be read is dropped from the index.</summary>
    public byte[]? TryGet(string imageId)
    {
        var name = FileNameFor(imageId);
        lock (_lock)
        {
            EnsureLoaded();
            if (!_index.TryGetValue(name, out var entry)) return null;
            var path = Path.Combine(Folder, name);
            try
            {
                var now = _time.GetUtcNow();
                _index[name] = (entry.Size, NextStamp());
                if (!_memory.TryGetValue(name, out var bytes))
                {
                    bytes = File.ReadAllBytes(path);
                    if (_memoryBytes + bytes.Length > MemoryBudgetBytes) ForgetMemory();
                    _memory[name] = bytes;
                    _memoryBytes += bytes.Length;
                }
                if (!_touched.TryGetValue(name, out var last) || now - last >= TouchInterval)
                {
                    _touched[name] = now;
                    try { File.SetLastWriteTimeUtc(path, now.UtcDateTime); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
                return bytes;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _total -= entry.Size;
                _index.Remove(name);
                ForgetMemory();
                return null;
            }
        }
    }

    /// <summary>Stores a picture (replacing one of the same id) and evicts the least recently used until the limits hold again. False when the disk refused.</summary>
    public bool Put(string imageId, byte[] bytes)
    {
        var name = FileNameFor(imageId);
        lock (_lock)
        {
            EnsureLoaded();
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, name);
                var temp = path + ".tmp";
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            ForgetMemory();
            if (_index.TryGetValue(name, out var old)) _total -= old.Size;
            _index[name] = (bytes.Length, NextStamp());
            _total += bytes.Length;
            Trim(keep: name);
            return true;
        }
    }

    /// <summary>Deletes the whole folder.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            try { if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _index.Clear();
            _total = 0;
            _loaded = true;
            ForgetMemory();
        }
    }

    private void Trim(string keep)
    {
        while ((_index.Count > maxFiles || _total > maxBytes) && _index.Count > 1)
        {
            var oldest = _index.Where(kv => kv.Key != keep).MinBy(kv => kv.Value.Stamp).Key;
            _total -= _index[oldest].Size;
            _index.Remove(oldest);
            ForgetMemory();
            try { File.Delete(Path.Combine(Folder, oldest)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void ForgetMemory()
    {
        _memory.Clear();
        _touched.Clear();
        _memoryBytes = 0;
    }

    private long NextStamp() => _lastStamp = Math.Max(_lastStamp + 1, _time.GetUtcNow().UtcTicks);

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!Directory.Exists(Folder)) return;
            // A crash between write and move leaves a temp file that nothing would read again.
            foreach (var temp in Directory.EnumerateFiles(Folder, "*.tmp")) { try { File.Delete(temp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
            foreach (var file in new DirectoryInfo(Folder).EnumerateFiles("*.bin").OrderBy(f => f.LastWriteTimeUtc))
            {
                _index[file.Name] = (file.Length, NextStamp());
                _total += file.Length;
            }
            Trim(keep: "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
