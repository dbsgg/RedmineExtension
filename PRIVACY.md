# Privacy Policy — Redmine for Command Palette

_Last updated: 2026-07-04_

This is an unofficial, open-source PowerToys Command Palette extension for Redmine. This policy
describes exactly what data the extension handles. English first; 日本語は下部。

## Summary

- The developer does **not** collect, receive, transmit, or have access to any of your data.
- There is **no telemetry, analytics, tracking, or advertising**.
- The extension communicates **only** with the Redmine server whose URL you enter. It sends data
  to no other server and to no third party.

## What the extension stores, and where

Everything is stored **locally on your device**. Nothing is uploaded to the developer.

- **Redmine API access key** — stored only in the **Windows Credential Manager** (Generic
  Credentials, DPAPI-encrypted per user). It is never written to disk in plain text and is never
  logged. It is sent only to your Redmine server, in the `X-Redmine-API-Key` request header.
- **Redmine URL and preferences** (history count, refresh interval, detail-pane fields, keybindings,
  comment order, pin defaults) — stored in the app's local data folder (LocalState) as
  `redmine-settings.json` and `ui-config.json`. These contain no secrets.
- **Saved queries and recent-ticket history** — stored locally as `saved-queries.json` and
  `ticket-history.json`. If you paste a query/URL, any `key=` credential parameter is stripped
  before it is saved.
- **Clipboard** — when you copy a link, the extension writes a hyperlink to your local clipboard.
  Nothing is transmitted.

## Network activity

- The extension makes HTTP(S) requests **only** to the Redmine base URL you configure, using the
  Redmine REST API, to read issues/comments and to make the edits you explicitly perform (status
  change, add comment).
- Prefer an `https://` URL: the API key is sent in a request header, which is in cleartext over
  plain `http://`.

## Data removal

Uninstalling the extension removes its local data folder. You can delete the stored API key at any
time from the Windows Credential Manager (target `RedmineExtension/ApiKey`).

## Contact

Questions or issues: <https://github.com/dbsgg/RedmineExtension/issues>

---

# プライバシーポリシー（日本語）

Redmine 向けの非公式・オープンソースの PowerToys コマンドパレット拡張機能です。本ポリシーは、
拡張機能が扱うデータの内容を正確に説明します。

## 要約

- 開発者はあなたのデータを**一切収集・受信・送信・閲覧しません**。
- **テレメトリ・解析・トラッキング・広告は一切ありません**。
- 拡張機能は、あなたが入力した URL の **Redmine サーバーとのみ通信**します。それ以外のサーバーや
  第三者へのデータ送信は行いません。

## 保存するデータと保存場所

すべて**お使いの端末内にローカル保存**され、開発者へアップロードされることはありません。

- **Redmine API アクセスキー** — **Windows 資格情報マネージャー**にのみ保存（汎用資格情報、
  ユーザーごとに DPAPI 暗号化）。平文でディスクに書き出さず、ログにも記録しません。送信先は
  あなたの Redmine サーバーのみで、`X-Redmine-API-Key` ヘッダーに付与されます。
- **Redmine URL と各種設定**（履歴件数、更新間隔、詳細ペイン項目、キーバインド、コメント並び順、
  固定既定）— アプリのローカルデータ（LocalState）に `redmine-settings.json` /
  `ui-config.json` として保存。秘密情報は含みません。
- **保存クエリ・最近のチケット履歴** — `saved-queries.json` / `ticket-history.json` として
  ローカル保存。クエリ/URL を貼り付けた場合、保存前に `key=` の資格情報パラメータを除去します。
- **クリップボード** — リンクをコピーすると、端末のクリップボードにハイパーリンクを書き込みます。
  外部への送信はありません。

## 通信について

- 拡張機能は、あなたが設定した Redmine のベース URL に対してのみ HTTP(S) 通信を行い、Redmine の
  REST API でチケット・コメントの取得や、あなたが明示的に行う編集（ステータス変更・コメント追加）
  を実行します。
- `https://` の URL を推奨します。API キーはリクエストヘッダーで送られるため、平文 `http://`
  ではクリアテキストになります。

## データの削除

拡張機能をアンインストールするとローカルデータフォルダーは削除されます。保存された API キーは、
Windows 資格情報マネージャー（ターゲット `RedmineExtension/ApiKey`）からいつでも削除できます。

## 連絡先

質問・不具合: <https://github.com/dbsgg/RedmineExtension/issues>
