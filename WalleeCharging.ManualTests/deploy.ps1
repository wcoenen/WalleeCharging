$ErrorActionPreference="Stop"
$publishFolder=(Join-Path $PSScriptRoot '\bin\Release\net8.0\publish')

# dotnet publish seems to keep old files around, so let's clean them first
if (Test-Path $publishFolder)
{
    write-host "Cleaning $publishFolder ..."
    Remove-Item -Force -Recurse $publishFolder
}

dotnet publish

Write-Host 'Copying files'
scp -r "$publishFolder\*" wim@wallee.local:/home/wim/WalleeManualTests