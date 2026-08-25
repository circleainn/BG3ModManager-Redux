[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolDirectory = Join-Path $repositoryRoot "External\lslib\external\gppg\binaries"
$requiredFiles = @(
	"Gplex.exe",
	"Gppg.exe",
	"QUT.ShiftReduceParser.dll"
)

$missingFiles = $requiredFiles | Where-Object {
	!(Test-Path -LiteralPath (Join-Path $toolDirectory $_))
}
if ($missingFiles.Count -eq 0)
{
	return
}

$archiveUri = "https://s3.eu-central-1.amazonaws.com/nb-stor/dos-legacy/ExportTool/gppg-distro-1_5_2.zip"
$expectedHash = "94B55A051200471ABA765D50404ABE66A14742913E224ADCB65B4E8383D2905A"
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temporaryRoot = Join-Path $temporaryBase ("bg3redux-gppg-" + [Guid]::NewGuid().ToString("N"))
$resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
if (!$resolvedTemporaryRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase))
{
	throw "Unexpected LSLib tool staging path: $resolvedTemporaryRoot"
}

try
{
	New-Item -ItemType Directory -Path $resolvedTemporaryRoot | Out-Null
	$archivePath = Join-Path $resolvedTemporaryRoot "gppg.zip"
	$extractionPath = Join-Path $resolvedTemporaryRoot "expanded"

	Write-Host "Downloading the pinned LSLib parser tools..."
	Invoke-WebRequest -Uri $archiveUri -OutFile $archivePath
	$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
	if ($actualHash -ne $expectedHash)
	{
		throw "The LSLib parser-tool archive checksum did not match the pinned value."
	}

	Expand-Archive -LiteralPath $archivePath -DestinationPath $extractionPath
	$sourceDirectory = Join-Path $extractionPath "gppg-distro-1_5_2\binaries"
	New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
	foreach ($fileName in $requiredFiles)
	{
		$source = Join-Path $sourceDirectory $fileName
		if (!(Test-Path -LiteralPath $source))
		{
			throw "The LSLib parser-tool archive is missing $fileName."
		}
		Copy-Item -LiteralPath $source -Destination (Join-Path $toolDirectory $fileName) -Force
	}
}
finally
{
	if (Test-Path -LiteralPath $resolvedTemporaryRoot)
	{
		Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
	}
}
