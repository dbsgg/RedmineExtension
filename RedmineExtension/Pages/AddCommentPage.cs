using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// チケットへコメント（注記）を1件追加する簡易フォーム（Ctrl+M で遷移）。
/// 追加後は前のページへ戻り、コメントキャッシュを破棄して再取得させる。
/// 既存コメントの編集は権限・別エンドポイントが絡むため Web で行う想定。
/// </summary>
internal sealed partial class AddCommentForm : FormContent
{
    private readonly RedmineApi _api;
    private readonly int _id;

    public AddCommentForm(RedmineApi api, int id)
    {
        _api = api;
        _id = id;
        TemplateJson = BuildCard();
    }

    public override CommandResult SubmitForm(string inputs)
    {
        using var doc = JsonDocument.Parse(inputs);
        var notes = doc.RootElement.TryGetProperty("notes", out var v) ? (v.GetString() ?? string.Empty) : string.Empty;
        notes = notes.Trim();
        if (notes.Length == 0)
        {
            return CommandResult.ShowToast(Strings.QuickEdit.CommentEmpty);
        }

        // 同期待ちはパレット全体を固まらせるため、背景で実行して先にページを戻す。
        BackgroundJob.Run(
            Strings.QuickEdit.CommentAdding(_id),
            async () =>
            {
                await _api.UpdateIssueAsync(_id, notes: notes).ConfigureAwait(false);
                CommentsPage.Invalidate(_id);
            },
            () => Strings.QuickEdit.CommentAdded(_id),
            Strings.QuickEdit.AddFailed);

        return CommandResult.GoBack();
    }

    private string BuildCard()
    {
        var sb = new StringBuilder();
        var label = Strings.QuickEdit.CommentLabel(_id);
        var error = Strings.QuickEdit.CommentRequired;
        var hint = Strings.QuickEdit.AddCommentHint;
        var submit = Strings.QuickEdit.SubmitAdd;

        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"notes\",\"label\":{J(label)},\"isMultiline\":true,\"isRequired\":true,\"errorMessage\":{J(error)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"isSubtle\":true,\"wrap\":true,\"text\":{J(hint)}}}");
        sb.Append(CultureInfo.InvariantCulture, $"],\"actions\":[{{\"type\":\"Action.Submit\",\"title\":{J(submit)}}}]}}");
        return sb.ToString();
    }

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>コメント追加フォームをホストする ContentPage。</summary>
internal sealed partial class AddCommentPage : ContentPage
{
    private static readonly IconInfo AddIcon = new IconInfo(""); // glyph:E90A

    private readonly AddCommentForm _form;

    public AddCommentPage(RedmineApi api, int id)
    {
        Title = Strings.QuickEdit.AddCommentTitle(id);
        Name = Strings.QuickEdit.AddCommentName;
        Icon = AddIcon;
        _form = new AddCommentForm(api, id);
    }

    public override IContent[] GetContent() => [_form];
}
