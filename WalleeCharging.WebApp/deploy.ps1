$ErrorActionPreference="Stop"
dotnet publish
& "C:\Program Files\PuTTY\pscp.exe" -r "$PSScriptRoot\bin\Release\net8.0\publish\" wim@wallee.local:/home/wim/WalleeCharging/