#!/bin/sh
# Build ForYusef.exe from WSL using Windows' built-in C# compiler.
cd "$(dirname "$0")"
/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /optimize+ \
  /out:ForYusef.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /recurse:'windows\*.cs'
