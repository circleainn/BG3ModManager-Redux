[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [string]$OutputDirectory,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..')
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts/maintenance'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    # Restore the C++/CLI graph with the supported Windows toolchain, not Linux Dependabot.
    if (!$SkipBuild) {
        & (Join-Path $repositoryRoot 'Build-Redux.ps1') -Configuration Debug
        if ($LASTEXITCODE -ne 0) { throw 'Windows build/restore failed; dependency status is unknown.' }
    }
    # Maintainer tools are outside the application solution and need their own restore.
    & dotnet restore tools/ReduxModDatabaseTool.Desktop/ReduxModDatabaseTool.Desktop.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Maintainer-tool restore failed; dependency status is unknown.' }

    $projects = @(
        'tests/Redux.Core.Tests/Redux.Core.Tests.csproj',
        'tools/ReduxModDatabaseTool.Desktop/ReduxModDatabaseTool.Desktop.csproj'
    )
    $vulnerableCount = 0
    foreach ($index in 0..($projects.Count - 1)) {
        foreach ($kind in @('vulnerable', 'outdated')) {
            $raw = & dotnet list $projects[$index] package "--$kind" --include-transitive --format json --output-version 1
            if ($LASTEXITCODE -ne 0) { throw "NuGet $kind query failed for $($projects[$index])." }
            $report = ($raw -join [Environment]::NewLine) | ConvertFrom-Json
            if ($report.version -ne 1 -or @($report.projects).Count -eq 0) {
                throw "NuGet returned an unsupported or empty $kind report."
            }
            foreach ($problem in @($report.problems)) {
                if ($problem) { throw "NuGet reported a problem: $($problem | ConvertTo-Json -Compress)" }
            }
            $raw | Set-Content -LiteralPath (Join-Path $OutputDirectory "$index-$kind.json") -Encoding utf8
            if ($kind -eq 'vulnerable') {
                foreach ($project in $report.projects) {
                    foreach ($framework in $project.frameworks) {
                        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                            if ($package -and @($package.vulnerabilities).Count -gt 0 -and $package.vulnerabilities) {
                                $vulnerableCount++
                                Write-Warning "Vulnerable package: $($package.id) $($package.resolvedVersion)"
                            }
                        }
                    }
                }
            }
        }
    }
    if ($vulnerableCount -gt 0) { throw "$vulnerableCount vulnerable package occurrences require review. See $OutputDirectory." }
    Write-Host "No known vulnerable dependencies reported. Update candidates are recorded in $OutputDirectory."
}
finally {
    Pop-Location
}
