# Build-Local.ps1 - Local channel-aware MSIX packaging for the Files app.
#
# Source stays in the upstream dev-default state; channel mutations are applied
# temporarily and reverted via git restore when the build finishes.
#
# Usage:
#   .\Build-Local.ps1 -Channel Dev              # test package (FilesDev, blue icon, side-by-side)
#   .\Build-Local.ps1 -Channel Stable           # official-style package (Files, orange icon)
#   .\Build-Local.ps1 -Channel Dev -Install     # build and install, replacing same-version package
#
# Requires: a clean git tree under src/, the signing certificate under gen\.

param(
    [ValidateSet('Dev', 'Stable')]
    [string]$Channel = 'Dev',

    [switch]$Install,

    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',

    [string]$CertificateKeyFile = (Join-Path $PSScriptRoot 'gen\FilesDev_Test.pfx'),
    [string]$CertificatePassword = 'FilesDevTest2026',

    [string]$VsDevShellPath = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\Launch-VsDevShell.ps1'
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'src\Files.App\Package.appxmanifest'

# 1. Safety contract: the mutation/restore cycle relies on a clean src tree.
$dirty = git status --porcelain -- src
if ($dirty) {
    throw "src/ is not clean; commit or stash first.`n$dirty"
}

try {
    # 2. Apply channel mutations (mirrors .github/scripts/Configure-AppxManifest.ps1 semantics).
    if ($Channel -eq 'Stable') {
        # Official sideload flow, straight from CI.
        & (Join-Path $PSScriptRoot '.github\scripts\Configure-AppxManifest.ps1') `
            -Branch SideloadStable `
            -PackageManifestPath $manifestPath `
            -Publisher 'CN=Files' `
            -WorkingDir $PSScriptRoot `
            -SecretBingMapsKey '' -SecretSentry '' -SecretGitHubOAuthClientId '' -SecretLanguageModelLafToken ''
    }
    else {
        # Dev channel: keep dev-default branding, only clear secret placeholders.
        Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -Filter *.cs | ForEach-Object {
            $text = Get-Content $_.FullName -Raw
            if ($text -match '\.secret') {
                $text = $text `
                    -replace 'bingmapskey\.secret', '' `
                    -replace 'sentry\.secret', '' `
                    -replace 'githubclientid\.secret', '' `
                    -replace 'languagemodellaftoken\.secret', ''
                Set-Content $_.FullName $text -NoNewline
            }
        }
    }

    # 3. Build.
    if (Test-Path $VsDevShellPath) {
        . $VsDevShellPath -Arch $Platform -HostArch x64 -SkipAutomaticLocation
    }
    msbuild -restore (Join-Path $PSScriptRoot 'src\Files.App\Files.App.csproj') `
        "-p:Configuration=$Configuration" "-p:Platform=$Platform" `
        -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never `
        "-p:PackageCertificateKeyFile=$CertificateKeyFile" `
        "-p:PackageCertificatePassword=$CertificatePassword" `
        -v:quiet -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) {
        throw "msbuild failed with exit code $LASTEXITCODE"
    }

    # 4. Locate the produced package.
    $msix = Get-ChildItem (Join-Path $PSScriptRoot 'src\Files.App\AppPackages') -Recurse -Filter *.msix |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $msix) {
        throw 'No .msix found under src\Files.App\AppPackages.'
    }
    Write-Host "`nPackage: $($msix.FullName)" -ForegroundColor Green

    # 5. Optionally install, replacing an existing same-version package.
    if ($Install) {
        [xml]$manifest = Get-Content $manifestPath
        $identityName = $manifest.Package.Identity.Name
        $version = $manifest.Package.Identity.Version

        Get-AppxPackage -Name $identityName |
            Where-Object { $_.Publisher -eq 'CN=Files' -and $_.Version -eq $version } |
            ForEach-Object {
                Write-Host "Removing existing package $($_.PackageFullName)"
                Remove-AppxPackage -Package $_.PackageFullName
            }

        Add-AppxPackage -Path $msix.FullName
        Write-Host "Installed: $identityName $version" -ForegroundColor Green
    }
}
finally {
    # 6. Always return the source tree to the committed dev-default state.
    git restore -- src
    if (git status --porcelain -- src) {
        Write-Warning 'src/ still dirty after restore; check manually.'
    }
}
