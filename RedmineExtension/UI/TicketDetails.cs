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
    /// <summary>選択可能な表示項目（キーと表示名）。設定・保存クエリのローカル設定で共有する。
    /// いずれも issues.json のレスポンスに含まれる情報で、追加のリクエストは発生しない。</summary>
    public static readonly (string Key, string Label)[] Fields =
    [
        ("project", Strings.Fields.Project),
        ("tracker", Strings.Fields.Tracker),
        ("status", Strings.Fields.Status),
        ("priority", Strings.Fields.Priority),
        ("assignee", Strings.Fields.Assignee),
        ("author", Strings.Fields.Author),
        ("category", Strings.Fields.Category),
        ("version", Strings.Fields.TargetVersion),
        ("progress", Strings.Fields.Progress),
        ("estimated", Strings.Fields.EstimatedHours),
        ("start", Strings.Fields.StartDate),
        ("due", Strings.Fields.DueDate),
        ("created", Strings.Fields.Created),
        ("updated", Strings.Fields.Updated),
        ("description", Strings.Fields.Description),
    ];

    /// <summary>表示項目を選んで Details を組み立てる（fields は Fields のキー集合）。</summary>
    public static IDetails Build(IssueSummary issue, string url, IReadOnlyCollection<string> fields)
    {
        var body = new StringBuilder();

        foreach (var (key, label, value) in Rows(issue))
        {
            if (fields.Contains(key))
            {
                body.Append(CultureInfo.InvariantCulture, $"**{label}**: {Or(value)}\n\n");
            }
        }

        // 説明は全文を表示する（要約は一覧の副題側の役割）。
        if (fields.Contains("description") && !string.IsNullOrWhiteSpace(issue.Description))
        {
            body.Append(CultureInfo.InvariantCulture, $"**{Strings.Fields.Description}**:\n\n{issue.Description}\n\n");
        }

        return new Details
        {
            Title = $"#{issue.Id} {issue.Subject}",
            Body = body.ToString(),
        };
    }

    /// <summary>全項目（説明を除く）を「**ラベル**: 値」で1行ずつ並べる。空値は「—」。
    /// コメントページの説明項目など、表示項目のカスタマイズ設定に依らない全量表示用。</summary>
    public static string FieldLines(IssueSummary issue)
    {
        var body = new StringBuilder();
        foreach (var (_, label, value) in Rows(issue))
        {
            body.Append(CultureInfo.InvariantCulture, $"**{label}**: {Or(value)}\n\n");
        }

        return body.ToString();
    }

    // 説明以外の全項目（キー・ラベル・表示値）。Build / FieldLines で共有する。
    private static IEnumerable<(string Key, string Label, string? Value)> Rows(IssueSummary issue)
    {
        yield return ("project", Strings.Fields.Project, issue.Project);
        yield return ("tracker", Strings.Fields.Tracker, issue.Tracker);
        yield return ("status", Strings.Fields.Status, issue.Status);
        yield return ("priority", Strings.Fields.Priority, issue.Priority);
        yield return ("assignee", Strings.Fields.Assignee, issue.Assignee);
        yield return ("author", Strings.Fields.Author, issue.Author);
        yield return ("category", Strings.Fields.Category, issue.Category);
        yield return ("version", Strings.Fields.TargetVersion, issue.TargetVersion);
        yield return ("progress", Strings.Fields.Progress, issue.DoneRatio.ToString(CultureInfo.InvariantCulture) + "%");
        yield return ("estimated", Strings.Fields.EstimatedHours, issue.EstimatedHours);
        yield return ("start", Strings.Fields.StartDate, DateOrNull(issue.StartDate));
        yield return ("due", Strings.Fields.DueDate, DateOrNull(issue.DueDate));
        yield return ("created", Strings.Fields.Created, DateOrNull(issue.CreatedOn));
        yield return ("updated", Strings.Fields.Updated, DateOrNull(issue.UpdatedOn));
    }

    /// <summary>一覧の副題向けの短い要約（ステータス · トラッカー · 担当者 · 作成者）。空項目は省く。
    /// ホストのファジー絞り込みは副題も対象のため、検索キーにもなる。</summary>
    public static string Subtitle(IssueSummary issue)
    {
        var parts = new List<string>();
        foreach (var value in new[] { issue.Status, issue.Tracker, issue.Assignee, issue.Author })
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add(value);
            }
        }

        return string.Join(" · ", parts);
    }

    private static string Or(string? value) => string.IsNullOrEmpty(value) ? "—" : value;

    // ISO 日時/日付。日付部分（先頭10文字）だけ表示する。空は null のまま返す。
    private static string? DateOrNull(string? value) =>
        string.IsNullOrEmpty(value) ? null : (value.Length >= 10 ? value[..10] : value);
}
