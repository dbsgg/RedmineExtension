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

Features
- Open an issue by number — type a ticket number (a leading # is allowed) and jump straight to its description and comments.
- Copy rich links — press Ctrl+C to copy a clickable "#id title" hyperlink (works in Teams, Outlook, and Word; plain text elsewhere).
- Recent history — recently viewed and copied tickets appear when the search box is empty.
- Saved queries — paste a Redmine query_id, a raw filter, or a URL; each saved query shows its issue count and can be pinned as a top-level command. Large result sets page automatically.
- Quick edits — change an issue's status or add a comment without leaving the palette. Anything more detailed is one Ctrl+Enter away in the browser.
- Fully customizable — remap almost every shortcut, choose which fields appear in the detail pane, and set the default comment order.
- Japanese and English UI, following your Windows display language.

Security
- Your API key is stored only in the Windows Credential Manager (DPAPI-encrypted, per user) and is never written to disk in plain text.

Requirements
- Microsoft PowerToys with the Command Palette feature enabled.
- A Redmine server with the REST API enabled and a personal API access key.

"Redmine" is a trademark of its respective owner. This is an independent, unofficial extension.

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

機能
- 番号でチケットを開く — チケット番号（先頭の # は任意）を入力し、説明とコメントへ直接ジャンプ。
- リッチリンクのコピー — Ctrl+C で「#番号 タイトル」のクリック可能なリンクをコピー（Teams・Outlook・Word で有効、その他はプレーンテキスト）。
- 最近の履歴 — 検索ボックスが空のとき、直近に開いた・コピーしたチケットを表示。
- 保存クエリ — Redmine の query_id・生フィルタ・URL を貼り付けて保存。各クエリは件数付きで表示し、トップレベルに固定可能。大量の結果は自動でページ読み込み。
- 簡易編集 — パレット内でステータス変更やコメント追加。細かい編集は Ctrl+Enter でブラウザへ。
- 高いカスタマイズ性 — ほぼ全てのショートカットを再割当でき、詳細ペインの表示項目やコメントの既定並び順も設定可能。
- 日本語・英語 UI（Windows の表示言語に追従）。

セキュリティ
- API キーは Windows 資格情報マネージャーにのみ保存（ユーザーごとに DPAPI 暗号化）し、平文でディスクに書き出しません。

必要環境
- Command Palette 機能を有効にした Microsoft PowerToys。
- REST API を有効化した Redmine サーバーと個人の API アクセスキー。

「Redmine」は各権利者の商標です。本拡張は独立した非公式のものです。

**検索キーワード**
Redmine, コマンドパレット, PowerToys, 課題管理, チケット, プロジェクト管理, 開発ツール

---

## 認定通知メモ（Notes for certification）— en

This extension integrates with Microsoft PowerToys Command Palette. To test:
1. Install Microsoft PowerToys (Microsoft Store or https://github.com/microsoft/PowerToys) and enable the Command Palette feature.
2. Install this extension.
3. Open Command Palette (Win+Alt+Space by default) and type "Redmine".
4. Open the extension's Settings and enter a Redmine URL and API key. A public demo server such as https://www.redmine.org can be used to reach the number-search UI, though editing requires an account/API key.
5. Type an issue number to open its description and comments; try the "Saved queries" command.

Note: full functionality requires a reachable Redmine server with the REST API enabled and a valid API key. The extension has no UI outside Command Palette (it does not appear in the app list by design).

---

## 提出前チェック（Store 固有）

- [ ] バージョンを 1.0.0.0 で提出（`Package.appxmanifest` と `build-msix.ps1 -Version 1.0.0` を一致）
- [ ] Partner Center 予約の Identity Name / Publisher を `build-msix.ps1` の引数（または CI 変数）で注入
- [ ] Store アートワーク（ロゴ各サイズ・スクリーンショット1枚以上）を差し替え
- [ ] **プライバシーポリシー URL** を用意して設定（ネットワーク通信・API キーを扱うため必須）。
      例: GitHub Pages か README のアンカーに簡潔なポリシーを置く（「入力された Redmine URL と API キーは
      ユーザー端末の資格情報マネージャーにのみ保存し、指定 Redmine サーバー以外へ送信しない」旨）
- [ ] 年齢区分アンケート・提供国/地域・価格（無料想定）を設定
- [ ] `winget search -s msstore` や直リンクでの導線を README に追記（掲載後）
