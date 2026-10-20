[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
# Use the supported solution build so native C++/CLI references, configuration
# mappings, submodule preparation, and NuGet sources match the application build.
& (Join-Path $repositoryRoot "Build-Redux.ps1") -Configuration Debug
if ($LASTEXITCODE -ne 0)
{
	exit $LASTEXITCODE
}

$executable = Join-Path $repositoryRoot "tests\Redux.Core.Tests\bin\x64\Debug\net8.0-windows10.0.22621.0\Redux.Core.Tests.exe"
& $executable
exit $LASTEXITCODE
