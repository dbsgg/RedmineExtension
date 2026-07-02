<#
.SYNOPSIS
    winget 配布用の EXE インストーラーをビルドする。
.DESCRIPTION
    dotnet publish（unpackaged: WindowsPackageType=None をコマンドラインで指定するため
    .csproj は MSIX 開発フローのまま）→ Inno Setup で Installer\*.exe を生成する。
    ISCC.exe は 引数 → 環境変数 ISCC_PATH → PATH → レジストリ → 既定の場所 の順で
    自動検出するため、winget / choco / 手動インストールのどれでも動く。
.PARAMETER Version
    埋め込むバージョン（例: 0.1.0）。タグと一致させる。
.PARAMETER Architectures
    ビルドするアーキテクチャ。既定は x64 と arm64。CI では 1 つずつ渡す。
.PARAMETER IsccPath
    ISCC.exe のフルパスを明示する場合に指定（自動検出より優先）。
.EXAMPLE
    .\build-exe.ps1 -Version 0.1.0
.EXAMPLE
    .\build-exe.ps1 -Version 0.1.0 -Architectures x64 -IsccPath "D:\tools\InnoSetup\ISCC.exe"
#>

param(
    [string]$Configuration = "Release",
    [string]$Version = "0.0.1",
    [string[]]$Architectures = @("x64", "arm64"),
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"

function Find-Iscc {
    param([string]$Explicit)

    # 1) 明示指定（引数 > 環境変数）。指定されているのに無い場合は誤設定なので即失敗させる。
    foreach ($candidate in @($Explicit, $env:ISCC_PATH)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            if (Test-Path $candidate) {
                return (Resolve-Path $candidate).Path
            }

            Write-Error "指定された ISCC.exe が見つかりません: $candidate"
        }
    }

    # 2) PATH（choco の shim、手動で PATH を通した場合、winget の一部構成など）。
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    # 3) アンインストール情報のレジストリ（Inno Setup のインストーラーが登録する。
    #    winget / choco / 手動のいずれ経由でも書かれるため、これが最も確実）。
    $regKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1"
    )
    foreach ($key in $regKeys) {
        $location = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).InstallLocation
        if ($location) {
            $exe = Join-Path $location "ISCC.exe"
            if (Test-Path $exe) {
                return $exe
            }
        }
    }

    # 4) 既定のインストール先（マシン単位 / ユーザー単位 = winget --scope user の既定）。
    $roots = @(
        ${env:ProgramFiles(x86)},
        $env:ProgramFiles,
        (Join-Path $env:LOCALAPPDATA "Programs")
    ) | Where-Object { $_ }
    foreach ($root in $roots) {
        $exe = Join-Path $root "Inno Setup 6\ISCC.exe"
        if (Test-Path $exe) {
            return $exe
        }
    }

    Write-Error (@(
        "ISCC.exe (Inno Setup 6) が見つかりません。次のいずれかでインストールしてください:",
        "  winget install JRSoftware.InnoSetup",
        "  choco install innosetup",
        "または -IsccPath / 環境変数 ISCC_PATH でフルパスを指定してください。"
    ) -join [Environment]::NewLine)
}

$iscc = Find-Iscc -Explicit $IsccPath
Write-Host "ISCC: $iscc" -ForegroundColor DarkGray

$project = "RedmineExtension\RedmineExtension.csproj"

foreach ($arch in $Architectures) {
    Write-Host "`n=== $arch ===" -ForegroundColor Cyan

    Remove-Item -Recurse -Force "publish" -ErrorAction SilentlyContinue

    # WindowsPackageType=None を渡すと csproj 側の条件で unpackaged 用の設定に切り替わる
    # （MSIX 用 pubxml の無効化・フレームワーク依存・トリミング/単一ファイル無効）。
    dotnet publish $project -c $Configuration -r "win-$arch" -o "publish" `
        -p:WindowsPackageType=None `
        -p:Platform=$arch `
        -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed for $arch"
    }

    & $iscc /DMyAppVersion="$Version" /DMyAppArch="$arch" setup.iss
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup failed for $arch"
    }
}

Remove-Item -Recurse -Force "publish" -ErrorAction SilentlyContinue

Write-Host "`nInstaller ディレクトリの出力:" -ForegroundColor Cyan
Get-ChildItem -Path "Installer" -Filter "*.exe" | ForEach-Object { Write-Host "  $($_.Name)" }
