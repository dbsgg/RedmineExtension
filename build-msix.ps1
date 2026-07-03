<#
.SYNOPSIS
    Microsoft Store 提出用の MSIX バンドル (.msixbundle) を x64 / ARM64 でビルドする。
.DESCRIPTION
    各アーキテクチャの .msix を生成し、makeappx でバンドルにまとめる。生成物は
    リポジトリ直下 MsixPackages\ に出力する（署名なし。Partner Center 側で署名される）。

    Store の Identity（予約済みの Name / Publisher）は引数で注入する。省略時は
    Package.appxmanifest の値のまま（ローカル検証用）。リポジトリに予約値を焼き込まないため。
.PARAMETER Version
    パッケージバージョン x.y.z（内部で x.y.z.0 に整形）。Store 提出のたびに上げる。
.PARAMETER IdentityName
    Partner Center の Package/Identity/Name。指定時のみ上書き。
.PARAMETER Publisher
    Partner Center の Package/Identity/Publisher（例: "CN=1234ABCD-..."）。指定時のみ上書き。
.PARAMETER NoTrim
    トリミング/単一ファイルを無効化する。ILLink のタスクホスト生成に失敗する環境
    （scoop 版 .NET SDK 等）でのローカル検証用。既定はトリミング有効（CI/公式 SDK 向け、
    小さいパッケージ）。最終的な Store 用パッケージはトリミング有効で作ることを推奨。
.EXAMPLE
    # ローカル検証（scoop SDK 等）: 署名なしバンドルを生成
    .\build-msix.ps1 -Version 0.1.0 -NoTrim
.EXAMPLE
    # Store 提出用（予約済み Identity を注入）
    .\build-msix.ps1 -Version 0.1.0 -IdentityName 12345Publisher.RedmineForCommandPalette -Publisher "CN=ABCD1234-..."
#>

param(
    [string]$Version = "1.0.0",
    [string]$IdentityName,
    [string]$Publisher,
    [switch]$NoTrim
)

$ErrorActionPreference = "Stop"

$project = "RedmineExtension\RedmineExtension.csproj"
$manifest = "RedmineExtension\Package.appxmanifest"
$outDir = Join-Path $PSScriptRoot "MsixPackages"
$version4 = "$Version.0"

function Find-MakeAppx {
    $cmd = Get-Command "makeappx.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { $_ }
    foreach ($root in $roots) {
        $found = Get-ChildItem (Join-Path $root "Windows Kits\10\bin") -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    Write-Error "makeappx.exe が見つかりません。Windows SDK をインストールしてください。"
}

$makeappx = Find-MakeAppx
Write-Host "makeappx: $makeappx" -ForegroundColor DarkGray

# 単一プロジェクト MSIX はパッケージ版数を Package.appxmanifest の Identity/Version から
# 取るため（AppxPackageVersion は効かない）、ビルド中だけマニフェストを書き換えて復元する。
$manifestBackup = Get-Content $manifest -Raw
[xml]$xml = $manifestBackup
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("d", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$xml.SelectSingleNode("//d:Identity", $ns).Version = $version4
if ($IdentityName) { $xml.SelectSingleNode("//d:Identity", $ns).Name = $IdentityName }
if ($Publisher) { $xml.SelectSingleNode("//d:Identity", $ns).Publisher = $Publisher }
$xml.Save((Resolve-Path $manifest))

# Store 提出用の共通プロパティ。
$common = @(
    "-c", "Release",
    "-p:GenerateAppxPackageOnBuild=true",
    "-p:AppxBundle=Never",
    "-p:UapAppxPackageBuildMode=SideloadOnly",
    "-p:AppxPackageSigningEnabled=false"
)
if ($NoTrim) {
    $common += @("-p:PublishTrimmed=false", "-p:PublishSingleFile=false")
}

Remove-Item -Recurse -Force (Join-Path (Split-Path $project) "AppPackages") -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $outDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $outDir | Out-Null

try {
    $msixPaths = @()
    foreach ($arch in @("x64", "ARM64")) {
        Write-Host "`n=== $arch ===" -ForegroundColor Cyan
        $pkgDir = "AppPackages\$arch\"
        dotnet build $project -p:Platform=$arch -p:AppxPackageDir=$pkgDir @common
        if ($LASTEXITCODE -ne 0) {
            Write-Error "MSIX build failed for $arch"
        }

        $msix = Get-ChildItem (Join-Path (Split-Path $project) "AppPackages\$arch") -Recurse -Filter *.msix |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $msix) {
            Write-Error "$arch の .msix が見つかりません。"
        }

        $dest = Join-Path $outDir $msix.Name
        Copy-Item $msix.FullName $dest -Force
        $msixPaths += $dest
        Write-Host "  -> $($msix.Name)"
    }
}
finally {
    # マニフェストを必ず元に戻す（Identity/Version の変更をコミットさせない）。
    Set-Content -Path $manifest -Value $manifestBackup -NoNewline -Encoding utf8
}

# バンドルのマッピングファイルを生成し、makeappx でまとめる。
$mapping = Join-Path $outDir "bundle_mapping.txt"
$lines = @("[Files]")
foreach ($p in $msixPaths) {
    $name = Split-Path $p -Leaf
    $lines += "`"$p`" `"$name`""
}
Set-Content -Path $mapping -Value $lines -Encoding utf8

$bundle = Join-Path $outDir "RedmineExtension_$($version4)_Bundle.msixbundle"
& $makeappx bundle /f $mapping /p $bundle /o
if ($LASTEXITCODE -ne 0) {
    Write-Error "makeappx bundle failed"
}

Write-Host "`nStore 提出用バンドル:" -ForegroundColor Cyan
Write-Host "  $bundle"
Write-Host "  （署名なし。Partner Center にそのままアップロードできます）"
