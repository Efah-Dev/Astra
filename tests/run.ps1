param([switch]$SkipInterrupt)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location -LiteralPath $root
New-Item -ItemType Directory -Force artifacts,test-output | Out-Null
& .\dist\vanity.exe --self-test --extended | Tee-Object -FilePath artifacts/self-test.log
if($LASTEXITCODE -ne 0){throw 'Self-test failed'}
& .\dist\vanity.exe --integration-test | Tee-Object -FilePath artifacts/integration.log
if($LASTEXITCODE -ne 0){throw 'Integration test failed'}
& .\dist\vanity.exe --export-test | Tee-Object -FilePath artifacts/export-test.log
if($LASTEXITCODE -ne 0){throw 'Export test failed'}
if(!$SkipInterrupt){
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 & $compiler /nologo /platform:x64 /out:artifacts/ConsoleInterrupt.exe (Join-Path $root 'tests\ConsoleInterrupt.cs')
 if($LASTEXITCODE -ne 0){throw 'Interrupt harness build failed'}
 $result=Join-Path $root 'artifacts\interrupt.log'
 $wallet=Join-Path $root ('test-output\interrupt_'+[Guid]::NewGuid().ToString('N')+'.txt')
 $arguments='"'+(Join-Path $root 'dist\vanity.exe')+'" "'+$result+'" "'+$wallet+'"'
 $testProcess=Start-Process -FilePath (Join-Path $root 'artifacts\ConsoleInterrupt.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
 Get-Content -LiteralPath $result
 if($testProcess.ExitCode -ne 0){throw 'Ctrl+C test failed'}
}
