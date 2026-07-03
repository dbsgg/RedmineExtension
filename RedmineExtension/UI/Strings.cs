namespace RedmineExtension;

/// <summary>
/// UI 文字列カタログ。ローカライズ対象の文字列は必ずここへ集約し、ページ側にハードコードしない。
/// 日本語/英語の解決は <see cref="L10n.T"/>（プロセス起動時の表示言語で確定）。
/// 区分は機能単位: Common（全ページ共通）/ Setup（未設定誘導）/ Tickets（番号検索・履歴）/
/// Comments（説明・コメント）/ Queries（保存クエリ）/ QuickEdit（簡易編集）/
/// Fields（詳細ペイン項目名）/ SettingsUi（設定ページ）。
/// </summary>
internal static class Strings
{
    private static string T(string ja, string en) => L10n.T(ja, en);

    /// <summary>全ページで共有する操作名・状態表示。</summary>
    internal static class Common
    {
        public static readonly string Open = T("開く", "Open");
        public static readonly string OpenInBrowser = T("ブラウザで開く", "Open in browser");
        public static readonly string CopyLink = T("リンクをコピー", "Copy link");
        public static readonly string Refresh = T("最新に更新", "Refresh");
        public static readonly string Edit = T("編集", "Edit");
        public static readonly string Delete = T("削除", "Delete");
        public static readonly string Loading = T("読み込み中…", "Loading…");
        public static readonly string Back = T("戻る", "Back");
        public static readonly string Home = T("ホームへ戻る", "Go home");
        public static readonly string Retry = T("再試行", "Retry");

        // 一覧項目にはアイコンが付くため、タイトルに記号（←等）は入れない。
        public static readonly string BackItemTitle = T("前のページへ戻る", "Go back");

        public static string BackItemSubtitle(string keyHint) =>
            T($"{keyHint} でも戻れます", $"You can also press {keyHint}");

        public static string FailedToLoad(string message) =>
            T($"取得に失敗: {message}", $"Failed to load: {message}");

        public static readonly string RetryHint = T(
            "Enter で再試行（設定を直した後もここから復帰できます）",
            "Press Enter to retry (also recovers after fixing the settings)");
    }

    /// <summary>URL / API キー未設定時の誘導。</summary>
    internal static class Setup
    {
        public static readonly string RequiredTitle = T("Redmine の設定が必要です", "Redmine setup required");

        public static readonly string RequiredSubtitle = T(
            "Enter で設定を開き、URL と API キーを入力してください",
            "Press Enter to open the settings and enter the URL and API key");

        public static readonly string ConfigureFirst = T(
            "設定で Redmine URL と API キーを入力してください。",
            "Set the Redmine URL and API key in the settings.");
    }

    /// <summary>番号検索・履歴（メインページ）とチケット項目。</summary>
    internal static class Tickets
    {
        // トップレベルのメインコマンド名。拡張表示名(DisplayName="Redmine")と重複して
        // 見えないよう、動作を表す固有の名前にする。
        public static readonly string MainCommandTitle = T("チケットを開く", "Open a ticket");

        public static readonly string SearchPlaceholder = T(
            "チケット番号を入力（番号の後にスペースでタイトル表示）",
            "Type an issue number (add a space to fetch the title)");

        public static readonly string OpenTicketHint = T(
            "Enter でチケットをブラウザで開く",
            "Press Enter to open the ticket in the browser");

        public static string FetchingTitle(int id) =>
            T($"#{id}（タイトル取得中…）", $"#{id} (fetching title…)");

        public static string Copying(int id) =>
            T($"#{id} のリンクを作成しています…", $"Preparing the link for #{id}…");

        public static string Copied(string label) => T($"コピーしました: {label}", $"Copied: {label}");

        public static string CopyFailed(string message) => T($"コピーに失敗: {message}", $"Copy failed: {message}");

        public static string CopyTitleFailed(int id, string message) => T(
            $"タイトル取得に失敗: {message}（#{id} のみコピー）",
            $"Failed to fetch the title: {message} (copied #{id} only)");
    }

    /// <summary>説明・コメントページ。</summary>
    internal static class Comments
    {
        public static readonly string CommandName = T("コメントを表示", "Show comments");
        public static readonly string FilterPlaceholder = T("説明・コメントを絞り込み", "Filter description & comments");
        public static readonly string DescriptionLabel = T("説明", "Description");
        public static readonly string UnknownAuthor = T("（不明）", "(unknown)");
        public static readonly string PostedBy = T("投稿者", "Posted by");
        public static readonly string PostedAt = T("日時", "Date");
        public static readonly string NoDescription = T("（説明なし）", "(no description)");
        public static readonly string AddCommentItem = T("コメントを追加", "Add a comment");

        public static readonly string ShowOldestFirst =
            T("古い順で表示（説明を先頭に）", "Show oldest first (description on top)");

        public static readonly string ShowNewestFirst =
            T("新しい順で表示（最新を先頭に）", "Show newest first (latest on top)");

        public static string PageTitle(int id) =>
            T($"#{id} の説明・コメント", $"#{id} description & comments");

        public static string CommentLabel(int number, int total) =>
            T($"コメント {number}/{total}", $"Comment {number}/{total}");

        public static string DetailTitle(int id, string label) =>
            T($"#{id} の{label}", $"#{id} {label}");
    }

    /// <summary>保存クエリ（ハブ・結果一覧・フォーム）。</summary>
    internal static class Queries
    {
        public static readonly string HubTitle = T("保存クエリ", "Saved queries");
        public static readonly string HubSubtitle = T("保存クエリの一覧・件数・追加", "List, counts, and add saved queries");

        // 追加キーは再割当可能なので、表記は Keybindings.AddQueryLabel を埋め込む。
        public static string HubPlaceholder(string addKey) =>
            T($"保存クエリを選択（{addKey} で追加）", $"Select a saved query ({addKey} to add)");

        public static readonly string AddTitle = T("保存クエリを追加", "Add a saved query");
        public static readonly string RefreshCount = T("件数を最新に更新", "Refresh the count");
        public static readonly string PinToTopLevel = T("トップレベルに固定", "Pin to top level");
        public static readonly string UnpinFromTopLevel = T("トップレベルの固定を解除", "Unpin from top level");
        public static readonly string ResultsPlaceholder = T("クエリでファジー絞り込み", "Fuzzy-filter the results");
        public static readonly string NoMatches = T("該当チケットなし", "No matching issues");
        public static readonly string OpenListHint = T("Enter で Redmine の一覧をブラウザで開く", "Press Enter to open the list in Redmine");
        public static readonly string LoadMore = T("さらに読み込む", "Load more");
        public static readonly string NoFilters = T("条件なし", "No filters");
        public static readonly string TrackerLabel = T("トラッカー", "Tracker");
        public static readonly string StatusLabel = T("ステータス", "Status");
        public static readonly string AssigneeLabel = T("担当者", "Assignee");

        public static string TitleWithCount(string name, int count) =>
            T($"{name}: {count} 件", $"{name}: {count} issues");

        public static string ConditionOpen(string label) => T($"{label}:未完了", $"{label}: open");

        public static string ConditionClosed(string label) => T($"{label}:完了", $"{label}: closed");

        public static string ConditionAny(string label) => T($"{label}:すべて", $"{label}: any");

        // --- 作成/編集フォーム ---
        public static readonly string FormCreateTitle = T("保存クエリを作成", "Create a saved query");
        public static readonly string FormEditTitle = T("保存クエリを編集", "Edit the saved query");
        public static readonly string FormCreateName = T("作成", "Create");
        public static readonly string FormEditName = T("編集", "Edit");
        public static readonly string FormNameLabel = T("名前", "Name");
        public static readonly string FormNameRequired = T("名前は必須です", "A name is required");
        public static readonly string FormQueryLabel = T("クエリ", "Query");
        public static readonly string FormDefaultName = T("保存クエリ", "Saved query");
        public static readonly string FormPinToggle = T("トップレベルに固定表示する", "Pin to the top level");

        public static readonly string FormDetailsToggle = T(
            "詳細ペインの表示項目をこのクエリ専用に設定する",
            "Use query-specific detail pane fields");

        public static readonly string FormDetailsLabel = T("詳細ペインに表示する項目", "Detail pane fields");

        public static readonly string FormHint = T(
            "Redmine でフィルタを保存し、URL の query_id を貼るのが簡単です。空欄なら未完了チケットを表示します。API キーは資格情報マネージャから付与されるため、クエリに key= は不要です。",
            "The easiest way is to save a filter in Redmine and paste the query_id from the URL. Leave empty to show open issues. The API key is attached from the Credential Manager, so no key= is needed in the query.");

        public static readonly string FormSubmit = T("保存", "Save");
    }

    /// <summary>簡易編集（ステータス変更・コメント追加）。</summary>
    internal static class QuickEdit
    {
        public static readonly string ChangeStatusName = T("ステータスを変更", "Change status");
        public static readonly string SelectStatusPlaceholder = T("変更後のステータスを選択", "Select the new status");
        public static readonly string SetThisStatus = T("このステータスに変更", "Set this status");
        public static readonly string CurrentStatus = T("現在のステータス", "Current status");
        public static readonly string AddCommentName = T("コメントを追加", "Add comment");
        public static readonly string CommentRequired = T("コメントを入力してください", "Enter a comment");
        public static readonly string CommentEmpty = T("コメントが空です", "The comment is empty");
        public static readonly string SubmitAdd = T("追加", "Add");

        public static readonly string AddCommentHint = T(
            "追加後は前のページに戻ります。ステータス変更や細かな編集は別コマンド / Web から。",
            "Returns to the previous page after adding. Use other commands / the web UI for status changes and detailed edits.");

        public static string ChangeStatusTitle(int id) =>
            T($"#{id} のステータスを変更", $"#{id}: change status");

        public static string AddCommentTitle(int id) =>
            T($"#{id} にコメントを追加", $"#{id}: add comment");

        public static string CommentLabel(int id) => T($"#{id} へのコメント", $"Comment on #{id}");

        public static string StatusChanging(int id, string status) => T(
            $"#{id} を「{status}」に変更しています…",
            $"Changing #{id} to \"{status}\"…");

        public static string StatusChanged(int id, string status) => T(
            $"#{id} のステータスを「{status}」に変更しました",
            $"Changed #{id} to \"{status}\"");

        public static string CommentAdding(int id) => T(
            $"#{id} にコメントを追加しています…",
            $"Adding a comment to #{id}…");

        public static string CommentAdded(int id) => T(
            $"#{id} にコメントを追加しました",
            $"Added a comment to #{id}");

        public static string UpdateFailed(string message) => T($"変更に失敗: {message}", $"Update failed: {message}");

        public static string AddFailed(string message) => T($"追加に失敗: {message}", $"Failed to add: {message}");
    }

    /// <summary>「表示と操作のカスタマイズ」フォーム。</summary>
    internal static class Customize
    {
        public static readonly string PageTitle = T("表示と操作のカスタマイズ", "Appearance & shortcuts");
        public static readonly string CommandName = T("カスタマイズ", "Customize");

        public static readonly string ItemSubtitle = T(
            "詳細ペインの項目・新規クエリの固定・キーバインド",
            "Detail pane fields, pin default, and keyboard shortcuts");

        public static readonly string DetailFieldsLabel = T("詳細ペインに表示する項目（既定）", "Detail pane fields (default)");

        public static readonly string KeybindingsHeader = T(
            "キーバインド（Ctrl / Alt / Win のいずれかが必須。C+ / A+ / S+ / W+ の短縮形も可。例: Ctrl+Shift+K = C+S+K, Alt+Left, Ctrl+Delete）",
            "Shortcuts (Ctrl / Alt / Win required; C+ / A+ / S+ / W+ abbreviations allowed, e.g. Ctrl+Shift+K = C+S+K, Alt+Left, Ctrl+Delete)");

        public static readonly string ShowKeybindings = T(
            "キーバインド一覧を開く / 閉じる",
            "Show / hide the shortcut list");

        public static readonly string ShowDetailFields = T(
            "表示項目の選択を開く / 閉じる",
            "Show / hide field selection");

        public static readonly string SaveHint = T(
            "保存後、開いているページには再表示時に反映されます。",
            "Changes apply when pages are reopened.");

        public static readonly string BehaviorHeader = T("表示と動作", "Display & behavior");

        public static readonly string CommentsOldestToggle = T(
            "コメントを既定で古い順に表示する（新しい順が既定）",
            "Show comments oldest-first by default (newest-first otherwise)");

        public static readonly string Submit = T("保存", "Save");
        public static readonly string Saved = T("カスタマイズを保存しました", "Customization saved");
        public static readonly string ToggleOrderLabel = T("コメントの並び順を切替", "Toggle comment order");
        public static readonly string EditQueryLabel = T("保存クエリを編集", "Edit the saved query");
        public static readonly string DeleteQueryLabel = T("保存クエリを削除", "Delete the saved query");

        public static string InvalidBinding(string action, string text) => T(
            $"「{action}」のキー指定が不正です: {text}",
            $"Invalid shortcut for \"{action}\": {text}");

        public static string DuplicateBinding(string text) => T(
            $"キーが重複しています: {text}",
            $"Duplicate shortcut: {text}");
    }

    /// <summary>詳細ペイン（右ペイン）の項目名。TicketDetails.Fields のキーと対で使う。</summary>
    internal static class Fields
    {
        public static readonly string Project = T("プロジェクト", "Project");
        public static readonly string Category = T("カテゴリ", "Category");
        public static readonly string TargetVersion = T("対象バージョン", "Target version");
        public static readonly string Created = T("作成日", "Created");
        public static readonly string EstimatedHours = T("予定工数", "Estimated hours");
        public static readonly string Tracker = T("トラッカー", "Tracker");
        public static readonly string Status = T("ステータス", "Status");
        public static readonly string Priority = T("優先度", "Priority");
        public static readonly string Assignee = T("担当者", "Assignee");
        public static readonly string Author = T("作成者", "Author");
        public static readonly string Progress = T("進捗", "Progress");
        public static readonly string StartDate = T("開始日", "Start date");
        public static readonly string DueDate = T("期日", "Due date");
        public static readonly string Updated = T("更新日", "Updated");
        public static readonly string Description = T("説明", "Description");
    }

    /// <summary>設定ページのラベル・説明・選択肢。</summary>
    internal static class SettingsUi
    {
        public static readonly string ServerUrlLabel = "Redmine URL";
        public static readonly string ServerUrlDescription = T("例: http://redmine/", "e.g. http://redmine/");
        public static readonly string ApiKeyLabel = "API access key";

        public static readonly string ApiKeyDescription = T(
            "Redmine の API アクセスキー。Windows 資格情報マネージャに保存され、入力後この欄は空に戻ります。",
            "Your Redmine API access key. Stored in the Windows Credential Manager; this field is cleared after saving.");

        public static readonly string ApiKeyPlaceholder = T(
            "新しいキーを入力すると更新します",
            "Enter a new key to update it");

        public static readonly string HistoryCountLabel = T("履歴の表示件数", "Recent tickets shown");

        public static readonly string HistoryCountDescription = T(
            "検索ボックスが空のときに表示する直近チケットの件数。",
            "How many recent tickets to list while the search box is empty.");

        public static readonly string HistoryCountNone = T("表示しない", "None");

        public static string HistoryCountItem(int count) => T($"{count} 件", $"{count}");

        public static readonly string CountTtlLabel = T("保存クエリ件数の更新間隔", "Saved-query count refresh interval");

        public static readonly string CountTtlDescription = T(
            "件数の記録がこれより古いとき、ハブを開いた際に裏で再取得します。",
            "Counts older than this are refreshed in the background when the hub is opened.");

        public static string Minutes(int minutes) => T($"{minutes} 分", $"{minutes} min");

        public static string Hours(int hours) => T($"{hours} 時間", $"{hours} h");

        public static readonly string PinNewLabel = T(
            "新しい保存クエリを既定でトップレベルに固定",
            "Pin new saved queries to the top level by default");
    }
}
