// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

internal sealed partial class RedmineExtensionPage : DynamicListPage
{
    private readonly RedmineCommandSettings _settings;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history = new();

    // チケット項目は id 単位でキャッシュし、取得完了時に Title を更新する。
    private readonly ConcurrentDictionary<int, ListItem> _ticketItems = new();

    // id → 取得済み subject(キーがあれば取得試行済み。null/空 = タイトル無し)。
    private readonly ConcurrentDictionary<int, string?> _subjects = new();
    // id → 取得失敗時のエラー文言。
    private readonly ConcurrentDictionary<int, string> _errors = new();
    private readonly HashSet<int> _loading = new();
    private readonly object _loadLock = new();

    private string _search = string.Empty;

    public RedmineExtensionPage(RedmineCommandSettings settings)
    {
        Icon = new IconInfo("");
        Title = "Redmine";
        Name = "Open";
        PlaceholderText = "チケット番号を入力（番号の後にスペースでタイトル表示）";

        _settings = settings;
        _api = new RedmineApi(settings);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _search = newSearch;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => BuildItems(_search);

    private IListItem[] BuildItems(string search)
    {
        var raw = search ?? string.Empty;
        var body = raw.TrimStart();
        if (body.StartsWith('#'))
        {
            body = body[1..];
        }

        var digitCount = 0;
        while (digitCount < body.Length && char.IsDigit(body[digitCount]))
        {
            digitCount++;
        }

        var rest = body[digitCount..];
        var restIsDelimiterOrEmpty = rest.Length == 0 || char.IsWhiteSpace(rest[0]);

        if (digitCount > 0 && restIsDelimiterOrEmpty &&
            int.TryParse(body[..digitCount], out var id) && id > 0)
        {
            // 数字の後に区切り(スペース等)が入っていれば「確定」とみなしタイトルを取得する。
            var confirmed = rest.Length > 0;
            var item = _ticketItems.GetOrAdd(id, CreateTicketItem);
            if (confirmed)
            {
                EnsureTitleLoaded(id, item);
            }

            return [item];
        }

        return DefaultItems(raw);
    }

    private ListItem CreateTicketItem(int id)
    {
        var url = _api.IssueUrl(id);
        return new ListItem(new OpenTicketCommand(url, id, () => _subjects.GetValueOrDefault(id), _history))
        {
            Title = ComposeTitle(id),
            Subtitle = url,
            Icon = new IconInfo(""), // Globe
            MoreCommands = [CopyContext(id)],
        };
    }

    private string ComposeTitle(int id)
    {
        var subject = _subjects.GetValueOrDefault(id);
        if (!string.IsNullOrEmpty(subject))
        {
            return $"#{id} {subject}";
        }

        // 取得に失敗していれば原因を表示する。
        if (_errors.TryGetValue(id, out var error))
        {
            var shortError = error.Length > 80 ? error[..80] + "…" : error;
            return $"#{id} ⚠ {shortError}";
        }

        return $"#{id} を開く";
    }

    private void EnsureTitleLoaded(int id, ListItem item)
    {
        if (_subjects.ContainsKey(id) || !_api.IsConfigured)
        {
            return;
        }

        lock (_loadLock)
        {
            if (!_loading.Add(id))
            {
                return;
            }
        }

        item.Title = $"#{id}（タイトル取得中…）";

        _ = Task.Run(async () =>
        {
            string? subject = null;
            try
            {
                subject = await _api.GetIssueSubjectAsync(id).ConfigureAwait(false);
                _errors.TryRemove(id, out _);
            }
            catch (System.Exception ex)
            {
                _errors[id] = ex.GetType().Name + ": " + ex.Message;
            }

            // 成功・失敗とも結果を記録する。
            _subjects[id] = subject;
            lock (_loadLock)
            {
                _loading.Remove(id);
            }

            // Title 更新とリスト更新通知で反映する。
            item.Title = ComposeTitle(id);
            RaiseItemsChanged();
        });
    }

    private IListItem[] DefaultItems(string raw)
    {
        var items = new List<IListItem>();

        // 検索ボックスが空のときだけ直近の履歴を表示する。
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var entry in _history.Recent)
            {
                items.Add(HistoryItem(entry));
            }
        }

        items.Add(new ListItem(new OpenUrlCommand(_settings.ServerUrl))
        {
            Title = "Redmine を開く",
            Subtitle = _settings.ServerUrl,
            Icon = new IconInfo(""),
        });

        return items.ToArray();
    }

    private ListItem HistoryItem(TicketHistoryEntry entry)
    {
        var url = _api.IssueUrl(entry.Id);
        var title = entry.Title;
        var label = string.IsNullOrWhiteSpace(title) ? $"#{entry.Id} を開く" : $"#{entry.Id} {title}";

        return new ListItem(new OpenTicketCommand(url, entry.Id, () => title, _history))
        {
            Title = label,
            Subtitle = url,
            Icon = new IconInfo(""), // History
            MoreCommands = [CopyContext(entry.Id)],
        };
    }

    private CommandContextItem CopyContext(int id) =>
        new(new CopyTicketLinkCommand(_api, id, _history))
        {
            // Ctrl+Enter で「#番号 タイトル」リンクをコピー
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true, alt: false, shift: false, win: false,
                vkey: VirtualKey.Enter, scanCode: 0),
        };
}
