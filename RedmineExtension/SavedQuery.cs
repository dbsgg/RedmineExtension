using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedmineExtension;

/// <summary>フィルタ値（Redmine の id とその表示名）。</summary>
internal sealed class FilterValue
{
    public string Value { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 1 フィールド分のフィルタ条件。Op は Redmine の演算子:
/// ""=未指定 / "="=いずれか(IN) / "!"=除外 / "o"=未完了 / "c"=完了 / "*"=すべて。
/// Values は Op が "=" / "!" のときのみ使う。
/// </summary>
internal sealed class FilterCondition
{
    public string Op { get; set; } = string.Empty;

    public List<FilterValue> Values { get; set; } = new();

    public bool HasFilter => !string.IsNullOrEmpty(Op);

    public bool UsesValues => Op is "=" or "!";
}

/// <summary>
/// 保存クエリ。プロジェクト(単一)で絞り、トラッカー/ステータス/担当者を演算子＋複数値で指定する。
/// 一覧モード(list)/件数モード(count)を持つ。秘密情報は含めない。
/// </summary>
internal sealed class SavedQuery
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>"list"(一覧) または "count"(件数)。</summary>
    public string Mode { get; set; } = "list";

    public int? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public FilterCondition Tracker { get; set; } = new();

    public FilterCondition Status { get; set; } = new() { Op = "o" };

    public FilterCondition Assignee { get; set; } = new();
}

// source-gen により反射なしで直列/逆直列化 → AOT/トリミング安全。
[JsonSerializable(typeof(List<SavedQuery>))]
internal sealed partial class SavedQueryJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 保存クエリの一覧を管理し、MSIX の LocalState に saved-queries.json として永続化する。
/// 変更時に <see cref="Changed"/> を発火する(プロバイダが top-level を更新するため)。
/// </summary>
internal sealed class SavedQueryStore
{
    private readonly object _lock = new();
    private readonly string? _filePath;
    private readonly List<SavedQuery> _entries;

    public SavedQueryStore()
    {
        _filePath = TryGetFilePath();
        _entries = Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<SavedQuery> All
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
    public void AddOrUpdate(SavedQuery query)
    {
        lock (_lock)
        {
            var index = _entries.FindIndex(e => e.Id == query.Id);
            if (index >= 0)
            {
                _entries[index] = query;
            }
            else
            {
                _entries.Add(query);
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

    private List<SavedQuery> Load()
    {
        try
        {
            if (_filePath is not null && File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                var list = JsonSerializer.Deserialize(stream, SavedQueryJsonContext.Default.ListSavedQuery);
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

        return new List<SavedQuery>();
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
            JsonSerializer.Serialize(stream, _entries, SavedQueryJsonContext.Default.ListSavedQuery);
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
            return Path.Combine(dir, "saved-queries.json");
        }
        catch
        {
            return null;
        }
    }
}
