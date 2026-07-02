using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>チケットの URL を既定ブラウザで開き、履歴へ記録する。</summary>
internal sealed partial class OpenTicketCommand : InvokableCommand
{
    private readonly string _url;
    private readonly int _id;
    private readonly Func<string?> _titleProvider;
    private readonly TicketHistory _history;

    public OpenTicketCommand(string url, int id, Func<string?> titleProvider, TicketHistory history)
    {
        _url = url;
        _id = id;
        _titleProvider = titleProvider;
        _history = history;
        Name = Strings.Common.OpenInBrowser;
        Icon = new IconInfo(""); // Globe
    }

    public override CommandResult Invoke()
    {
        // タイトルは取得済みなら記録する(未取得なら null で id のみ)。
        _history.Record(_id, _titleProvider());

        try
        {
            Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true });
        }
        catch
        {
            // 既定ブラウザの起動失敗は致命的でないため無視。
        }

        return CommandResult.Dismiss();
    }
}
