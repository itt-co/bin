$ittPath = "C:\ProgramData\itt"
$installerPath = "$env:TEMP\installer.msi"

if (-not (Test-Path $ittPath)) {
    New-Item -ItemType Directory -Path $ittPath -Force | Out-Null
}

Invoke-WebRequest "https://github.com/itt-co/bin/releases/latest/download/installer.msi" -OutFile $installerPath

Start-Process msiexec.exe -ArgumentList "/i `"$installerPath`" /q" -NoNewWindow -Wait

$currentPath = [Environment]::GetEnvironmentVariable('Path', 'Machine')

if ($currentPath -notlike "*${ittPath}*") 
{
    [Environment]::SetEnvironmentVariable('Path', "$currentPath;$ittPath", 'Machine')
}
