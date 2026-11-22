#define FFmpegBin GetEnv("FFMPEG_BIN")
[Setup]
; Keep the installation identity so existing copies can be upgraded.
AppId=Rechekadr.Desktop
AppName=Meeting2Text
AppVersion=1.2.0
DefaultDirName={localappdata}\Programs\Meeting2Text
DefaultGroupName=Meeting2Text
UsePreviousGroup=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
AllowNoIcons=yes
Compression=lzma2/ultra64
LZMAUseSeparateProcess=yes
LZMADictionarySize=131072
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=Meeting2Text-Setup-x64
UninstallDisplayIcon={app}\Meeting2Text.exe
[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
[Files]
Source: "..\Meeting2Text.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\models\ggml-small-q5_1.bin"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "..\models\ggml-silero-v6.2.0.bin"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "..\models\LICENSE-*.txt"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "{#FFmpegBin}\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#FFmpegBin}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#FFmpegBin}\..\LICENSE"; DestDir: "{app}\licenses\FFmpeg"; Flags: ignoreversion
Source: "{#FFmpegBin}\..\README.txt"; DestDir: "{app}\licenses\FFmpeg"; Flags: ignoreversion
[InstallDelete]
Type: files; Name: "{userprograms}\Речекадр\Речекадр.lnk"
Type: dirifempty; Name: "{userprograms}\Речекадр"
Type: files; Name: "{app}\VideoToMP3.exe"
[Icons]
Name: "{group}\Meeting2Text"; Filename: "{app}\Meeting2Text.exe"
[Run]
Filename: "{app}\Meeting2Text.exe"; Flags: nowait skipifsilent
