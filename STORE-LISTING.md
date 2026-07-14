# Microsoft Store 掲載文ドラフト（Redmine for Command Palette）

Partner Center の各フィールドに貼り付ける下書き。英語（en-US、既定ロケール）と日本語（ja-JP）。
文字数の目安は Partner Center の各上限（Short description は簡潔に）。提出前に最新の機能・
スクリーンショットと突き合わせて調整すること。

---

## en-US（default locale）

**Product name**
Redmine for Command Palette

**Short description** (used in search results; keep it tight)
Work with Redmine from the PowerToys Command Palette — open issues, copy rich links, browse comments, run saved queries, and make quick edits.

**Description**
Redmine for Command Palette is an unofficial extension that brings your Redmine issues into the PowerToys Command Palette, so you can act on tickets without switching to the browser.

Type an issue number to jump straight to its description and comments. Recently viewed tickets stay one keystroke away in the history, and your favorite Redmine filters become saved queries — each listed with its issue count and ready to pin as a top-level command. Status changes and comments are handled right in the palette; anything more detailed opens in the browser with Ctrl+Enter.

The extension is designed to be gentle on your server (batched requests and caching) and careful with your credentials: the API key is stored only in the Windows Credential Manager (DPAPI-encrypted, per user) and is never written to disk in plain text.

Requirements
- Microsoft PowerToys with the Command Palette feature enabled.
- A Redmine server with the REST API enabled. An API access key is optional: public servers
  can be browsed read-only without one; a key is needed for private projects and for status
  changes / comments.

"Redmine" is a trademark of its respective owner. This is an independent, unofficial extension.

**Product features**（Partner Center の「製品の機能」/ App features。最大 20 行・各 200 文字）
- Open a Redmine issue by number and read its description and comments without leaving the palette
- Keep a history of recently viewed tickets, shown whenever the search box is empty
- Copy rich "#id title" links that paste as clickable hyperlinks in Teams, Outlook, and Word
- Turn Redmine filters (query_id, raw query, or URL) into saved queries with issue counts, pinnable as top-level commands
- Browse large result sets comfortably — results load 100 issues at a time with automatic paging
- Change an issue's status or add a comment right in the palette; open the browser with Ctrl+Enter for anything else
- Remap nearly every keyboard shortcut and choose which fields appear in the detail pane
- Use it in Japanese or English — the UI follows the Windows display language
- Keep credentials safe: the API key lives only in the Windows Credential Manager, never in plain text

**Search terms / keywords**
Redmine, Command Palette, PowerToys, issue tracker, tickets, project management, developer tools

**Category**: Developer tools

**Privacy policy URL**: (要設定 — 下記メモ参照)

---

## ja-JP

**製品名**
Redmine for Command Palette

**簡単な説明**（検索結果に出る短文）
PowerToys コマンドパレットから Redmine を操作 — チケットを開く、リッチリンクのコピー、コメント閲覧、保存クエリ、簡易編集。

**説明**
Redmine for Command Palette は、Redmine のチケットを PowerToys コマンドパレットに取り込み、ブラウザに切り替えずに操作できる非公式の拡張機能です。

チケット番号を入力すれば、説明とコメントへ直接ジャンプ。直近に見たチケットは履歴からすぐに開き直せます。よく使う Redmine のフィルタは「保存クエリ」としてコマンド化でき、件数の表示やトップレベルへの固定にも対応。ステータス変更やコメント追加はパレット内で完結し、それ以上の編集は Ctrl+Enter でそのままブラウザに引き継げます。

サーバーへの問い合わせはバッチ化とキャッシュで最小限に抑え、API キーは Windows 資格情報マネージャーにのみ保存します（ユーザーごとに DPAPI 暗号化）。平文でディスクに書き出すことはありません。

必要環境
- Command Palette 機能を有効にした Microsoft PowerToys。
- REST API を有効化した Redmine サーバー。API アクセスキーは任意（公開サーバーの閲覧は
  キーなしで可能。非公開プロジェクトやステータス変更・コメント追加には必要）。

「Redmine」は各権利者の商標です。本拡張は独立した非公式のものです。

**製品の機能**（最大 20 行・各 200 文字）
- 番号でチケットを開き、説明とコメントをパレット内で閲覧
- 検索ボックスが空のときは直近チケットの履歴を表示（件数は設定可能）
- 「#番号 タイトル」のリッチリンクをコピー。Teams・Outlook・Word ではクリック可能なリンクとして貼り付け
- Redmine のフィルタ（query_id・生クエリ・URL）を件数付きの保存クエリとして登録し、トップレベルに固定可能
- クエリ結果は 100 件ずつの自動ページングで、大量の結果も快適に閲覧
- ステータス変更・コメント追加はパレット内で完結。それ以上の編集は Ctrl+Enter でブラウザへ
- ほぼすべてのショートカットを再割当でき、詳細ペインの表示項目も選択可能
- 日本語・英語 UI（Windows の表示言語に追従）
- API キーは Windows 資格情報マネージャーにのみ保存。平文では保持しません

**検索キーワード**
Redmine, コマンドパレット, PowerToys, 課題管理, チケット, プロジェクト管理, 開発ツール

---

## 認定通知メモ（Notes for certification）— en

Partner Center の「認定の注意書き」欄にそのまま貼る（顧客には非公開）。

> **10.3.1 対応（v1.0.2〜）:** 拡張を「API キー省略時は匿名の読み取り専用」対応にしたため、
> **テストアカウント自体が不要**になった。審査は公開サーバー URL を入れるだけで可能。
> 資格情報の記載・プレースホルダー置換は不要（www.redmine.org は API キー認証が無効で
> キーを取得できないため、アカウント提供方式は成立しない。demo.redmine.org は停止中）。

IMPORTANT: This app is an extension for Microsoft PowerToys Command Palette. It has no
standalone window and intentionally does not appear in the Start menu / app list
(AppListEntry="none" in the manifest). All of its UI is hosted inside Command Palette,
which activates it on demand as an out-of-process COM server — this is the standard
Command Palette extension model.

NO TEST ACCOUNT IS NEEDED. There is no sign-in in this product. The extension browses
public Redmine servers anonymously (read-only) when no API key is set; an API key is only
needed for private projects and for write operations (changing an issue's status, adding
comments). The steps below use the Redmine open-source project's public tracker and
require no credentials at all.

Prerequisites
1. Install Microsoft PowerToys (Microsoft Store or https://github.com/microsoft/PowerToys)
   and make sure the "Command Palette" module is enabled in PowerToys Settings.

Test steps
2. Install this extension. Open Command Palette (default hotkey: Win+Alt+Space). If the
   palette was already running during installation, run its "Reload" command once.
3. Type "Redmine" — the extension's commands appear (e.g. "Open a ticket").
4. The first-run item "setup required" opens the extension's settings page. Enter
   https://www.redmine.org as the Redmine URL, leave "API access key" empty, save, and
   go back.
5. Type an issue number (e.g. 1) and press Enter to read its description and comments;
   Ctrl+Enter opens it in the browser. Also try the "Saved queries" command and add a query
   such as: status_id=open
6. Read/browse features exercise the extension's full UI and are sufficient to validate it.
   Changing an issue's status or adding a comment requires an API key with write permission
   on a Redmine project; without one the server rejects the write and the extension shows an
   error message — this is expected on the public tracker.

Notes
- The UI language follows the Windows display language: Japanese on Japanese Windows,
  English otherwise.
- If a tester enters an API key, it is stored only in Windows Credential Manager
  (DPAPI-encrypted); network traffic goes only to the Redmine server URL the tester enters.

---

## 制限付き機能の申告理由 — runFullTrust（Packages ページで問われる）

Partner Center の「runFullTrust 機能、および製品でどのように使用されますか?」への回答。

**en**

This app is an extension for Microsoft PowerToys Command Palette. The Command Palette
extensibility model requires extensions to be MSIX-packaged Win32 apps that run as
out-of-process COM servers (`windows.comServer` manifest extension): the host activates the
extension via classic COM (`CreateInstance` on the registered class ID) and communicates through
the Microsoft.CommandPalette.Extensions interfaces. A packaged desktop (Win32) process requires
the runFullTrust capability; the official Command Palette extension template declares it for the
same reason.

The full-trust process is used only to:
1. Host the out-of-process COM server that implements the Command Palette extension interfaces.
   The app has no UI or app-list entry of its own and runs only while the palette uses it.
2. Store the user's Redmine API key in Windows Credential Manager via the Win32 credential API
   (CredReadW/CredWriteW, DPAPI-encrypted per user), so the key is never written to disk in
   plain text.
3. Copy rich hyperlinks in the CF_HTML clipboard format via the Win32 clipboard API
   (RegisterClipboardFormat/SetClipboardData).

The app never runs elevated, has no autostart, and its only network communication is with the
Redmine server URL the user configures (internetClient).

**ja**

本アプリは Microsoft PowerToys コマンドパレットの拡張機能です。コマンドパレットの拡張モデルでは、
拡張は MSIX パッケージ化された Win32 アプリとしてアウトプロセス COM サーバー
（マニフェストの `windows.comServer`）で動作する必要があり、ホストは登録されたクラス ID への
クラシック COM アクティベーションで拡張を起動し、Microsoft.CommandPalette.Extensions
インターフェイス経由で通信します。パッケージ化されたデスクトップ（Win32）プロセスには
runFullTrust 機能の宣言が必須で、公式の拡張テンプレートも同じ理由で宣言しています。

フルトラストプロセスの用途は以下に限られます。
1. コマンドパレット拡張インターフェイスを実装するアウトプロセス COM サーバーのホスト
   （本アプリ自身の UI やアプリ一覧項目はなく、パレット利用中のみ動作）。
2. Win32 資格情報 API（CredReadW/CredWriteW）による Redmine API キーの
   Windows 資格情報マネージャーへの保存（ユーザーごとに DPAPI 暗号化、平文でのディスク保存なし）。
3. Win32 クリップボード API（RegisterClipboardFormat/SetClipboardData）による
   CF_HTML 形式のリッチリンクのコピー。

昇格（管理者権限）では動作せず、自動起動もありません。ネットワーク通信はユーザーが設定した
Redmine サーバー URL に対してのみ行います（internetClient）。

---

## 提出前チェック（Store 固有）

- [ ] バージョンを 1.0.2.0 で提出（`Package.appxmanifest` と `build-msix.ps1 -Version 1.0.2` を一致。
      1.0.0=既定アイコンで却下 / 1.0.1=テストアカウント無しで却下(10.3.1)。1.0.2 で
      API キー省略可＝アカウント不要にして対応）
- [ ] Partner Center 予約の Identity Name / Publisher を `build-msix.ps1` の引数（または CI 変数）で注入
- [x] Store アートワーク（ロゴ各サイズ）を差し替え済み（深紅グラデーション + 白 `#`。
      テンプレートの既定画像のままだと認定で落ちる）。スクリーンショットは `screenshots/`
      （1920x1080、日英、タスクバーなし）を使用
- [ ] **プライバシーポリシー URL** を用意して設定（ネットワーク通信・API キーを扱うため必須）。
      例: GitHub Pages か README のアンカーに簡潔なポリシーを置く（「入力された Redmine URL と API キーは
      ユーザー端末の資格情報マネージャーにのみ保存し、指定 Redmine サーバー以外へ送信しない」旨）
- [ ] 年齢区分アンケート・提供国/地域・価格（無料想定）を設定
- [ ] `winget search -s msstore` や直リンクでの導線を README に追記（掲載後）
