@echo off
REM Builds LittleKnight.exe using the C# compiler built into Windows (.NET Framework 4).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:LittleKnight.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LittleKnight.cs
if errorlevel 1 (echo Build failed & pause) else (echo Built LittleKnight.exe)
