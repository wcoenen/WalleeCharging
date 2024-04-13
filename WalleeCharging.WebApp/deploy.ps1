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

# prompt for target
$targetHostname = (Read-Host 'Enter user@hostname target')
$targetPath = (Read-Host 'Enter target folder')

# Stop WalleeCharging on deployment target, copy files, and restart
Write-Host 'Stopping WalleeCharging...'
ssh $targetHostname sudo systemctl stop WalleeCharging

Write-Host 'Copying files...'
scp -r "$publishFolder\*" "$($targetHostname):$targetPath"
Write-Host 'Starting WalleeCharging'
ssh wim@wallee sudo systemctl start WalleeCharging