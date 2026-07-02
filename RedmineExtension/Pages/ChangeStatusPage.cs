using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// チケットのステータスを選んで変更する簡易編集ページ（Ctrl+S で遷移）。
/// 一覧はコンテキスト構築時に先読みし、開いた瞬間に表示できるようにする。
/// 現在のステータスにだけチェックアイコンを付ける（裏で取得して反映）。
/// Enter で即変更しステータスメッセージで結果を知らせる。細かな編集は Web で行う想定。
/// </summary>
internal sealed partial class ChangeStatusPage : ListPage
{
    // ステータス一覧はサーバー単位でほぼ不変のためセッション内でキャッシュする。
    // Task で持つことで同時要求を1リクエストに束ね、失敗時だけ次回取り直す。
    private static readonly object StatusesLock = new();
    private static Task<IReadOnlyList<IssueStatus>>? _statusesTask;

    private static readonly IconInfo PageIcon = new IconInfo(""); // glyph:E70F
    private static readonly IconInfo CheckIcon = new IconInfo(""); // glyph:E73E

    private readonly int _id;
    private readonly RedmineApi _api;

    private IListItem[] _items = [new ListItem(new NoOpCommand()) { Title = Strings.Common.Loading }];
    private bool _loading;
    private bool _built;
    private bool _failed; // 失敗後は次に開いた際に必ず再試行する。

    public ChangeStatusPage(int id, RedmineApi api)
    {
        _id = id;
        _api = api;

        Id = $"redmine-status-{id}";
        Title = Strings.QuickEdit.ChangeStatusTitle(id);
        Name = Strings.QuickEdit.ChangeStatusName;
        Icon = PageIcon;
        PlaceholderText = Strings.QuickEdit.SelectStatusPlaceholder;

        // チケット項目のコンテキスト構築時点で先読みしておく（開いた際の待ちを無くす）。
        Prefetch(api);
    }

    /// <summary>ステータス一覧の先読み。失敗しても無視し、次回の要求で取り直す。</summary>
    public static void Prefetch(RedmineApi api)
    {
        if (!api.IsConfigured)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await GetStatusesCachedAsync(api).ConfigureAwait(false);
            }
            catch
            {
                // 先読み失敗は無視（開いた際に再試行される）。
            }
        });
    }

    private static Task<IReadOnlyList<IssueStatus>> GetStatusesCachedAsync(RedmineApi api)
    {
        lock (StatusesLock)
        {
            if (_statusesTask is null || _statusesTask.IsFaulted || _statusesTask.IsCanceled)
            {
                _statusesTask = api.GetStatusesAsync();
            }

            return _statusesTask;
        }
    }

    public override IListItem[] GetItems()
    {
        if (!_loading && (!_built || _failed))
        {
            _loading = true;
            _ = LoadAsync();
        }

        return _items;
    }

    private async Task LoadAsync()
    {
        try
        {
            var statuses = await GetStatusesCachedAsync(_api).ConfigureAwait(false);

            var byName = new Dictionary<string, ListItem>();
            var items = new List<IListItem>(statuses.Count + 1);
            foreach (var status in statuses)
            {
                var item = new ListItem(new SetStatusCommand(_api, _id, status))
                {
                    Title = status.Name,
                    MoreCommands = [Navigation.BackContext(), Navigation.HomeContext()],
                };
                byName[status.Name] = item;
                items.Add(item);
            }

            items.Add(Navigation.BackItem());
            _items = items.ToArray();
            _built = true;
            _failed = false;

            // 現在のステータスにだけチェックを付ける（裏で取得し、判明したらその場で反映）。
            MarkCurrentStatus(byName);
        }
        catch (Exception ex)
        {
            _failed = true;

            // Enter=再試行（RaiseItemsChanged → GetItems が _failed を見て再取得する）。
            _items = [
                new ListItem(new AnonymousCommand(() => RaiseItemsChanged())
                {
                    Name = Strings.Common.Retry,
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = Strings.Common.FailedToLoad(ex.Message),
                    Subtitle = Strings.Common.RetryHint,
                },
                Navigation.BackItem(),
            ];
        }
        finally
        {
            _loading = false;
            RaiseItemsChanged();
        }
    }

    // 現在のステータス名を取得し、一致する項目にチェックアイコンと副題を付ける。
    // 項目のプロパティ更新はその場で反映されるため RaiseItemsChanged は不要。
    private void MarkCurrentStatus(Dictionary<string, ListItem> byName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var issue = await _api.GetIssueAsync(_id).ConfigureAwait(false);
                if (issue.Status is { } current && byName.TryGetValue(current, out var item))
                {
                    item.Icon = CheckIcon;
                    item.Subtitle = Strings.QuickEdit.CurrentStatus;
                }
            }
            catch
            {
                // 現在値が分からないだけなので無視（チェック無しで表示継続）。
            }
        });
    }
}

/// <summary>選択したステータスへ変更する。成否をトーストで知らせる。</summary>
internal sealed partial class SetStatusCommand : InvokableCommand
{
    private readonly RedmineApi _api;
    private readonly int _id;
    private readonly IssueStatus _status;

    public SetStatusCommand(RedmineApi api, int id, IssueStatus status)
    {
        _api = api;
        _id = id;
        _status = status;
        Name = Strings.QuickEdit.SetThisStatus;
    }

    public override CommandResult Invoke()
    {
        // 同期待ちはパレット全体を固まらせるため、背景で実行して先にページを戻す。
        BackgroundJob.Run(
            Strings.QuickEdit.StatusChanging(_id, _status.Name),
            async () =>
            {
                await _api.UpdateIssueAsync(_id, statusId: _status.Id).ConfigureAwait(false);

                // ステータス変更は journal に載るため、コメントのキャッシュも破棄する。
                CommentsPage.Invalidate(_id);
            },
            () => Strings.QuickEdit.StatusChanged(_id, _status.Name),
            Strings.QuickEdit.UpdateFailed);

        return CommandResult.GoBack();
    }
}
