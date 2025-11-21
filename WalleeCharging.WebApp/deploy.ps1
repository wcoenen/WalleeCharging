# Deploy WalleeCharging.WebApp to a remote Linux system via SSH/SCP.
$ErrorActionPreference="Stop"

$deployConfig = "$PSScriptRoot\.deploy.json"
if (Test-Path $deployConfig)
{
    $config = Get-Content $deployConfig | ConvertFrom-Json
    $targetHostname = $config.targetHostname
    $targetPath = $config.targetPath
}
else {
    # prompt for target if no config file is found
    $targetHostname = (Read-Host 'Enter user@hostname target')
    $targetPath = (Read-Host 'Enter target folder')

    # offer to save config for next time
    $saveConfig = (Read-Host 'Save target info to deploy.json for next time? (y/n)')
    if ($saveConfig -eq 'y')
    {
        $config = [PSCustomObject]@{
            targetHostname = $targetHostname
            targetPath = $targetPath
        }
        $config | ConvertTo-Json | Set-Content $deployConfig
        Write-Host "Saved config to $deployConfig"
    }
}

$publishFolder=(Join-Path $PSScriptRoot '\bin\Release\net10.0\publish')

# dotnet publish seems to keep old files around, so let's clean them first
if (Test-Path $publishFolder)
{
    write-host "Cleaning $publishFolder ..."
    Remove-Item -Force -Recurse $publishFolder
}

dotnet publish

# Deployment should not change the configuration on the target system
Remove-Item (Join-Path $publishFolder appsettings*.json)

# Stop WalleeCharging on deployment target, copy files, and restart
Write-Host 'Stopping WalleeCharging...'
ssh $targetHostname sudo systemctl stop WalleeCharging

Write-Host 'Copying files...'
scp -r "$publishFolder\*" "$($targetHostname):$targetPath"
Write-Host 'Starting WalleeCharging'
ssh $targetHostname sudo systemctl start WalleeCharging