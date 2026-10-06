param([ValidateSet('light','dark')][string]$Theme='light',[switch]$Preview)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location -LiteralPath $root
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@(Get-ChildItem -LiteralPath src -Filter '*.cs' | ForEach-Object FullName)+(Join-Path $root 'tests\GuiPreview.cs')
& $compiler /nologo /optimize+ /platform:x64 /target:winexe /main:GuiPreview /out:dist/GuiPreview.exe /r:System.IO.Compression.dll /r:System.Xml.dll /r:System.Numerics.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:dist/BouncyCastle.Cryptography.dll $sources
if($LASTEXITCODE -ne 0){throw 'UI fixture build failed'}
if(!$Preview){$p=Start-Process -FilePath (Join-Path $root 'dist\GuiPreview.exe') -ArgumentList @($Theme,'--close') -WindowStyle Hidden -Wait -PassThru;Get-Content -LiteralPath ('dist\ui-'+$Theme+'.log');if($p.ExitCode -ne 0){throw 'UI regression failed'}}
else {Write-Host 'Preview fixture ready. Launch dist\GuiPreview.exe with light/dark.'}
