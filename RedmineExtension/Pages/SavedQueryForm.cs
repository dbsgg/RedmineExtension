using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 保存クエリの作成/編集フォーム（貼り付け方式）。名前と、Redmine のクエリ
/// （query_id=NN / "status_id=open&amp;..." / 完全な URL）を1つ貼り付けて保存する。
/// トップレベル固定と詳細ペイン表示項目のローカル設定もここで行う（既定値は拡張設定）。
/// API キーは含めない（資格情報マネージャからヘッダ付与）。
/// </summary>
internal sealed partial class SavedQueryForm : FormContent
{
    private readonly SavedQueryStore _store;
    private readonly SavedQuery? _editing;
    private readonly SettingsManager _settings;

    public SavedQueryForm(SavedQueryStore store, SavedQuery? editing, SettingsManager settings)
    {
        _store = store;
        _editing = editing;
        _settings = settings;
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
            name = Strings.Queries.FormDefaultName;
        }

        var query = _editing ?? new SavedQuery();
        query.Name = name;
        query.PinnedToTopLevel = Get("pinned") == "true";

        var raw = Get("query").Trim();
        query.RawQuery = string.IsNullOrEmpty(raw) ? null : raw;

        // 詳細ペインのローカル設定。「既定に従う」なら null、専用指定なら選択キーの一覧。
        if (Get("detailsCustom") == "true")
        {
            query.DetailFields = Get("detailFields")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        else
        {
            query.DetailFields = null;
        }

        _store.AddOrUpdate(query);
        return CommandResult.GoHome();
    }

    private string BuildCard()
    {
        var name = _editing?.Name ?? string.Empty;
        var raw = _editing?.RawQuery ?? string.Empty;

        // 新規作成時の固定初期値は拡張設定の既定に従う。
        var pinned = (_editing?.PinnedToTopLevel ?? _settings.PinNewQueriesByDefault) ? "true" : "false";

        // 詳細ペインのローカル設定の初期値（未設定なら拡張設定の既定値を選択済みで見せる）。
        var detailsCustom = _editing?.DetailFields is not null ? "true" : "false";
        var selectedFields = _editing?.DetailFields ?? _settings.DefaultDetailFields.ToList();

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"name\",\"label\":{J(Strings.Queries.FormNameLabel)},\"isRequired\":true,\"errorMessage\":{J(Strings.Queries.FormNameRequired)},\"value\":{J(name)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"query\",\"label\":{J(Strings.Queries.FormQueryLabel)},\"isMultiline\":true,\"placeholder\":\"query_id=42 / status_id=open&assigned_to_id=me / https://redmine/issues?...\",\"value\":{J(raw)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"pinned\",\"title\":{J(Strings.Queries.FormPinToggle)},\"value\":{J(pinned)}}},");
        // 「このクエリ専用に設定する」チェックが実質のスイッチ。OFF のとき下の選択は
        // 保存時に無視される（Adaptive Card 1.5 に入力のグレーアウトは無い）。
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"detailsCustom\",\"title\":{J(Strings.Queries.FormDetailsToggle)},\"value\":{J(detailsCustom)}}},");

        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.ChoiceSet\",\"id\":\"detailFields\",\"label\":{J(Strings.Queries.FormDetailsLabel)},\"isMultiSelect\":true,\"choices\":[");
        var first = true;
        foreach (var (key, label) in TicketDetails.Fields)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(CultureInfo.InvariantCulture, $"{{\"title\":{J(label)},\"value\":{J(key)}}}");
        }

        sb.Append(CultureInfo.InvariantCulture, $"],\"value\":{J(string.Join(",", selectedFields))}}},");

        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"isSubtle\":true,\"wrap\":true,\"text\":{J(Strings.Queries.FormHint)}}}");
        sb.Append(CultureInfo.InvariantCulture, $"],\"actions\":[{{\"type\":\"Action.Submit\",\"title\":{J(Strings.Queries.FormSubmit)}}}]}}");
        return sb.ToString();
    }

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \\u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>保存クエリフォームをホストする ContentPage。</summary>
internal sealed partial class SavedQueryFormPage : ContentPage
{
    private readonly SavedQueryForm _form;

    public SavedQueryFormPage(SavedQueryStore store, SettingsManager settings, SavedQuery? editing = null)
    {
        Title = editing is null ? Strings.Queries.FormCreateTitle : Strings.Queries.FormEditTitle;
        Name = editing is null ? Strings.Queries.FormCreateName : Strings.Queries.FormEditName;
        Icon = new IconInfo(""); // glyph:E710
        _form = new SavedQueryForm(store, editing, settings);
    }

    public override IContent[] GetContent() => [_form];
}
