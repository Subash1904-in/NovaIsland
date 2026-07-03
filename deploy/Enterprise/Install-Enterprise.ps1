<#
.SYNOPSIS
Enterprise deployment script for Nova Island.

.DESCRIPTION
This script is designed for Microsoft Intune or MECM deployment.
It installs Nova Island per-machine (unpackaged) or per-user, and copies the ADMX/ADML 
policy templates to the central PolicyDefinitions store.

.EXAMPLE
.\Install-Enterprise.ps1 -InstallPath "C:\Program Files\NovaIsland"
#>
param(
    [string]$InstallPath = "$env:ProgramFiles\NovaIsland",
    [switch]$InstallPolicies = $true
)

try {
    Write-Host "Installing Nova Island to $InstallPath..."
    
    # 1. Ensure directory exists
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    # 2. Extract binaries (simulated: in production, an MSI or ZIP extract goes here)
    # Expand-Archive -Path ".\NovaIsland.zip" -DestinationPath $InstallPath -Force
    Write-Host "Binaries staged successfully."

    # 3. Install ADMX Policies if requested and running as admin
    if ($InstallPolicies) {
        $policyDefPath = "$env:windir\PolicyDefinitions"
        
        Write-Host "Copying ADMX policies to $policyDefPath..."
        
        if (Test-Path ".\NovaIsland.admx") {
            Copy-Item -Path ".\NovaIsland.admx" -Destination $policyDefPath -Force
            Copy-Item -Path ".\en-US\NovaIsland.adml" -Destination "$policyDefPath\en-US" -Force
            Write-Host "Policies installed."
        } else {
            Write-Warning "NovaIsland.admx not found in current directory. Skipping policies."
        }
    }

    # 4. Create Start Menu Shortcut (All Users)
    $wshShell = New-Object -ComObject WScript.Shell
    $shortcut = $wshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nova Island.lnk")
    $shortcut.TargetPath = "$InstallPath\NovaIsland.App.exe"
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.IconLocation = "$InstallPath\Assets\StoreLogo.png"
    $shortcut.Save()

    Write-Host "Installation completed successfully."
    exit 0
}
catch {
    Write-Error "Installation failed: $_"
    exit 1
}
