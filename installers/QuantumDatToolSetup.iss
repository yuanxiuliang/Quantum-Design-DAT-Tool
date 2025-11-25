#define MyAppName "Quantum Design DAT Tool"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Yuan Xiuliang"
#define MyAppURL "https://github.com/yuanxiuliang/Quantum-Design-DAT-Data-Visualization-Tool"
#define PublishDir "..\DatTool.UI\bin\Release\net8.0-windows10.0.19041\win-x64\publish"
#define AppIcon "..\DatTool.UI\Assets\QuantumDatTool.ico"

[Setup]
AppId={{B9ED5A3E-1F97-4F2B-9F64-1DDD7DD67A63}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=no
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\releases\latest
OutputBaseFilename=QuantumDatToolSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ChangesAssociations=yes
SetupIconFile={#AppIcon}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\DatTool.UI.exe"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\DatTool.UI.exe"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: ".dat"; ValueType: string; ValueName: ""; ValueData: "QuantumDatTool.Dat"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".dat\OpenWithProgids"; ValueType: string; ValueName: "QuantumDatTool.Dat"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: "QuantumDatTool.Dat"; ValueType: string; ValueName: ""; ValueData: "Quantum Design DAT File"; Flags: uninsdeletekey
Root: HKCR; Subkey: "QuantumDatTool.Dat\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\DatTool.UI.exe,0"
Root: HKCR; Subkey: "QuantumDatTool.Dat\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\DatTool.UI.exe"" ""%1"""

[Run]
Filename: "{app}\DatTool.UI.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDesktopKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DotNetSharedHostKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost';
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime';

var
  ExecResult: Integer;

function GetMajorVersion(const Version: string): Integer;
var
  DelimPos: Integer;
  MajorPart: string;
begin
  DelimPos := Pos('.', Version);
  if DelimPos > 0 then
    MajorPart := Copy(Version, 1, DelimPos - 1)
  else
    MajorPart := Version;
  Result := StrToIntDef(MajorPart, 0);
end;

function HasDesktopRuntime: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, DotNetDesktopKey, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if GetMajorVersion(Names[I]) >= 8 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function HasSharedHost: Boolean;
var
  Version: string;
begin
  Result := RegQueryStringValue(HKLM64, DotNetSharedHostKey, 'Version', Version);
  if Result then
    Result := GetMajorVersion(Version) >= 8;
end;

function IsDotNet8Installed: Boolean;
begin
  Result := HasDesktopRuntime or HasSharedHost;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet8Installed then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime is required but not found.'#13#13 +
      'Click OK to open the download page in your browser. Install the runtime, then run this setup again.',
      mbError, MB_OKCANCEL) = IDOK then
    begin
      ShellExec('', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ExecResult);
    end;
    Result := False;
  end;
end;

