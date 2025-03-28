<#
    .NOTES
        Author         : @ChrisStro
        GitHub         : https://github.com/ChrisStro
        Modfied:       : https://github.com/emadadel4
#>

function Get-FileFromWeb {
    param (
        # Parameter help description
        [Parameter(Mandatory)]
        [string]$URL,
  
        # Parameter help description
        [Parameter(Mandatory)]
        [string]$File 
    )
    Begin {
        function Show-Progress {
            param (
                # Enter total value
                [Parameter(Mandatory)]
                [Single]$TotalValue,
        
                # Enter current value
                [Parameter(Mandatory)]
                [Single]$CurrentValue,
        
                # Enter custom progresstext
                [Parameter(Mandatory)]
                [string]$ProgressText,
        
                # Enter value suffix
                [Parameter()]
                [string]$ValueSuffix,
        
                # Enter bar lengh suffix
                [Parameter()]
                [int]$BarSize = 20,

                # show complete bar
                [Parameter()]
                [switch]$Complete
            )
            
            # calc %
            $percent = $CurrentValue / $TotalValue
            $percentComplete = $percent * 100
            if ($ValueSuffix) {
                $ValueSuffix = " $ValueSuffix" # add space in front
            }
            if ($psISE) {
                Write-Progress "$ProgressText $CurrentValue$ValueSuffix of $TotalValue$ValueSuffix" -id 0 -percentComplete $percentComplete            
            }
            else {
                # build progressbar with string function
                $curBarSize = $BarSize * $percent
                $progbar = ""
                $progbar = $progbar.PadRight($curBarSize,[char]9608)
                $progbar = $progbar.PadRight($BarSize,[char]9617)
        
                if (!$Complete.IsPresent) {
                    Write-Host -NoNewLine "[+] Downloading $($percentComplete.ToString("##0"))%"
                }
                else {
                    Write-Host -NoNewLine "[+] Downloading $($percentComplete.ToString("##0"))%"
                }                
            }   
        }
    }
    Process {
        try {
            $storeEAP = $ErrorActionPreference
            $ErrorActionPreference = 'Stop'
        
            # invoke request
            $request = [System.Net.HttpWebRequest]::Create($URL)
            $response = $request.GetResponse()
  
            if ($response.StatusCode -eq 401 -or $response.StatusCode -eq 403 -or $response.StatusCode -eq 404) {
                throw "Remote file either doesn't exist, is unauthorized, or is forbidden for '$URL'."
            }
  
            if($File -match '^\.\\') {
                $File = Join-Path (Get-Location -PSProvider "FileSystem") ($File -Split '^\.')[1]
            }
            
            if($File -and !(Split-Path $File)) {
                $File = Join-Path (Get-Location -PSProvider "FileSystem") $File
            }

            if ($File) {
                $fileDirectory = $([System.IO.Path]::GetDirectoryName($File))
                if (!(Test-Path($fileDirectory))) {
                    [System.IO.Directory]::CreateDirectory($fileDirectory) | Out-Null
                }
            }

            [long]$fullSize = $response.ContentLength
            $fullSizeMB = $fullSize / 1024 / 1024
  
            # define buffer
            [byte[]]$buffer = new-object byte[] 1048576
            [long]$total = [long]$count = 0
  
            # create reader / writer
            $reader = $response.GetResponseStream()
            $writer = new-object System.IO.FileStream $File, "Create"
  
            # start download
            $finalBarCount = 0 #show final bar only one time
            do {
          
                $count = $reader.Read($buffer, 0, $buffer.Length)
          
                $writer.Write($buffer, 0, $count)
              
                $total += $count
                $totalMB = $total / 1024 / 1024
          
                if ($fullSize -gt 0) {
                    Show-Progress -TotalValue $fullSizeMB -CurrentValue $totalMB -ProgressText "Downloading $($File.Name)" -ValueSuffix "MB"
                }

                if ($total -eq $fullSize -and $count -eq 0 -and $finalBarCount -eq 0) {
                    Show-Progress -TotalValue $fullSizeMB -CurrentValue $totalMB -ProgressText "Downloading $($File.Name)" -ValueSuffix "MB" -Complete
                    $finalBarCount++
                    #Write-Host "$finalBarCount"
                }

            } while ($count -gt 0)
        }
  
        catch {
        
            $ExeptionMsg = $_.Exception.Message
            Write-Host "Download breaks with error : $ExeptionMsg"
        }
  
        finally {
            # cleanup
            if ($reader) { $reader.Close() }
            if ($writer) { $writer.Flush(); $writer.Close() }
        
            $ErrorActionPreference = $storeEAP
            [GC]::Collect()
        }    
    }
}

function Install-ITTPackage {
    param (
        [string]$packageName,
        [string]$fileType,
        [array]$dependencies,
        [string]$url,
        [string]$url64,
        [string]$installerName,
        [string]$installerName64,
        [string]$silentArgs,
        [array]$validExitCodes
    )

    $toolsDir = "C:\ProgramData\itt\downloads\$packageName\"
    $installerPath = "$toolsDir\$packageName.$fileType"

    if (-Not (Test-Path $installerPath)) {
        
        $downloadUrl = if ([Environment]::Is64BitOperatingSystem) { $url64 } else { $url }
        $launcherName = if ([Environment]::Is64BitOperatingSystem) { $installerName64 } else { $installerName }

        # Create directory if it does not exist
        if (-Not (Test-Path $toolsDir)) {
            New-Item -ItemType Directory -Path $toolsDir | Out-Null
        }
        # Install dependencies first
        if ($dependencies -and $dependencies.Count -gt 0) {
            foreach ($depUrl in $dependencies) {
                Start-Process -FilePath "itt" -ArgumentList "install $depUrl -y" -NoNewWindow -Wait -PassThru
            }
        }

        Get-FileFromWeb -URL $downloadUrl -File $installerPath

        switch ($fileType) {
            "msi" {
                try {
                    Start-Process -FilePath "msiexec.exe" -ArgumentList "/i $installerPath /quiet $silentArgs" -Wait -NoNewWindow
                    Write-Host "[+] Installing $packageName..." -NoNewline
                }
                catch {
                    Write-Error $_
                }
            }
            "exe"{
                try {
                    Start-Process -FilePath $installerPath -ArgumentList $silentArgs -Wait
                    Write-Host "[+] Installing $packageName..."
                }
                catch {
                    Write-Error $_
                }
            }
            "zip"{

                Write-Host "[+] Expanding Archive..." -ForegroundColor Yellow
                Expand-Archive -Path  $installerPath -DestinationPath $toolsDir -Force -ErrorAction Stop

                $desktopPath = [System.Environment]::GetFolderPath('Desktop')
                $shortcutPath = Join-Path -Path $desktopPath -ChildPath "$packageName.lnk"

                try {
                    # Create the shortcut
                    $shell = New-Object -ComObject WScript.Shell
                    $shortcut = $shell.CreateShortcut($shortcutPath)
                    $shortcut.TargetPath = "$toolsDir\$launcherName"
                    $shortcut.Save()
                    Write-Host "`r[+] Shortcut created on Destkop " -ForegroundColor Yellow -NoNewline
                }
                catch {
                    Write-Error "`r[x] Failed to create shortcut. Error: $_" -ForegroundColor Red -NoNewline
                }
            }
            "msixbundle"{
                try {
                    Add-AppxPackage -Path $installerPath
                    Write-Host "`r[+] Installing $packageName..."
                }
                catch {
                    Write-Error "`r[x] Failed to install $packageName. Error: $_"
                }
            }
            Default {
            }
        }
    }
}
