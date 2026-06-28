using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// チケットのリンクを「#番号 タイトル」のリッチ HTML リンクとしてクリップボードへコピーし、
/// 履歴へ記録する。タイトルは Redmine API から取得する。
/// </summary>
internal sealed partial class CopyTicketLinkCommand : InvokableCommand
{
    private readonly RedmineApi _api;
    private readonly int _id;
    private readonly TicketHistory _history;

    public CopyTicketLinkCommand(RedmineApi api, int id, TicketHistory history)
    {
        _api = api;
        _id = id;
        _history = history;
        Name = "リンクをコピー";
        Icon = new IconInfo(""); // Link
    }

    public override CommandResult Invoke()
    {
        if (!_api.IsConfigured)
        {
            return CommandResult.ShowToast("設定で Redmine URL と API キーを入力してください。");
        }

        var url = _api.IssueUrl(_id);

        string? subject;
        try
        {
            subject = _api.GetIssueSubjectAsync(_id).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // タイトル取得に失敗してもリンク自体はコピーを試みる。
            _history.Record(_id, null);
            try
            {
                RichLinkClipboard.SetHyperlink($"#{_id}", url);
            }
            catch
            {
                // コピーも失敗した場合は下のトーストで取得失敗のみ知らせる。
            }

            return CommandResult.ShowToast($"タイトル取得に失敗: {ex.Message}（#{_id} のみコピー）");
        }

        var label = string.IsNullOrWhiteSpace(subject) ? $"#{_id}" : $"#{_id} {subject}";
        _history.Record(_id, subject);

        try
        {
            RichLinkClipboard.SetHyperlink(label, url);
            return CommandResult.ShowToast($"コピーしました: {label}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"コピーに失敗: {ex.Message}");
        }
    }
}
