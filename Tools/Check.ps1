# -Godot defaults to the GODOT environment variable: the Godot 4.7.2 .NET *console* executable.
param([string]$Godot = $env:GODOT)
$ErrorActionPreference = 'Stop'
if (-not $Godot) { throw 'Pass -Godot or set GODOT to the Godot 4.7.2 .NET console executable.' }
$projectRoot = Split-Path -Parent $PSScriptRoot

# Each Godot check prints this marker on success; the settings-reload check must follow settings-test.
$checks = [ordered]@{
    'smoke-test'           = 'SMOKE PASS:'
    'ui-test'              = 'UI PASS:'
    'campaign-test'        = 'CAMPAIGN PASS:'
    'classic-test'         = 'CLASSIC PASS:'
    'view-state-test'      = 'VIEW STATE PASS:'
    'scenery-test'         = 'SCENERY PASS:'
    'settings-test'        = 'SETTINGS PASS:'
    'settings-reload-test' = 'SETTINGS RELOAD PASS:'
}

# Godot logs GD.PushError, engine errors and unhandled C# exceptions (which it catches and keeps
# running after) with one of these prefixes. A check that logs any of them fails, even if it
# reached its PASS marker.
$errorPattern = '^(USER |SCRIPT |USER SCRIPT |SHADER )?ERROR:'
$warningPattern = '^(USER |SCRIPT |USER SCRIPT )?WARNING:'

Push-Location $projectRoot
try {
    dotnet build Breakwater.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Godot C# build failed.' }

    dotnet test --project Tests/Breakwater.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Simulation tests failed.' }

    # Import the art in assets/ (models, textures, skies) so a fresh clone runs without opening the editor.
    & $Godot --headless --path $projectRoot --import 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Godot asset import failed.' }

    New-Item -ItemType Directory -Force .godot/qa | Out-Null
    foreach ($check in $checks.Keys) {
        # Stringify each line: Godot writes errors to stderr, which arrive as ErrorRecords.
        $checkOutput = & $Godot --headless --path $projectRoot --fixed-fps 60 --quit-after 1800 -- "--$check" 2>&1 |
            ForEach-Object { "$_" }
        $exitCode = $LASTEXITCODE
        $checkOutput | Write-Output

        $errors = @($checkOutput | Where-Object { $_ -match $errorPattern })
        $warnings = @($checkOutput | Where-Object { $_ -match $warningPattern })
        if ($warnings.Count -gt 0) {
            Write-Warning "$check logged $($warnings.Count) Godot warning(s):`n$($warnings -join "`n")"
        }

        if ($errors.Count -gt 0) {
            throw "$check logged $($errors.Count) Godot error(s):`n$($errors -join "`n")"
        }

        if ($exitCode -ne 0 -or ($checkOutput -join "`n") -notmatch [regex]::Escape($checks[$check])) {
            throw "$check failed or timed out."
        }
    }
}
finally {
    Pop-Location
}
