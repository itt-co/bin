$ittPath = "C:\ProgramData\itt"
$installerPath = "$env:TEMP\installer.msi"

if (-not (Test-Path $ittPath)) {
    New-Item -ItemType Directory -Path $ittPath -Force | Out-Null
}

Invoke-WebRequest "https://github.com/itt-co/bin/releases/latest/download/installer.msi" -OutFile $installerPath

Start-Process msiexec.exe -ArgumentList "/i `"$installerPath`" /q" -NoNewWindow -Wait

$envPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::Machine)
if (-not $envPath.Split(';').Contains($ittPath)) {
    $newPath = "$envPath;$ittPath"
    [Environment]::SetEnvironmentVariable("Path", $newPath, [EnvironmentVariableTarget]::Machine)
    Write-Output "Path added successfully."
} else {
    Write-Output "Path already exists."
}
