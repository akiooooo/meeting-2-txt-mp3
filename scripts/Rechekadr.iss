#define FFmpegBin GetEnv("FFMPEG_BIN")
[Setup]
AppId=Rechekadr.Desktop
AppName=Речекадр
AppVersion=1.0.2
DefaultDirName={localappdata}\Programs\Rechekadr
DefaultGroupName=Речекадр
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
AllowNoIcons=yes
Compression=lzma2/fast
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=Rechekadr-Setup-x64
UninstallDisplayIcon={app}\VideoToMP3.exe
[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
[Files]
Source: "..\VideoToMP3.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\models\ggml-small-q5_1.bin"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "..\models\ggml-silero-v6.2.0.bin"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "..\models\LICENSE-*.txt"; DestDir: "{app}\models"; Flags: ignoreversion
Source: "{#FFmpegBin}\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#FFmpegBin}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#FFmpegBin}\..\LICENSE"; DestDir: "{app}\licenses\FFmpeg"; Flags: ignoreversion
Source: "{#FFmpegBin}\..\README.txt"; DestDir: "{app}\licenses\FFmpeg"; Flags: ignoreversion
[Icons]
Name: "{group}\Речекадр"; Filename: "{app}\VideoToMP3.exe"
[Run]
Filename: "{app}\VideoToMP3.exe"; Flags: nowait skipifsilent
