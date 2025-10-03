param(
    [Parameter(Mandatory=$true)]
    [string]$targetHostname,
    [Parameter(Mandatory=$true)]
    [string]$targetPath
)

$ErrorActionPreference="Stop"

$publishFolder=(Join-Path $PSScriptRoot '\bin\Release\net8.0\publish')

# dotnet publish seems to keep old files around, so let's clean them first
if (Test-Path $publishFolder)
{
    write-host "Cleaning $publishFolder ..."
    Remove-Item -Force -Recurse $publishFolder
}

dotnet publish

# dotnet publish seems to include development-only files, so let's delete those
Remove-Item (Join-Path $publishFolder appsettings*.json)

# Stop WalleeCharging on deployment target, copy files, and restart
Write-Host 'Stopping WalleeCharging...'
ssh $targetHostname sudo systemctl stop WalleeCharging

Write-Host 'Copying files...'
scp -r "$publishFolder\*" "$($targetHostname):$targetPath"
Write-Host 'Starting WalleeCharging'
ssh $targetHostname sudo systemctl start WalleeCharging