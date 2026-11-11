param([switch]$DetailOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $projectRoot 'assets/art-study'
$headers = @{ 'User-Agent' = 'Breakwater-ArtStudy/1.0 (local game art prototype)' }
$assets = @('rock_07', 'rock_09', 'island_tree_01', 'wild_rooibos_bush')
$detailAssets = @('coastal_cliff_01', 'fern_02', 'rock_face_02', 'boulder_01')
if ($DetailOnly) { $assets = $detailAssets }
else { $assets += $detailAssets }

# Download only the named CC0 models and their declared glTF dependencies.
# Keep the original manifests and check every file against its published checksum.
foreach ($asset in $assets) {
    $folder = Join-Path $assetRoot $asset
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $manifest = Invoke-RestMethod -Uri "https://api.polyhaven.com/files/$asset" -Headers $headers
    $manifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $folder 'source-manifest.json') -Encoding utf8
    $resolution = if ($asset -in $detailAssets) { '2k' } else { '1k' }
    $entry = $manifest.gltf.$resolution.gltf
    if (-not $entry) { throw "No $resolution glTF available for $asset" }
    $files = @([PSCustomObject]@{ Name = "$($asset)_$resolution.gltf"; Value = $entry }) + @($entry.include.PSObject.Properties)
    foreach ($file in $files) {
        $target = [IO.Path]::GetFullPath((Join-Path $folder $file.Name))
        $prefix = [IO.Path]::GetFullPath($folder) + [IO.Path]::DirectorySeparatorChar
        if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid dependency path' }
        $uri = [Uri]$file.Value.url
        if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'dl.polyhaven.org') { throw 'Unexpected download host' }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm MD5).Hash -ne $file.Value.md5) {
            Invoke-WebRequest -Uri $uri -Headers $headers -OutFile $target
        }
        if ((Get-FileHash -LiteralPath $target -Algorithm MD5).Hash -ne $file.Value.md5) { throw "Checksum mismatch: $target" }
    }
    Write-Output "Verified CC0 asset: $asset"
}
