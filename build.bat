@echo off
dotnet restore FFVII_LAUNCHER.csproj
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" FFVII_LAUNCHER.csproj /p:Configuration=Release /p:Platform=x86
