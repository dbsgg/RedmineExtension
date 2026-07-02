using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>フォーカス時に右ペインへ出すチケットの基本情報（Details）を組み立てる。</summary>
internal static class TicketDetails
{
    /// <summary>選択可能な表示項目（キーと表示名）。設定・保存クエリのローカル設定で共有する。</summary>
    public static readonly (string Key, string Label)[] Fields =
    [
        ("tracker", Strings.Fields.Tracker),
        ("status", Strings.Fields.Status),
        ("priority", Strings.Fields.Priority),
        ("assignee", Strings.Fields.Assignee),
        ("author", Strings.Fields.Author),
        ("progress", Strings.Fields.Progress),
        ("start", Strings.Fields.StartDate),
        ("due", Strings.Fields.DueDate),
        ("updated", Strings.Fields.Updated),
        ("description", Strings.Fields.Description),
    ];

    /// <summary>表示項目を選んで Details を組み立てる（fields は Fields のキー集合）。</summary>
    public static IDetails Build(IssueSummary issue, string url, IReadOnlyCollection<string> fields)
    {
        var body = new StringBuilder();

        void Line(string key, string label, string value)
        {
            if (fields.Contains(key))
            {
                body.Append(CultureInfo.InvariantCulture, $"**{label}**: {value}\n\n");
            }
        }

        Line("tracker", Strings.Fields.Tracker, Or(issue.Tracker));
        Line("status", Strings.Fields.Status, Or(issue.Status));
        Line("priority", Strings.Fields.Priority, Or(issue.Priority));
        Line("assignee", Strings.Fields.Assignee, Or(issue.Assignee));
        Line("author", Strings.Fields.Author, Or(issue.Author));
        Line("progress", Strings.Fields.Progress, issue.DoneRatio.ToString(CultureInfo.InvariantCulture) + "%");
        Line("start", Strings.Fields.StartDate, Date(issue.StartDate));
        Line("due", Strings.Fields.DueDate, Date(issue.DueDate));
        Line("updated", Strings.Fields.Updated, Date(issue.UpdatedOn));

        if (fields.Contains("description"))
        {
            var description = Snippet(issue.Description);
            if (description.Length > 0)
            {
                body.Append(CultureInfo.InvariantCulture, $"**{Strings.Fields.Description}**:\n\n{description}\n\n");
            }
        }

        return new Details
        {
            Title = $"#{issue.Id} {issue.Subject}",
            Body = body.ToString(),
        };
    }

    /// <summary>一覧の副題向けの短い要約（ステータス · トラッカー · 担当者）。空項目は省く。</summary>
    public static string Subtitle(IssueSummary issue)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(issue.Status))
        {
            parts.Add(issue.Status);
        }

        if (!string.IsNullOrEmpty(issue.Tracker))
        {
            parts.Add(issue.Tracker);
        }

        if (!string.IsNullOrEmpty(issue.Assignee))
        {
            parts.Add(issue.Assignee);
        }

        return string.Join(" · ", parts);
    }

    private static string Or(string? value) => string.IsNullOrEmpty(value) ? "—" : value;

    // ISO 日時/日付。日付部分（先頭10文字）だけ表示する。
    private static string Date(string? value) =>
        string.IsNullOrEmpty(value) ? "—" : (value.Length >= 10 ? value[..10] : value);

    // 説明/コメントの冒頭。改行を詰めて最大 100 文字に切り詰める。
    private static string Snippet(string? text, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', text.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > max ? collapsed[..max] + "…" : collapsed;
    }
}
