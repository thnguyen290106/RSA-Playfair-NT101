; Installer cho RSA & Playfair. Compile: ISCC.exe installer\setup.iss
; Yêu cầu: đã chạy `dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64`.

#define AppName "RSA & Playfair"
#define AppVersion "0.1.0"
#define AppExe "RSA-Playfair-NT101.exe"
#define RepoUrl "https://github.com/thnguyen290106/RSA-Playfair-NT101"
#define PayloadDir "..\publish\win-x64"

#if !FileExists(AddBackslash(SourcePath) + PayloadDir + "\" + AppExe)
  #error Chua co ban publish. Chay: dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64
#endif

[Setup]
; AppId cố định — Inno dùng nó để nhận ra bản cũ khi upgrade. Không bao giờ đổi.
AppId={{74446C7E-BE14-4833-8B86-8BD0644BFE05}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=thnguyen290106
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#AppVersion}

DefaultDirName={autopf}\RSA-Playfair
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; App không ghi gì cạnh exe nên cài vào Program Files là an toàn; vẫn cho chọn
; cài per-user vào %LocalAppData% nếu máy không có quyền admin.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
LicenseFile=
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\publish
OutputBaseFilename=RSA-Playfair-NT101-v0.1-setup

[Languages]
; Inno Setup 6 không kèm file ngôn ngữ tiếng Việt, nên wizard chạy tiếng Anh.
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Toàn bộ cây self-contained: exe + runtime .NET 10 + WPF. Không cần .pdb khi phát hành.
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
