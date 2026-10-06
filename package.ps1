$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $root
$release=Get-Content -Raw -LiteralPath (Join-Path $root 'release.json') | ConvertFrom-Json
if($release.version -notmatch '^\d+\.\d+\.\d+$' -or $release.locale -notin @('zh-CN','en-US')){throw 'Invalid release metadata.'}
$tag='Astra-v'+$release.version+'-'+$release.locale
$stage=Join-Path $root ('artifacts\package_'+[Guid]::NewGuid().ToString('N'))
$portable=Join-Path $stage ($tag+'-Windows-x64')
$source=Join-Path $stage ($tag+'-Source')
$releaseDir=Join-Path $root 'releases'
New-Item -ItemType Directory -Force $portable,$source,$releaseDir | Out-Null
function Write-Manifest([string]$directory){
 $manifest=Get-ChildItem -LiteralPath $directory -File -Recurse | Sort-Object FullName | ForEach-Object { [pscustomobject]@{path=$_.FullName.Substring($directory.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} }
 $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'SHA256.json') -Encoding UTF8
}
try {
 # Explicit allowlists: never distribute generated wallets, test output or local build caches.
 foreach($name in @('Astra.exe','vanity.exe','BouncyCastle.Cryptography.dll','nvrtc64_120_0.dll','nvrtc-builtins64_128.dll','kernel.cu','kernel-sm120.ptx')){Copy-Item -LiteralPath (Join-Path $root ('dist\'+$name)) -Destination $portable}
 Copy-Item -LiteralPath README.md,release.json,docs,licenses -Destination $portable -Recurse
 Copy-Item -LiteralPath src,tests,docs,licenses -Destination $source -Recurse
 Copy-Item -LiteralPath README.md,build.ps1,bootstrap.ps1,package.ps1,release.json,.gitignore -Destination $source
 Write-Manifest $portable
 Write-Manifest $source
 $portableZip=Join-Path $releaseDir ($tag+'-Windows-x64.zip')
 $sourceZip=Join-Path $releaseDir ($tag+'-Source.zip')
 Compress-Archive -LiteralPath $portable -DestinationPath $portableZip -Force
 Compress-Archive -LiteralPath $source -DestinationPath $sourceZip -Force
 $hashes=Get-FileHash -LiteralPath $portableZip,$sourceZip -Algorithm SHA256
 $hashes | ForEach-Object { $_.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Encoding ASCII
 $hashes | Format-Table
} finally {
 $resolved=[IO.Path]::GetFullPath($stage)
 $allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
 if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe staging cleanup path.'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
