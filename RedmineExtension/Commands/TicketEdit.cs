using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// チケット簡易編集（ステータス変更・コメント追加）へのコンテキストコマンド。
/// チケットを表示する各一覧ページで共有し、キー割当を統一する。
/// </summary>
internal static class TicketEdit
{
    /// <summary>Ctrl+S=ステータスを変更。</summary>
    public static CommandContextItem StatusContext(RedmineApi api, int id) =>
        new(new ChangeStatusPage(id, api))
        {
            RequestedShortcut = Keybindings.ChangeStatus,
        };

    /// <summary>Ctrl+M=コメントを追加。</summary>
    public static CommandContextItem AddCommentContext(RedmineApi api, int id) =>
        new(new AddCommentPage(api, id))
        {
            RequestedShortcut = Keybindings.AddComment,
        };
}
