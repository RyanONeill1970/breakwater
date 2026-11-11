param(
    [string]$Godot,
    [ValidateSet('overview', 'harbour', 'road', 'detail', 'quay', 'coast')][string]$View = 'detail',
    [string]$Screenshot,
    [ValidateRange(100, 3600)][int]$Frames = 240,
    [switch]$Orbit
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Godot) {
    $command = Get-Command godot -ErrorAction SilentlyContinue
    if ($command) { $Godot = $command.Source }
    else {
        $candidate = Join-Path $env:USERPROFILE 'Downloads/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe'
        if (Test-Path -LiteralPath $candidate) { $Godot = $candidate }
        else { throw 'Pass -Godot with the path to the Godot .NET executable.' }
    }
}

Push-Location $projectRoot
try {
    dotnet build Breakwater.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Godot --headless --path $projectRoot --import
    if ($LASTEXITCODE -ne 0) { throw 'Asset import failed.' }
    $arguments = @('--path', $projectRoot, 'res://ArtStudy.tscn', '--windowed', '--resolution', '1600x1000', '--', "--art-view=$View")
    if ($Screenshot) {
        $destination = [IO.Path]::GetFullPath($Screenshot)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        $arguments += @("--screenshot=$destination", "--screenshot-frame=$Frames")
    }
    if ($Orbit) { $arguments += '--art-orbit' }
    & $Godot @arguments
    if ($LASTEXITCODE -ne 0) { throw 'The art study did not exit successfully.' }
}
finally {
    Pop-Location
}
