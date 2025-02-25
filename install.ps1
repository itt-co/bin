$ittPath = "C:\ProgramData\itt"
$installerPath = "$env:TEMP\installer.msi"

if (-not (Test-Path $ittPath)) {
    New-Item -ItemType Directory -Path $ittPath -Force | Out-Null
}

Invoke-WebRequest "https://github.com/itt-co/bin/releases/latest/download/installer.msi" -OutFile $installerPath

Start-Process msiexec.exe -ArgumentList "/i `"$installerPath`" /q" -NoNewWindow -Wait

# Get the current machine-level Path
$envPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine)

# Check if the path already exists
if (-not $envPath.Split(';').Contains($ittPath)) {
    # Add to machine-level Path
    $newPath = "$envPath;$ittPath"
    [Environment]::SetEnvironmentVariable("Path", $newPath, [EnvironmentVariableTarget]::Machine)
    #Write-Output "Path added to machine-level Path."

    # Also add to current session's Path so it's immediately available
    if (-not $env:Path.Split(';').Contains($ittPath)) {
        $env:Path += ";$ittPath"
        #Write-Output "Path added to current session Path."
    }
} else {
    #Write-Output "Path already exists in machine-level Path."
}
