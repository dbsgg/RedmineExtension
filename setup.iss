; Inno Setup script — winget 配布用の EXE インストーラー（unpackaged 配布）
; ビルドは build-exe.ps1 から ISCC.exe で実行する（/DMyAppVersion, /DArchitecturesAllowed を渡す）。
;
; 注意: この拡張はアウトプロセス COM サーバー（WinExe）。InprocServer32(DLL) ではなく
; LocalServer32 に exe を登録する。CLSID は Package.appxmanifest / RedmineExtension.cs と一致必須。

#define MyAppName "RedmineExtension"
#define MyAppDisplayName "Redmine for Command Palette"
#define MyAppPublisher "dbsgg"
#define MyAppURL "https://github.com/dbsgg/RedmineExtension"
#define MyAppCLSID "{{f0824b2a-3b8d-4e2f-bfa7-26b3b7b8e61e}"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.1"
#endif
; ビルド対象アーキテクチャ（build-exe.ps1 から /DMyAppArch=x64|arm64 で渡される）
#ifndef MyAppArch
  #define MyAppArch "x64"
#endif

[Setup]
AppId={#MyAppCLSID}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
#if MyAppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputBaseFilename={#MyAppName}_{#MyAppVersion}_{#MyAppArch}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=Installer
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Files]
; デバッグシンボルは配布物に含めない。
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Command Palette が拡張を発見するための COM サーバー登録（アウトプロセス）。
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppDisplayName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppName}.exe"" -RegisterProcessAsComServer"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
