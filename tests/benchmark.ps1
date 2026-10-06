param([int]$Seconds=30)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location -LiteralPath $root
$stamp=Get-Date -Format yyyyMMdd_HHmmss
$folder=Join-Path $root ('artifacts\benchmark_'+$stamp)
New-Item -ItemType Directory -Force $folder,test-output | Out-Null
nvidia-smi --query-gpu=name,memory.total,driver_version,compute_cap --format=csv | Set-Content (Join-Path $folder 'hardware.csv')
$cases=@(
 @{Name='eth-strict8-g128-b32';Args=@('--chain','eth','--suffix-repeat','8','--case','sensitive','--groups','128','--batch','32')},
 @{Name='polygon-strict8-g128-b32';Args=@('--chain','polygon','--suffix-repeat','8','--case','sensitive','--groups','128','--batch','32')},
 @{Name='tron-strict8-g128-b32';Args=@('--chain','tron','--suffix-repeat','8','--case','sensitive','--groups','128','--batch','32')},
 @{Name='polygon-strict8-g256-b64';Args=@('--chain','polygon','--suffix-repeat','8','--case','sensitive','--groups','256','--batch','64')},
 @{Name='polygon-easy1';Args=@('--chain','polygon','--suffix-repeat','1','--case','insensitive','--groups','128','--batch','32')}
)
foreach($case in $cases){
 $wallet=Join-Path $root ('test-output\'+$stamp+'-'+$case.Name+'.txt')
 $log=Join-Path $folder ($case.Name+'.log')
 $argsList=$case.Args+@('--count','0','--seconds',"$Seconds",'--output',$wallet)
 ('vanity.exe '+($argsList -join ' ')) | Set-Content -LiteralPath $log
 $watch=[Diagnostics.Stopwatch]::StartNew()
 & .\dist\vanity.exe @argsList | Tee-Object -FilePath $log -Append
 if($LASTEXITCODE -ne 0){throw ('Benchmark failed: '+$case.Name)}
 ('wall_seconds='+$watch.Elapsed.TotalSeconds.ToString('F3')) | Add-Content -LiteralPath $log
}
Write-Host ('Benchmark records: '+$folder)
