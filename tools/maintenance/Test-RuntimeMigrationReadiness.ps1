<#
.SYNOPSIS
Read-only inventory of prerequisites for Redux's proposed .NET 10 Windows migration.
.DESCRIPTION
Does not install tools, restore packages, run MSBuild, edit projects, or validate a release.
By default, missing prerequisites are reported without failing the inventory. Use
-RequireToolchain to return exit code 1 when the basic build prerequisites are absent.
A successful prerequisite check is not a successful migration or a portability check.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [switch]$AsJson,
    [switch]$RequireToolchain
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$isWindowsHost = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$checks = [Collections.Generic.List[object]]::new()

function Add-Check([string]$Id, [bool]$Detected, [string]$Detail) {
    $checks.Add([pscustomobject]@{ id = $Id; detected = $Detected; detail = $Detail })
}

function Invoke-InventoryCommand([string]$Command, [string[]]$Arguments) {
    # Commands are fixed read-only inventory operations; never invoke a repository build.
    $lines = @(& $Command @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Inventory command failed ($LASTEXITCODE): $Command $($Arguments -join ' ')"
    }
    return $lines
}

$globalJsonPath = Join-Path $repositoryRoot 'global.json'
if (!(Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
    throw 'RepositoryRoot must contain Redux global.json.'
}
$sdkPolicy = (Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk
Add-Check 'windows-host' $isWindowsHost 'The current WPF/C++/CLI application requires a Windows build host.'

$installedSdks = @()
$desktopRuntimes = @()
$dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($dotnet) {
    $installedSdks = @(Invoke-InventoryCommand $dotnet.Source @('--list-sdks') | ForEach-Object {
        if ($_ -match '^(\d+\.\d+\.\d+(?:-[^\s]+)?)\s+\[') { $Matches[1] }
    })
    $desktopRuntimes = @(Invoke-InventoryCommand $dotnet.Source @('--list-runtimes') | ForEach-Object {
        if ($_ -match '^Microsoft\.WindowsDesktop\.App\s+(\S+)\s+\[') { $Matches[1] }
    })
}
$hasSdk10 = @($installedSdks | Where-Object { $_ -match '^10\.\d+\.\d+$' }).Count -gt 0
Add-Check 'stable-sdk-10' $hasSdk10 '.NET 10 runtime presence does not supply the .NET 10 SDK; preview SDKs do not satisfy this check.'

$visualStudio = @()
$qualifiedVisualStudio = @()
$vswhereCandidates = @()
foreach ($programFilesRoot in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
    if ($programFilesRoot) {
        $vswhereCandidates += Join-Path $programFilesRoot 'Microsoft Visual Studio/Installer/vswhere.exe'
    }
}
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ($isWindowsHost -and $vswhere) {
    $allJson = (Invoke-InventoryCommand $vswhere @('-all', '-products', '*', '-format', 'json')) -join [Environment]::NewLine
    $visualStudio = @(ConvertFrom-Json -InputObject $allJson | ForEach-Object {
        [pscustomobject]@{ name = $_.displayName; version = $_.installationVersion }
    })
    $requirements = @(
        'Microsoft.VisualStudio.Workload.ManagedDesktop',
        'Microsoft.VisualStudio.Component.VC.Tools.x86.x64',
        'Microsoft.VisualStudio.Component.VC.CLI.Support'
    )
    $arguments = @('-all', '-products', '*', '-version', '[18.0,)', '-requires') + $requirements + @('-format', 'json')
    $qualifiedJson = (Invoke-InventoryCommand $vswhere $arguments) -join [Environment]::NewLine
    $qualifiedVisualStudio = @(ConvertFrom-Json -InputObject $qualifiedJson | Where-Object {
        $_.isComplete -and $_.isLaunchable -and !$_.isPrerelease -and
        (Test-Path -LiteralPath (Join-Path $_.installationPath 'MSBuild/Current/Bin/MSBuild.exe') -PathType Leaf)
    } | ForEach-Object {
        [pscustomobject]@{ name = $_.displayName; version = $_.installationVersion }
    })
}
Add-Check 'vs-2026-desktop-cpp-cli' ($qualifiedVisualStudio.Count -gt 0) 'Requires VS 2026 (18+) with managed desktop, x64 C++ tools, C++/CLI support, and MSBuild. Missing vswhere leaves this unverified.'

$windowsSdkVersions = @()
if ($isWindowsHost) {
    $kitsRoot = $null
    $kitsKey = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue
    if ($kitsKey) { $kitsRoot = $kitsKey.KitsRoot10 }
    if (!$kitsRoot -and ${env:ProgramFiles(x86)}) { $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10' }
    if ($kitsRoot -and (Test-Path -LiteralPath (Join-Path $kitsRoot 'Include') -PathType Container)) {
        $windowsSdkVersions = @(Get-ChildItem -LiteralPath (Join-Path $kitsRoot 'Include') -Directory | Where-Object {
            $version = $_.Name
            $version -match '^10\.0\.\d+\.0$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName 'um/Windows.h') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $kitsRoot "Lib/$version/um/x64/kernel32.lib") -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $kitsRoot "Lib/$version/ucrt/x64/ucrt.lib") -PathType Leaf)
        } | Select-Object -ExpandProperty Name)
    }
}
Add-Check 'windows-sdk-x64' ($windowsSdkVersions.Count -gt 0) 'Detected headers and x64 libraries are prerequisites only; the chosen SDK/native-toolset combination still needs a build.'

$projectPaths = @(
    'src/GUI/GUI.csproj', 'src/Core/DivinityModManagerCore.csproj',
    'src/Updater/ReduxUpdater.csproj', 'src/Toolbox/Toolbox.csproj',
    'tests/Redux.Core.Tests/Redux.Core.Tests.csproj',
    'tools/ReduxModDatabaseTool/ReduxModDatabaseTool.csproj',
    'tools/ReduxModDatabaseTool.Desktop/ReduxModDatabaseTool.Desktop.csproj',
    'External/lslib/LSLib/LSLib.csproj', 'External/lslib/LSLibStats/LSLibStats.csproj',
    'External/lslib/LSLibNative/LSLibNative.vcxproj',
    'External/CrossSpeak/CrossSpeak/CrossSpeak.csproj'
)
$projects = @($projectPaths | ForEach-Object {
    $relativePath = $_
    $path = Join-Path $repositoryRoot $relativePath
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        [xml]$project = Get-Content -LiteralPath $path -Raw
        [pscustomobject]@{
            path = $relativePath
            present = $true
            declaredTargetFrameworks = @($project.SelectNodes("//*[local-name()='TargetFramework' or local-name()='TargetFrameworks']") | ForEach-Object { $_.InnerText })
            declaresWpf = @($project.SelectNodes("//*[local-name()='UseWPF']") | Where-Object { $_.InnerText -eq 'true' }).Count -gt 0
            declaredNativeToolsets = @($project.SelectNodes("//*[local-name()='PlatformToolset']") | ForEach-Object { $_.InnerText } | Select-Object -Unique)
        }
    } else {
        [pscustomobject]@{ path = $relativePath; present = $false; declaredTargetFrameworks = @(); declaresWpf = $false; declaredNativeToolsets = @() }
    }
})
$missingProjects = @($projects | Where-Object { !$_.present } | Select-Object -ExpandProperty path)
$prerequisitesDetected = @($checks | Where-Object { !$_.detected }).Count -eq 0
$report = [ordered]@{
    schemaVersion = 1
    purpose = 'runtime-migration-inventory'
    target = '.NET 10 Windows x64'
    basicToolchainPrerequisitesDetected = $prerequisitesDetected
    migrationValidated = $false
    portabilityValidated = $false
    sdkPolicy = $sdkPolicy
    installedSdks = $installedSdks
    installedWindowsDesktopRuntimes = $desktopRuntimes
    visualStudio = $visualStudio
    qualifyingVisualStudio = $qualifiedVisualStudio
    windowsSdkVersions = $windowsSdkVersions
    checks = @($checks.ToArray())
    projects = $projects
    missingProjects = $missingProjects
    limitations = @(
        'Project declarations are read without MSBuild evaluation; conditional properties and overrides may differ.',
        'The SDK pin, project targets, native toolset, CI, and updater transition still require explicit migration work.',
        'No restore, compilation, package execution, upgrade, accessibility, or Linux/macOS test was performed.',
        'A .NET upgrade does not port WPF or the Windows-only C++/CLI LSLibNative bridge.'
    )
}
if ($AsJson) {
    $report | ConvertTo-Json -Depth 8
} else {
    "Target: $($report.target)"
    "Basic toolchain prerequisites detected: $prerequisitesDetected"
    "SDK pin: $($sdkPolicy.version) / $($sdkPolicy.rollForward)"
    "Installed SDKs: $($installedSdks -join ', ')"
    "Installed Desktop runtimes: $($desktopRuntimes -join ', ')"
    foreach ($check in $checks) {
        $status = if ($check.detected) { 'DETECTED' } else { 'MISSING/UNVERIFIED' }
        "[$status] $($check.id): $($check.detail)"
    }
    if ($missingProjects.Count -gt 0) { "Missing project files (including uninitialized submodules): $($missingProjects -join ', ')" }
    'Inventory only: migration and native Linux/macOS support remain unvalidated. Use -AsJson for project declarations.'
}
if ($RequireToolchain -and !$prerequisitesDetected) { exit 1 }
