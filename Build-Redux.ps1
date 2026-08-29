[CmdletBinding()]
param(
	[ValidateSet("Debug", "Publish")]
	[string]$Configuration = "Debug",

	[switch]$Rebuild,

	[string]$PythonExecutable
)

$ErrorActionPreference = "Stop"

$vswhereCandidates = @(
	(Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
	(Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
)
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$installationPath = $null

if ($vswhere)
{
	$requirements = @(
		"Microsoft.VisualStudio.Workload.ManagedDesktop",
		"Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
		"Microsoft.VisualStudio.Component.VC.CLI.Support"
	)
	$vswhereArguments = @("-latest", "-products", "*")
	foreach ($requirement in $requirements)
	{
		$vswhereArguments += @("-requires", $requirement)
	}
	$vswhereArguments += @("-property", "installationPath")
	$installationPath = (& $vswhere @vswhereArguments | Select-Object -First 1)
}

if ([String]::IsNullOrWhiteSpace($installationPath))
{
	$msbuildCandidate = Get-ChildItem `
		-Path (Join-Path $env:ProgramFiles "Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe") `
		-File `
		-ErrorAction SilentlyContinue |
		Sort-Object FullName -Descending |
		Select-Object -First 1
	if ($msbuildCandidate)
	{
		$installationPath = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $msbuildCandidate.FullName)))
	}
}

if ([String]::IsNullOrWhiteSpace($installationPath))
{
	throw "Visual Studio with .NET desktop, Desktop C++, and C++/CLI support could not be found."
}

$msbuild = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
if (!(Test-Path -LiteralPath $msbuild))
{
	throw "MSBuild was not found under the detected Visual Studio installation: $installationPath"
}

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$prepareLSLib = Join-Path $repositoryRoot "Prepare-LSLib.ps1"
& $prepareLSLib

$solution = Join-Path $repositoryRoot "BG3ModManager.sln"
$target = if ($Rebuild) { "Rebuild" } else { "Build" }
$msbuildArguments = @(
	$solution,
	"/restore",
	"/t:$target",
	"/p:Configuration=$Configuration",
	"/p:Platform=x64",
	"/m",
	"/v:minimal"
)

if ($Configuration -eq "Publish")
{
	# LSLibNative exposes Debug/Release configurations only. Restore evaluates the
	# project before solution mappings are applied, so use Release for restore while
	# retaining Publish for the actual solution build.
	$msbuildArguments += "/restoreProperty:Configuration=Release"

	if ([String]::IsNullOrWhiteSpace($PythonExecutable))
	{
		$PythonExecutable = $env:REDUX_PYTHON
	}
	if ([String]::IsNullOrWhiteSpace($PythonExecutable))
	{
		foreach ($commandName in @("python", "python3"))
		{
			$command = Get-Command $commandName -ErrorAction SilentlyContinue
			if (!$command) { continue }
			& $command.Source --version *> $null
			if ($LASTEXITCODE -eq 0)
			{
				$PythonExecutable = $command.Source
				break
			}
		}
	}
	if ([String]::IsNullOrWhiteSpace($PythonExecutable))
	{
		throw "Python 3 is required for Publish packaging. Pass -PythonExecutable or set REDUX_PYTHON."
	}
	& $PythonExecutable --version *> $null
	if ($LASTEXITCODE -ne 0)
	{
		throw "The configured Python executable could not be started: $PythonExecutable"
	}
	$msbuildArguments += "/p:PythonExecutable=$PythonExecutable"
}

Write-Host "Building Redux $Configuration x64 with $msbuild"
& $msbuild @msbuildArguments
if ($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}
