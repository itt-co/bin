$ittPath = "C:\ProgramData\itt"

# Create directory if it doesn't exist
if (-not (Test-Path $ittPath)) {
    New-Item -ItemType Directory -Path $ittPath -Force | Out-Null
}

# Download the installer
$installerPath = "$env:TEMP\installer.msi"
Invoke-WebRequest -Uri "https://github.com/itt-co/bin/releases/latest/download/installer.msi" -OutFile $installerPath

# Check if the installer was downloaded
if (-not (Test-Path $installerPath)) {
    Write-Host "Failed to download the installer. Please try again." -ForegroundColor Red
    exit
}

# Install the MSI with logging to catch errors
$logPath = "$env:TEMP\itt-install.log"
Start-Process msiexec.exe -ArgumentList "/i `"$installerPath`" /qn /L*V `"$logPath`" REINSTALL=ALL REINSTALLMODE=vomus" -NoNewWindow -Wait

# Check the log if the installer failed
if (-not (Test-Path "$ittPath\itt.exe")) {
    Write-Host "Installation failed. Check the log for details: $logPath" -ForegroundColor Red
    notepad $logPath
    exit
}

# Add to PATH if not already added
$currentPath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
if ($currentPath -notlike "*$ittPath*") {
    [Environment]::SetEnvironmentVariable('Path', "$currentPath;$ittPath", 'Machine')
    Write-Host "Path updated successfully." -ForegroundColor Green
}

Write-Host "Installation completed successfully!" -ForegroundColor Green
