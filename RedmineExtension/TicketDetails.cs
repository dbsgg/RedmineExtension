using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>フォーカス時に右ペインへ出すチケットの基本情報（Details）を組み立てる。</summary>
internal static class TicketDetails
{
    public static IDetails Build(IssueSummary issue, string url)
    {
        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"**トラッカー**: {Or(issue.Tracker)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**ステータス**: {Or(issue.Status)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**優先度**: {Or(issue.Priority)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**担当者**: {Or(issue.Assignee)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**作成者**: {Or(issue.Author)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**進捗**: {issue.DoneRatio}%\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**開始日**: {Date(issue.StartDate)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**期日**: {Date(issue.DueDate)}\n\n");
        body.Append(CultureInfo.InvariantCulture, $"**更新**: {Date(issue.UpdatedOn)}\n\n");

        var description = Snippet(issue.Description);
        if (description.Length > 0)
        {
            body.Append(CultureInfo.InvariantCulture, $"**説明**:\n\n{description}\n\n");
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
