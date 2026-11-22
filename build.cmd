@echo off
setlocal
cd /d "%~dp0"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /win32manifest:src\app.manifest /out:Meeting2Text.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\MainWindow.cs src\AudioExport.cs src\MeetingNotes.cs /reference:System.Web.Extensions.dll /reference:System.IO.Compression.FileSystem.dll
exit /b %errorlevel%
