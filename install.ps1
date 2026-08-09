<#
.SYNOPSIS
    Installer for the abcversion native binary on Windows.

.DESCRIPTION
    irm https://raw.githubusercontent.com/deneblab/abcversion/production/install.ps1 | iex

    Environment:
      ABCVERSION_VERSION      version to install, e.g. 1.2.15 (default: latest release)
      ABCVERSION_INSTALL_DIR  where to put the binary (default: $env:LOCALAPPDATA\Programs\abcversion)
      ABCVERSION_BASE_URL     release base URL (default: GitHub releases; override for mirrors/tests)
#>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

function Install-AbcVersion {
    $repoUrl = 'https://github.com/deneblab/abcversion'

    # Only win-x64 is published. Check this first, so an unsupported machine gets this
    # message rather than an incidental failure from the path setup below.
    $arch = $env:PROCESSOR_ARCHITECTURE
    if ($arch -ne 'AMD64') {
        throw "Unsupported architecture '$arch'. Only win-x64 is published. " +
              "Use 'dotnet tool install --global Deneblab.AbcVersionCmd' instead."
    }

    $asset   = 'abcversion-win-x64.exe'
    $version = $env:ABCVERSION_VERSION
    $baseUrl = if ($env:ABCVERSION_BASE_URL) { $env:ABCVERSION_BASE_URL } else { "$repoUrl/releases" }

    if ($env:ABCVERSION_INSTALL_DIR) {
        $installDir = $env:ABCVERSION_INSTALL_DIR
    }
    elseif ($env:LOCALAPPDATA) {
        $installDir = Join-Path $env:LOCALAPPDATA 'Programs\abcversion'
    }
    else {
        throw 'LOCALAPPDATA is not set; specify a target with ABCVERSION_INSTALL_DIR.'
    }

    if ($version) {
        $version = $version -replace '^v', ''
        $url   = "$baseUrl/download/v$version/$asset"
        $label = "v$version"
    }
    else {
        $url   = "$baseUrl/latest/download/$asset"
        $label = 'the latest release'
    }

    Write-Host "Installing abcversion (win-x64) from $label"

    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null

    try {
        $binPath = Join-Path $tmp $asset
        $sumPath = "$binPath.sha256"

        try {
            Invoke-WebRequest -Uri $url -OutFile $binPath -UseBasicParsing
        }
        catch {
            throw "Download failed: $url`nIf you pinned a version, check it exists at $repoUrl/releases"
        }

        try {
            Invoke-WebRequest -Uri "$url.sha256" -OutFile $sumPath -UseBasicParsing
        }
        catch {
            throw "No checksum published for this release ($url.sha256). " +
                  "Releases from before checksums were introduced cannot be verified."
        }

        # The .sha256 is written by sha256sum/shasum as "<digest>  <filename>".
        $expected = ((Get-Content $sumPath -Raw).Trim() -split '\s+')[0]
        $actual   = (Get-FileHash -Path $binPath -Algorithm SHA256).Hash

        if ($actual -ine $expected) {
            throw "Checksum mismatch - the download is corrupt or has been tampered with. Nothing was installed."
        }

        # Copy into place only after verification, so a failure never leaves a half-installed binary.
        New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        $target = Join-Path $installDir 'abcversion.exe'
        Copy-Item -Path $binPath -Destination $target -Force

        Write-Host "Installed abcversion to $target"

        # Persist to the *user* PATH; never touch the machine-wide one, which needs admin.
        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        if ($userPath -notlike "*$installDir*") {
            $newPath = if ([string]::IsNullOrEmpty($userPath)) { $installDir } else { "$userPath;$installDir" }
            [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
            Write-Host ''
            Write-Host "Added $installDir to your user PATH. Open a new terminal for it to take effect."
        }
    }
    finally {
        Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Install-AbcVersion
