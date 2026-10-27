param([string]$OutputDirectory = 'artifacts/TeleGramBlog-1.7.2-win-x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet publish src/TeleGramBlog.App/TeleGramBlog.App.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory
    if($LASTEXITCODE -ne 0){throw 'Publish failed'}
    Copy-Item -LiteralPath README.md,telegram.example.json,THIRD-PARTY-NOTICES.md -Destination $OutputDirectory
    Write-Host "Open $OutputDirectory/TeleGramBlog.App.exe. Keep the whole folder together."
} finally {Pop-Location}
