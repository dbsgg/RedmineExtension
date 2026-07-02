using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// パレットを固まらせないための非同期実行ヘルパー。
/// Invoke/SubmitForm は COM 応答スレッドで呼ばれるため、API 完了を
/// GetAwaiter().GetResult() 等で同期待ちするとパレット全体がフリーズする。
/// サーバー呼び出しは必ずここを通して背景実行し、進捗・結果はステータスメッセージで知らせる。
/// 結果メッセージは残り続けないよう一定時間で自動的に消す（成功=短め、失敗=長め）。
/// </summary>
internal static class BackgroundJob
{
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(10);

    /// <summary>完了通知だけを出す（背景処理なし。フォーム保存の成功表示などに使う）。自動で消える。</summary>
    public static void Notify(string message) => ShowTransient(message, MessageState.Success);

    /// <summary>work を背景で実行する。実行中は progress を、完了時は成否メッセージを表示する。</summary>
    public static void Run(string progressMessage, Func<Task> work, Func<string> successMessage, Func<string, string> failureMessage)
    {
        var progress = new StatusMessage { Message = progressMessage, State = MessageState.Info };
        ExtensionHost.ShowStatus(progress, StatusContext.Page);

        _ = Task.Run(async () =>
        {
            string message;
            var state = MessageState.Success;
            try
            {
                await work().ConfigureAwait(false);
                message = successMessage();
            }
            catch (Exception ex)
            {
                message = failureMessage(ex.Message);
                state = MessageState.Error;
            }

            ExtensionHost.HideStatus(progress);
            ShowTransient(message, state);
        });
    }

    // 表示して一定時間後に自動で消す（表示しっぱなし防止）。
    private static void ShowTransient(string message, MessageState state)
    {
        var status = new StatusMessage { Message = message, State = state };
        ExtensionHost.ShowStatus(status, StatusContext.Page);

        var duration = state == MessageState.Error ? ErrorDuration : SuccessDuration;
        _ = Task.Run(async () =>
        {
            await Task.Delay(duration).ConfigureAwait(false);
            ExtensionHost.HideStatus(status);
        });
    }
}
