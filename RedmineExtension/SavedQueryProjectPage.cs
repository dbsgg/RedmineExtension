using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 保存クエリ作成の1段目。プロジェクトを選択するとフォーム(2段目)へ遷移する。
/// 担当者候補が選択プロジェクトのメンバーに依存するため、プロジェクトを先に決める。
/// </summary>
internal sealed partial class SavedQueryProjectPage : ListPage
{
    private readonly RedmineApi _api;
    private readonly SavedQueryStore _store;

    private IListItem[] _items;
    private bool _started;

    public SavedQueryProjectPage(RedmineApi api, SavedQueryStore store)
    {
        _api = api;
        _store = store;

        Title = "保存クエリを追加";
        Name = "追加";
        Icon = new IconInfo(""); // glyph:E710
        PlaceholderText = "プロジェクトを選択";

        _items = [new ListItem(new NoOpCommand()) { Title = "読み込み中…" }];
    }

    public override IListItem[] GetItems()
    {
        if (!_started)
        {
            _started = true;
            _ = LoadAsync();
        }

        return _items;
    }

    private async Task LoadAsync()
    {
        if (!_api.IsConfigured)
        {
            _items = [new ListItem(new NoOpCommand()) { Title = "設定で Redmine URL と API キーを入力してください。" }];
            RaiseItemsChanged();
            return;
        }

        try
        {
            var projects = await _api.GetProjectsAsync().ConfigureAwait(false);
            var list = new List<IListItem>
            {
                new ListItem(new SavedQueryFormPage(null, _api, _store))
                {
                    Title = "（全プロジェクト）",
                    Subtitle = "プロジェクトで絞らない（担当者は自分/指定なしのみ）",
                },
            };

            foreach (var project in projects)
            {
                list.Add(new ListItem(new SavedQueryFormPage(project, _api, _store))
                {
                    Title = project.Name,
                    Subtitle = "このプロジェクトで作成",
                });
            }

            _items = list.ToArray();
        }
        catch (Exception ex)
        {
            _items = [new ListItem(new NoOpCommand()) { Title = $"プロジェクト取得に失敗: {ex.Message}" }];
        }

        RaiseItemsChanged();
    }
}
