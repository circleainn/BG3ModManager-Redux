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
if ($missingFiles.Count -gt 0)
{
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
}

$generationSteps = @(
	@("Gplex.exe", "External\lslib\LSLib\LS\Story\GoalParser\Goal.lex", "External\lslib\LSLib\LS\Story\GoalParser\Goal.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLib\LS\Story\GoalParser\Goal.yy", "External\lslib\LSLib\LS\Story\GoalParser\Goal.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLib\LS\Story\HeaderParser\StoryHeader.lex", "External\lslib\LSLib\LS\Story\HeaderParser\StoryHeader.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLib\LS\Story\HeaderParser\StoryHeader.yy", "External\lslib\LSLib\LS\Story\HeaderParser\StoryHeader.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLibStats\Stats\File\Stat.lex", "External\lslib\LSLibStats\Stats\File\Stat.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLibStats\Stats\File\Stat.yy", "External\lslib\LSLibStats\Stats\File\Stat.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLibStats\Stats\Functor\Functor.lex", "External\lslib\LSLibStats\Stats\Functor\Functor.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLibStats\Stats\Functor\Functor.yy", "External\lslib\LSLibStats\Stats\Functor\Functor.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLibStats\Stats\Functor\Lua.lex", "External\lslib\LSLibStats\Stats\Functor\Lua.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLibStats\Stats\Functor\Lua.yy", "External\lslib\LSLibStats\Stats\Functor\Lua.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLibStats\Stats\Functor\Requirement.lex", "External\lslib\LSLibStats\Stats\Functor\Requirement.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLibStats\Stats\Functor\Requirement.yy", "External\lslib\LSLibStats\Stats\Functor\Requirement.yy.cs"),
	@("Gplex.exe", "External\lslib\LSLibStats\Stats\Functor\RollConditions.lex", "External\lslib\LSLibStats\Stats\Functor\RollConditions.lex.cs"),
	@("Gppg.exe", "External\lslib\LSLibStats\Stats\Functor\RollConditions.yy", "External\lslib\LSLibStats\Stats\Functor\RollConditions.yy.cs")
)

foreach ($step in $generationSteps)
{
	$output = Join-Path $repositoryRoot $step[2]
	if (Test-Path -LiteralPath $output)
	{
		continue
	}

	$generator = Join-Path $toolDirectory $step[0]
	$input = Join-Path $repositoryRoot $step[1]
	Write-Host "Generating $($step[2])..."
	& $generator "/out:$output" $input
	if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $output))
	{
		throw "LSLib parser generation failed for $($step[1])."
	}
}
