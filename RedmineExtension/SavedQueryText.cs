using System.Collections.Generic;
using System.Linq;

namespace RedmineExtension;

/// <summary>保存クエリの表示用テキスト（タイトル・条件サマリ）。ハブと top-level で共有する。</summary>
internal static class SavedQueryText
{
    /// <summary>記録済み件数があれば「{名前}: {N} 件」、無ければ名前のみ。</summary>
    public static string Title(SavedQuery query) =>
        query.Count is int count ? $"{query.Name}: {count} 件" : query.Name;

    /// <summary>副題向けの短い条件サマリ。</summary>
    public static string Describe(SavedQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.RawQuery))
        {
            var raw = query.RawQuery.Trim();
            return raw.Length > 60 ? raw[..60] + "…" : raw;
        }

        var parts = new List<string>();
        AddCondition(parts, "トラッカー", query.Tracker);
        AddCondition(parts, "ステータス", query.Status);
        AddCondition(parts, "担当者", query.Assignee);
        return parts.Count > 0 ? string.Join(" / ", parts) : "条件なし";
    }

    private static void AddCondition(List<string> parts, string label, FilterCondition condition)
    {
        if (condition is null || !condition.HasFilter)
        {
            return;
        }

        var names = string.Join(",", condition.Values.Select(v => v.Name));
        var text = condition.Op switch
        {
            "o" => $"{label}:未完了",
            "c" => $"{label}:完了",
            "*" => $"{label}:すべて",
            "=" => $"{label}:{names}",
            "!" => $"{label}≠{names}",
            _ => null,
        };

        if (!string.IsNullOrEmpty(text))
        {
            parts.Add(text);
        }
    }
}
