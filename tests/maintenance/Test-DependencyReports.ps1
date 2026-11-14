$ErrorActionPreference = 'Stop'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempBase ('redux-dependency-test-' + [Guid]::NewGuid().ToString('N'))))
$checker = Join-Path $PSScriptRoot '../../tools/maintenance/Check-Dependencies.ps1'
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    # Keep the test offline: this function shadows dotnet only within this script's scope.
    function dotnet {
        $global:LASTEXITCODE = 0
        if ($args[0] -eq 'restore') { return }
        if ($global:ReduxDependencyFixtureScenario -eq 'query-failure') { $global:LASTEXITCODE = 1; return }
        $framework = @{ framework = 'net8.0-windows'; topLevelPackages = @(); transitivePackages = @() }
        $report = @{ version = 1; projects = @(@{ path = 'fixture.csproj'; frameworks = @($framework) }) }
        switch ($global:ReduxDependencyFixtureScenario) {
            'bad-schema' { $report.version = 2 }
            'empty' { $report.projects = @() }
            'problem' { $report.problems = @(@{ message = 'source unavailable' }) }
            'top-level' { $framework.topLevelPackages = @(@{ id = 'Example'; resolvedVersion = '1.0'; vulnerabilities = @(@{ severity = 'High' }) }) }
            'transitive' { $framework.transitivePackages = @(@{ id = 'Example'; resolvedVersion = '1.0'; vulnerabilities = @(@{ severity = 'Moderate' }) }) }
        }
        $report | ConvertTo-Json -Depth 12
    }
    foreach ($global:ReduxDependencyFixtureScenario in @('clean', 'bad-schema', 'empty', 'problem', 'top-level', 'transitive', 'query-failure')) {
        $failed = $false
        try { & $checker -SkipBuild -RepositoryRoot $fixtureRoot -OutputDirectory (Join-Path $fixtureRoot $global:ReduxDependencyFixtureScenario) }
        catch { $failed = $true }
        if ($failed -ne ($global:ReduxDependencyFixtureScenario -ne 'clean')) { throw "Unexpected result for $global:ReduxDependencyFixtureScenario" }
        Write-Host "PASS dependency report: $global:ReduxDependencyFixtureScenario"
    }
}
finally {
    if (!$fixtureRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or $fixtureRoot -eq $tempBase) {
        throw 'Refusing cleanup outside the temporary fixture directory.'
    }
    Remove-Variable -Name ReduxDependencyFixtureScenario -Scope Global -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}
$global:LASTEXITCODE = 0
