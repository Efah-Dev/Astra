param([int]$Seconds=120,[int]$ReseedSeconds=2)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location -LiteralPath $root
New-Item -ItemType Directory -Force artifacts,test-output | Out-Null
$stamp=Get-Date -Format yyyyMMdd_HHmmss
$wallet=Join-Path $root ('test-output\soak_'+$stamp+'.txt')
$log=Join-Path $root ('artifacts\soak_'+$stamp+'.log')
& .\dist\vanity.exe --chain polygon --suffix-repeat 8 --case sensitive --count 0 --seconds $Seconds --reseed-seconds $ReseedSeconds --output $wallet | Tee-Object -FilePath $log
if($LASTEXITCODE -ne 0){throw 'Soak test failed'}
