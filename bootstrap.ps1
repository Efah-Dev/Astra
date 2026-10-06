$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $MyInvocation.MyCommand.Path)
New-Item -ItemType Directory -Force .tools,third_party | Out-Null
$nvrtcUrl='https://developer.download.nvidia.com/compute/cuda/redist/cuda_nvrtc/windows-x86_64/cuda_nvrtc-windows-x86_64-12.8.93-archive.zip'
$bcUrl='https://api.nuget.org/v3-flatcontainer/bouncycastle.cryptography/2.6.2/bouncycastle.cryptography.2.6.2.nupkg'
if(!(Test-Path .tools/nvrtc.zip)){Invoke-WebRequest -UseBasicParsing $nvrtcUrl -OutFile .tools/nvrtc.zip}
if((Get-FileHash .tools/nvrtc.zip -Algorithm SHA256).Hash -ne 'A63302A077F0248A743A1A7CAA7DBD80D0FAC56C6CFA9C41FA05FAC9B7E5EDA5'){throw 'NVRTC SHA256 mismatch'}
if(!(Test-Path .tools/bouncycastle.zip)){Invoke-WebRequest -UseBasicParsing $bcUrl -OutFile .tools/bouncycastle.zip}
if((Get-FileHash .tools/bouncycastle.zip -Algorithm SHA256).Hash -ne '623936FB1FD171579C706390390711282898CF2C759A5167852E5AB82208F8CB'){throw 'Bouncy Castle SHA256 mismatch'}
Expand-Archive -LiteralPath .tools/nvrtc.zip -DestinationPath .tools/nvrtc -Force
Expand-Archive -LiteralPath .tools/bouncycastle.zip -DestinationPath third_party/bouncycastle -Force
Write-Host 'Dependencies ready. Run .\build.ps1'
