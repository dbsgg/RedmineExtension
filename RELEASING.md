# 公開手順（GitHub public 化 → winget 掲載）

> ## ⚠️ 重要な訂正（2026-07-03 調査結果）
>
> 当初計画していた **unpackaged EXE 方式（Inno Setup + HKCU CLSID 登録）は CmdPal に発見されない**。
> - CmdPal の拡張発見は `AppExtensionCatalog.Open("com.microsoft.commandpalette")` のみ
>   （`WinRTExtensionService.cs`）。**MSIX パッケージの appExtension 宣言が唯一の発見経路**。
> - レジストリ（CLSID/InprocServer32/LocalServer32）を列挙する実装は PowerToys リポジトリの
>   どこにも存在しない。この方式を案内している
>   `.github/skills/publish-extension/references/winget-publishing.md` は
>   **公式拡張テンプレート由来の誤ドキュメント**（実装と不一致。リポジトリ全検索で確認済み）。
> - 実機でも確認済み: インストーラー自体は動くが、登録しても CmdPal には現れない。
>
> 従って **公開は MSIX（署名付き）経路** で行う。`setup.iss` / `build-exe.ps1` /
> release.yml の EXE 生成は「CmdPal 発見不可」の注記付きで残置（他用途・将来の仕様変更に備え）。

## 配布経路の選択肢（MSIX 前提）

| 経路 | 費用 | 手間 | 備考 |
|---|---|---|---|
| **A. Microsoft Store（推奨）** | 個人 $19（買い切り） | 中 | Store が署名を肩代わり。`winget search -s msstore` でも入る。手順は `.github/skills/publish-extension/references/store-publishing.md` |
| B. winget-pkgs + 署名済み MSIX | Azure Trusted Signing 月額約 $10 | 中 | 自前 Release に .msix を置き `InstallerType: msix` で提出。CmdPal のブラウズ（`windows-commandpalette-extension` タグ）にも載せられる |
| C. サイドロード（開発者向け） | 無料 | 低 | 未署名 MSIX + 開発者モード必須。一般配布には不向き。README に手順を書く補助チャネル |

以下の手順書は経路が確定するまでの共通部分（public 化・タグ運用）と、旧 EXE 手順の記録。

## 現状の検証済み事項

| 項目 | 状態 |
|---|---|
| CLSID 一致（`RedmineExtension.cs` の `[Guid]` = `Package.appxmanifest` = `setup.iss`） | ✅ `f0824b2a-3b8d-4e2f-bfa7-26b3b7b8e61e` |
| LICENSE (MIT) / README / CONTRIBUTING / CLAUDE.md | ✅ |
| マニフェスト Publisher（`CN=dbsgg`）・バージョン 0.0.1.0 | ✅ |
| CI（push/PR で警告ゼロビルド） | ✅ `.github/workflows/build.yml` |
| リリース自動化（tag → インストーラー → Release → winget PR） | ✅ `.github/workflows/release.yml` |
| `.gitignore`（`Installer/`・`publish/` を除外） | ✅ |
| unpackaged 発行の設定切替（csproj の `WindowsPackageType==None` 条件で pubxml 無効化＋フレームワーク依存・トリミング/単一ファイル/R2R 無効） | ✅ |
| ローカルでのインストーラー生成（x64/arm64、winget 版 Inno Setup 自動検出） | ✅ `Installer\RedmineExtension_0.0.1_{x64,arm64}.exe` 生成確認済み |
| 秘密情報（API キー等）がリポジトリに無いこと | ✅ 資格情報マネージャのみ |

## 検証済み・結論が出た項目（追記）

1. ~~LocalServer32 登録で CmdPal が拡張を発見できるか~~ → **発見されない（確定）**。
   冒頭の訂正参照。EXE 経路は公開チャネルとしては廃止。
2. 残る実機確認: 署名済み（または dev モード＋未署名）**MSIX をインストールした場合の動作**
   （VS Deploy では動作済みのため、パッケージ経由でも動く見込みが高い）。
3. arm64 ビルド（ローカルに arm64 機が無ければ CI 産物をそのまま信頼しない）。

## 0. 一度だけの準備

```powershell
# ツール（Inno Setup はどの方法で入れてもよい。build-exe.ps1 が
# PATH / レジストリ / 既定の場所から ISCC.exe を自動検出する。
# 特殊な場所に置いた場合のみ -IsccPath か環境変数 ISCC_PATH で指定）
winget install JRSoftware.InnoSetup    # または choco install innosetup
winget install Microsoft.WingetCreate

# GitHub リポジトリ
#  - Settings → General → リポジトリを Public に変更
#  - Settings → Secrets and variables → Actions:
#      Secret   WINGET_PAT      … public_repo スコープの PAT（winget-pkgs へ PR するため）
#      Variable WINGET_ENABLED  … "true"（release.yml の winget-update ジョブ有効化。初回提出後に設定）
```

## 1. バージョンを上げる

- `RedmineExtension/Package.appxmanifest` の `Version="X.Y.Z.0"`（MSIX 開発用の整合のため）
- タグが正：リリース版数はタグ `vX.Y.Z` から取られ、`-p:Version` で埋め込まれる

## 2. ローカルでインストーラーを検証（旧 EXE 手順・記録として残置）

> ⚠️ この EXE は正常にインストール・COM 登録されるが、**CmdPal には発見されない**（冒頭参照）。

```powershell
.\build-exe.ps1 -Version X.Y.Z
# → Installer\RedmineExtension_X.Y.Z_x64.exe / _arm64.exe（ビルド自体は x64/arm64 とも検証済み）
```

## 3. リリース（タグ push で自動）

```powershell
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

release.yml が x64/arm64 のインストーラーをビルドし GitHub Release に添付する。

## 4. winget への初回提出（手動）※経路 B（署名済み MSIX）を選んだ場合のみ。URL は .msix に読み替える

```powershell
wingetcreate new `
  "https://github.com/dbsgg/RedmineExtension/releases/download/vX.Y.Z/RedmineExtension_X.Y.Z_x64.exe" `
  "https://github.com/dbsgg/RedmineExtension/releases/download/vX.Y.Z/RedmineExtension_X.Y.Z_arm64.exe"
```

プロンプトへの回答例：PackageIdentifier `dbsgg.RedmineExtension` / Publisher `dbsgg` /
PackageName `Redmine for Command Palette` / License `MIT`。

**提出前にマニフェストを必ず編集**（`wingetcreate` は生成のみで入れてくれない）：

1. `*.locale.en-US.yaml`（全ロケール）に **CmdPal ブラウズ掲載用タグ**：
   ```yaml
   Tags:
   - windows-commandpalette-extension
   ```
2. `*.installer.yaml` に **Windows App SDK ランタイム依存**（`Directory.Packages.props` の
   `Microsoft.WindowsAppSDK` = 2.0.1 に対応する系列へ合わせる）：
   ```yaml
   Dependencies:
     PackageDependencies:
     - PackageIdentifier: Microsoft.WindowsAppRuntime.2.0
   ```
3. `winget validate --manifest <dir>` を通してから `--submit`（または手動 PR）。

## 5. 2 回目以降

`WINGET_ENABLED=true` を設定してあれば、タグ push だけで release.yml の
`winget-update` ジョブが `wingetcreate update dbsgg.RedmineExtension` の PR を自動送付する。

## トラブルシューティング

| 症状 | 対処 |
|---|---|
| CmdPal のブラウズに出ない | locale YAML の `windows-commandpalette-extension` タグ漏れ |
| インストール後に拡張が出ない | LocalServer32 のパス/引数、CLSID の一致、CmdPal 再起動を確認 |
| 起動時に WinRT 型ロード失敗 | WindowsAppRuntime 依存の宣言漏れ（手順 4-2） |
| winget PR の検証落ち | `winget validate` をローカルで再実行し指摘を修正 |
