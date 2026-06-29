using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedmineExtension;

/// <summary>
/// 保存されたカスタム検索。フィルタは Redmine /issues.json のパラメータに対応する。
/// Status/Assignee は "open"/"closed"/"*"/数値 id /"me" などの生値を保持し、API にそのまま渡す。
/// 表示名(*Name)も保存して再取得不要にする。秘密情報は含めない。
/// </summary>
internal sealed class SavedSearch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>"list"(一覧) または "count"(件数)。</summary>
    public string Mode { get; set; } = "list";

    public int? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public int? TrackerId { get; set; }

    public string? TrackerName { get; set; }

    /// <summary>status_id の生値("open"/"closed"/"*"/id)。</summary>
    public string? Status { get; set; }

    public string? StatusName { get; set; }

    /// <summary>assigned_to_id の生値("me"/id)。</summary>
    public string? Assignee { get; set; }

    public string? AssigneeName { get; set; }
}

// source-gen により反射なしで直列/逆直列化 → AOT/トリミング安全。
[JsonSerializable(typeof(List<SavedSearch>))]
internal sealed partial class SavedSearchJsonContext : JsonSerializerContext
{
}

/// <summary>
/// カスタム検索の一覧を管理し、MSIX の LocalState に saved-searches.json として永続化する。
/// 変更時に <see cref="Changed"/> を発火する(プロバイダが top-level を更新するため)。
/// </summary>
internal sealed class SavedSearchStore
{
    private readonly object _lock = new();
    private readonly string? _filePath;
    private readonly List<SavedSearch> _entries;

    public SavedSearchStore()
    {
        _filePath = TryGetFilePath();
        _entries = Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SavedSearch> All
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>新規追加(Id が既存なら置き換え)。</summary>
    public void AddOrUpdate(SavedSearch search)
    {
        lock (_lock)
        {
            var index = _entries.FindIndex(e => e.Id == search.Id);
            if (index >= 0)
            {
                _entries[index] = search;
            }
            else
            {
                _entries.Add(search);
            }

            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => e.Id == id);
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public SavedSearch? Find(string id)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.Id == id);
        }
    }

    private List<SavedSearch> Load()
    {
        try
        {
            if (_filePath is not null && File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                var list = JsonSerializer.Deserialize(stream, SavedSearchJsonContext.Default.ListSavedSearch);
                if (list is not null)
                {
                    return list;
                }
            }
        }
        catch
        {
            // 壊れたファイルは無視して空で始める。
        }

        return new List<SavedSearch>();
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
            JsonSerializer.Serialize(stream, _entries, SavedSearchJsonContext.Default.ListSavedSearch);
        }
        catch
        {
            // 保存失敗は致命的でないため無視。
        }
    }

    private static string? TryGetFilePath()
    {
        try
        {
            var dir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(dir, "saved-searches.json");
        }
        catch
        {
            return null;
        }
    }
}
