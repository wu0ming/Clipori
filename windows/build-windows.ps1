$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Please install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0'
}
dotnet restore './Clipori.Windows/Clipori.Windows.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
dotnet publish './Clipori.Windows/Clipori.Windows.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o './publish/Clipori-Windows-win-x64'
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Write-Host "Build output: $((Resolve-Path './publish/Clipori-Windows-win-x64').Path)" -ForegroundColor Green
