[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$prepareLSLib = Join-Path $repositoryRoot "Prepare-LSLib.ps1"
& $prepareLSLib

$project = Join-Path $repositoryRoot "tests\Redux.Core.Tests\Redux.Core.Tests.csproj"
$solutionDirectory = "$repositoryRoot\"
$offlinePackages = Join-Path $env:USERPROFILE ".nuget\packages"
$restoreArguments = @(
	"restore",
	$project,
	"--source",
	$offlinePackages,
	"--source",
	"https://api.nuget.org/v3/index.json",
	"--ignore-failed-sources",
	"--property:Platform=x64",
	"--property:SolutionDir=$solutionDirectory"
)

& dotnet @restoreArguments
if ($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

$vswhereCandidates = @(
	(Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
	(Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
)
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$installationPath = $null
if ($vswhere)
{
	$installationPath = (& $vswhere `
		"-latest" `
		"-products" "*" `
		"-requires" "Microsoft.VisualStudio.Workload.ManagedDesktop" `
		"-requires" "Microsoft.VisualStudio.Component.VC.Tools.x86.x64" `
		"-requires" "Microsoft.VisualStudio.Component.VC.CLI.Support" `
		"-property" "installationPath" | Select-Object -First 1)
}

if ([String]::IsNullOrWhiteSpace($installationPath))
{
	throw "Visual Studio with .NET desktop, Desktop C++, and C++/CLI support could not be found."
}

$msbuild = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
& $msbuild $project `
	"/t:Build" `
	"/p:Configuration=Debug" `
	"/p:Platform=x64" `
	"/p:SolutionDir=$solutionDirectory" `
	"/v:minimal"

if ($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

$executable = Join-Path $repositoryRoot "tests\Redux.Core.Tests\bin\x64\Debug\net8.0-windows10.0.22621.0\Redux.Core.Tests.exe"
& $executable
exit $LASTEXITCODE
