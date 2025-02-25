$ittPath = "C:\ProgramData\itt"

if (-not (Test-Path $ittPath)) {
    New-Item -ItemType Directory -Path $ittPath -Force | Out-Null
}

$installerPath = "$env:TEMP\installer.msi"

Invoke-WebRequest "https://github.com/itt-co/bin/releases/latest/download/installer.msi" -OutFile $installerPath

if (Test-Path $installerPath) {
    Start-Process msiexec.exe -ArgumentList "/i `"$installerPath`" /qn REINSTALL=ALL REINSTALLMODE=vomus" -NoNewWindow -Wait

    $currentPath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    if ($currentPath -notlike "*${ittPath}*") {
        [Environment]::SetEnvironmentVariable('Path', "$currentPath;$ittPath", 'Machine')
    }
} else {
    Write-Host "Failed to download the installer Please try again."
}
