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
        Name = Strings.Common.CopyLink;
        Icon = new IconInfo(""); // Link
    }

    public override CommandResult Invoke()
    {
        if (!_api.IsConfigured)
        {
            return CommandResult.ShowToast(Strings.Setup.ConfigureFirst);
        }

        var url = _api.IssueUrl(_id);
        var label = $"#{_id}";

        // タイトル取得(最大10秒)を同期待ちするとパレットが固まるため、背景で実行する。
        BackgroundJob.Run(
            Strings.Tickets.Copying(_id),
            async () =>
            {
                string? subject = null;
                try
                {
                    subject = await _api.GetIssueSubjectAsync(_id).ConfigureAwait(false);
                }
                catch
                {
                    // タイトル取得に失敗してもリンク自体はコピーする（#番号のみ）。
                }

                if (!string.IsNullOrWhiteSpace(subject))
                {
                    label = $"#{_id} {subject}";
                }

                _history.Record(_id, subject);
                RichLinkClipboard.SetHyperlink(label, url);
            },
            () => Strings.Tickets.Copied(label),
            Strings.Tickets.CopyFailed);

        return CommandResult.KeepOpen();
    }
}
