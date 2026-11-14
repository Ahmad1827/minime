#!/bin/sh
# Build from WSL using Windows' built-in C# compiler.
cd "$(dirname "$0")"
/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /optimize+ \
  /out:LittleKnight.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll LittleKnight.cs
