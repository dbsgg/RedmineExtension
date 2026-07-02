using System.Collections.Generic;
using System.Linq;

namespace RedmineExtension;

/// <summary>保存クエリの表示用テキスト（タイトル・条件サマリ）。ハブと top-level で共有する。</summary>
internal static class SavedQueryText
{
    /// <summary>記録済み件数があれば「{名前}: {N} 件」、無ければ名前のみ。</summary>
    public static string Title(SavedQuery query) =>
        query.Count is int count ? Strings.Queries.TitleWithCount(query.Name, count) : query.Name;

    /// <summary>副題向けの短い条件サマリ。</summary>
    public static string Describe(SavedQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.RawQuery))
        {
            var raw = query.RawQuery.Trim();
            return raw.Length > 60 ? raw[..60] + "…" : raw;
        }

        var parts = new List<string>();
        AddCondition(parts, Strings.Queries.TrackerLabel, query.Tracker);
        AddCondition(parts, Strings.Queries.StatusLabel, query.Status);
        AddCondition(parts, Strings.Queries.AssigneeLabel, query.Assignee);
        return parts.Count > 0 ? string.Join(" / ", parts) : Strings.Queries.NoFilters;
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
            "o" => Strings.Queries.ConditionOpen(label),
            "c" => Strings.Queries.ConditionClosed(label),
            "*" => Strings.Queries.ConditionAny(label),
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
