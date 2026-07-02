using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 保存クエリの作成/編集フォーム（貼り付け方式）。名前と、Redmine のクエリ
/// （query_id=NN / "status_id=open&amp;..." / 完全な URL）を1つ貼り付けて保存する。
/// API キーは含めない（資格情報マネージャからヘッダ付与）。
/// </summary>
internal sealed partial class SavedQueryForm : FormContent
{
    private readonly SavedQueryStore _store;
    private readonly SavedQuery? _editing;

    public SavedQueryForm(SavedQueryStore store, SavedQuery? editing)
    {
        _store = store;
        _editing = editing;
        TemplateJson = BuildCard();
    }

    public override CommandResult SubmitForm(string inputs)
    {
        using var doc = JsonDocument.Parse(inputs);
        var root = doc.RootElement;

        string Get(string key) => root.TryGetProperty(key, out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

        var name = Get("name").Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = "保存クエリ";
        }

        var query = _editing ?? new SavedQuery();
        query.Name = name;
        query.PinnedToTopLevel = Get("pinned") == "true";

        var raw = Get("query").Trim();
        query.RawQuery = string.IsNullOrEmpty(raw) ? null : raw;

        _store.AddOrUpdate(query);
        return CommandResult.GoHome();
    }

    private string BuildCard()
    {
        var name = _editing?.Name ?? string.Empty;
        var raw = _editing?.RawQuery ?? string.Empty;
        var pinned = (_editing?.PinnedToTopLevel ?? false) ? "true" : "false";

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"name\",\"label\":\"名前\",\"isRequired\":true,\"errorMessage\":\"名前は必須です\",\"value\":{J(name)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"query\",\"label\":\"クエリ\",\"isMultiline\":true,\"placeholder\":\"例: query_id=42 / status_id=open&assigned_to_id=me / https://redmine/issues?...\",\"value\":{J(raw)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"pinned\",\"title\":\"トップレベルに固定表示する\",\"value\":{J(pinned)}}},");
        sb.Append("{\"type\":\"TextBlock\",\"isSubtle\":true,\"wrap\":true,\"text\":\"Redmine でフィルタを保存し、URL の query_id を貼るのが簡単です。空欄なら未完了チケットを表示します。API キーは資格情報マネージャから付与されるため、クエリに key= は不要です。\"}");
        sb.Append("],\"actions\":[{\"type\":\"Action.Submit\",\"title\":\"保存\"}]}");
        return sb.ToString();
    }

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \\u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>保存クエリフォームをホストする ContentPage。</summary>
internal sealed partial class SavedQueryFormPage : ContentPage
{
    private readonly SavedQueryForm _form;

    public SavedQueryFormPage(SavedQueryStore store, SavedQuery? editing = null)
    {
        Title = editing is null ? "保存クエリを作成" : "保存クエリを編集";
        Name = editing is null ? "作成" : "編集";
        Icon = new IconInfo(""); // glyph:E710
        _form = new SavedQueryForm(store, editing);
    }

    public override IContent[] GetContent() => [_form];
}
