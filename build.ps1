param([switch]$SkipCudaCompile)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $project
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw '.NET Framework 4.x compiler is missing.'}
New-Item -ItemType Directory -Force dist | Out-Null
$bc=Join-Path $project 'third_party\bouncycastle\lib\net461\BouncyCastle.Cryptography.dll'
if(!(Test-Path -LiteralPath $bc)){throw 'Run bootstrap.ps1 first.'}
Copy-Item -LiteralPath $bc -Destination dist
Copy-Item -LiteralPath src/kernel.cu -Destination dist
$nvrtcRoot=Join-Path $project '.tools\nvrtc\cuda_nvrtc-windows-x86_64-12.8.93-archive'
Copy-Item -LiteralPath (Join-Path $nvrtcRoot 'bin\nvrtc64_120_0.dll'),(Join-Path $nvrtcRoot 'bin\nvrtc-builtins64_128.dll') -Destination dist
$sources=Get-ChildItem -LiteralPath src -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /optimize+ /platform:x64 /target:exe /out:dist/vanity.exe /r:System.IO.Compression.dll /r:System.Xml.dll /r:System.Numerics.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$bc" $sources
if($LASTEXITCODE -ne 0){throw 'C# compile failed.'}
& $compiler /nologo /optimize+ /platform:x64 /target:winexe /out:dist/Astra.exe /r:System.IO.Compression.dll /r:System.Xml.dll /r:System.Numerics.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "/r:$bc" /define:WINDOWS_GUI $sources
if($LASTEXITCODE -ne 0){throw 'GUI compile failed.'}
if(!$SkipCudaCompile){& .\dist\vanity.exe --compile;if($LASTEXITCODE -ne 0){throw 'CUDA compile failed.'}}
Write-Host 'Build complete: dist\vanity.exe and dist\Astra.exe'
