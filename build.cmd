@echo off
setlocal
cd /d "%~dp0"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /win32manifest:src\app.manifest /out:VideoToMP3.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\MainWindow.cs src\AudioExport.cs
exit /b %errorlevel%
