using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedmineExtension;

/// <summary>履歴1件分(チケット番号・タイトル・最終利用日時)。</summary>
internal sealed class TicketHistoryEntry
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public DateTime LastUsedUtc { get; set; }
}

// source-gen により反射なしで(直列|逆直列)化 → AOT/トリミング安全。
[JsonSerializable(typeof(List<TicketHistoryEntry>))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 直近に開いた / コピーしたチケットの履歴。MSIX パッケージの LocalState に
/// ticket-history.json として永続化する(取得できない環境ではメモリのみ)。
/// </summary>
internal sealed class TicketHistory
{
    private readonly int _maxEntries;
    private readonly object _lock = new();
    private readonly string? _filePath;
    private readonly List<TicketHistoryEntry> _entries;

    /// <param name="maxEntries">保持する最大件数。表示件数より多めに保持しておく。</param>
    public TicketHistory(int maxEntries)
    {
        _maxEntries = maxEntries;
        _filePath = TryGetFilePath();
        _entries = Load();
    }

    public IReadOnlyList<TicketHistoryEntry> Recent
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>チケットを履歴の先頭へ。既存があれば前へ移動し、タイトルを補完する。</summary>
    public void Record(int id, string? title)
    {
        lock (_lock)
        {
            var existing = _entries.FirstOrDefault(e => e.Id == id);
            var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title : existing?.Title;

            if (existing is not null)
            {
                _entries.Remove(existing);
            }

            _entries.Insert(0, new TicketHistoryEntry
            {
                Id = id,
                Title = resolvedTitle,
                LastUsedUtc = DateTime.UtcNow,
            });

            if (_entries.Count > _maxEntries)
            {
                _entries.RemoveRange(_maxEntries, _entries.Count - _maxEntries);
            }

            Save();
        }
    }

    private List<TicketHistoryEntry> Load()
    {
        try
        {
            if (_filePath is not null && File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                var list = JsonSerializer.Deserialize(stream, HistoryJsonContext.Default.ListTicketHistoryEntry);
                if (list is not null)
                {
                    return list.Take(_maxEntries).ToList();
                }
            }
        }
        catch
        {
            // 壊れたファイルは無視して空履歴で始める。
        }

        return new List<TicketHistoryEntry>();
    }

    private void Save()
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            using var stream = File.Create(_filePath);
            JsonSerializer.Serialize(stream, _entries, HistoryJsonContext.Default.ListTicketHistoryEntry);
        }
        catch
        {
            // 保存失敗(権限・容量等)は致命的でないため無視。
        }
    }

    private static string? TryGetFilePath()
    {
        try
        {
            var dir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(dir, "ticket-history.json");
        }
        catch
        {
            // 非パッケージ実行など LocalState を取れない場合はメモリのみ。
            return null;
        }
    }
}
