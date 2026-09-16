# Build CompanyDecorations.dll with Framework csc (no .NET SDK required)
$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $modRoot 'src'
$outDll = Join-Path $modRoot 'CompanyDecorations.dll'
$gameRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$managed = Join-Path $gameRoot 'BattleTech_Data\Managed'
$harmony = Join-Path $gameRoot 'Mods\ModTek\lib\0Harmony.dll'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $harmony)) { throw "0Harmony.dll not found at $harmony" }

$refs = @(
  (Join-Path $managed 'Assembly-CSharp.dll'),
  (Join-Path $managed 'Newtonsoft.Json.dll'),
  (Join-Path $managed 'UnityEngine.CoreModule.dll'),
  (Join-Path $managed 'UnityEngine.dll'),
  (Join-Path $managed 'UnityEngine.UI.dll'),
  (Join-Path $managed 'UnityEngine.UIModule.dll'),
  (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
  (Join-Path $managed 'UnityEngine.InputModule.dll'),
  (Join-Path $managed 'UnityEngine.TextCoreModule.dll'),
  (Join-Path $managed 'UnityEngine.ImageConversionModule.dll'),
  (Join-Path $managed 'Unity.TextMeshPro.dll'),
  $harmony
)

foreach ($r in $refs) {
  if (-not (Test-Path $r)) { throw "Missing reference: $r" }
}

$sources = Get-ChildItem $srcDir -Filter '*.cs' | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No .cs files in $srcDir" }

$outTemp = Join-Path $modRoot 'CompanyDecorations.dll.build'
$refArgs = $refs | ForEach-Object { '/reference:"{0}"' -f $_ }
$args = @(
  '/nologo',
  '/optimize+',
  '/target:library',
  ('/out:"{0}"' -f $outTemp)
) + $refArgs + ($sources | ForEach-Object { '"{0}"' -f $_ })

Write-Host "Compiling CompanyDecorations.dll..."
Write-Host ($args -join ' ')
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit $LASTEXITCODE" }

try {
  Copy-Item -Force $outTemp $outDll
  Remove-Item -Force $outTemp -ErrorAction SilentlyContinue
  Write-Host "OK -> $outDll"
  Get-Item $outDll | Format-List Name, Length, LastWriteTime
}
catch {
  Write-Host "Compiled OK to $outTemp but could not replace live DLL (game likely running)."
  Write-Host "Close BattleTech, then run: Copy-Item -Force `"$outTemp`" `"$outDll`""
  throw
}
