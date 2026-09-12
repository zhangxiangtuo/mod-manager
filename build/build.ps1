# Build MOD Manager with the built-in .NET Framework compiler (no dependencies needed).
# NOTE: keep this file ASCII-only so it works in Windows PowerShell 5.1 and PowerShell 7
#       regardless of the file encoding.
param(
    [string]$OutDir = (Join-Path $PSScriptRoot "..\outputs"),
    [string]$ExeName = "MODManager.exe",
    [switch]$BuildTests
)

$ErrorActionPreference = "Stop"

$frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $frameworkDir)) {
    $frameworkDir = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319"
}
$csc = Join-Path $frameworkDir "csc.exe"
if (-not (Test-Path $csc)) { throw "C# compiler not found: $csc" }

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$srcDir = Join-Path $root "src"
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.IO.Compression.dll",
    "System.IO.Compression.FileSystem.dll",
    "System.Web.Extensions.dll"
)

$sources = Get-ChildItem -Path $srcDir -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "no .cs source files found in $srcDir" }

$outExe = Join-Path $OutDir $ExeName
$cscArgs = @("/nologo", "/target:winexe", "/platform:anycpu", "/optimize+", "/codepage:65001", "/warn:4")
$cscArgs += ("/out:" + $outExe)
$icon = Join-Path $root "assets\app.ico"
if (Test-Path $icon) { $cscArgs += ("/win32icon:" + $icon) }
$manifest = Join-Path $root "assets\app.manifest"
if (Test-Path $manifest) { $cscArgs += ("/win32manifest:" + $manifest) }
foreach ($r in $refs) { $cscArgs += ("/r:" + (Join-Path $frameworkDir $r)) }
$cscArgs += $sources

Write-Host ("Compiling $($sources.Count) source files ...") -ForegroundColor Cyan
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit code $LASTEXITCODE)" }

$size = (Get-Item $outExe).Length
Write-Host ("OK: {0} ({1:N0} bytes)" -f $outExe, $size) -ForegroundColor Green

if ($BuildTests) {
    $testFile = Join-Path $root "tests\CoreTests.cs"
    $coreFiles = @(
        (Join-Path $srcDir "Model.cs"),
        (Join-Path $srcDir "Util.cs"),
        (Join-Path $srcDir "Workspace.cs"),
        $testFile
    )
    $testExe = Join-Path $root "work\CoreTests.exe"
    $testDir = Split-Path $testExe -Parent
    if (-not (Test-Path $testDir)) { New-Item -ItemType Directory -Path $testDir -Force | Out-Null }
    $testArgs = @("/nologo", "/target:exe", "/platform:anycpu", "/codepage:65001")
    $testArgs += ("/out:" + $testExe)
    foreach ($r in @("System.dll", "System.Core.dll", "System.IO.Compression.dll", "System.IO.Compression.FileSystem.dll", "System.Web.Extensions.dll")) {
        $testArgs += ("/r:" + (Join-Path $frameworkDir $r))
    }
    $testArgs += $coreFiles
    Write-Host "Compiling test runner ..." -ForegroundColor Cyan
    & $csc @testArgs
    if ($LASTEXITCODE -ne 0) { throw "test runner compile failed" }
    Write-Host ("OK: {0}" -f $testExe) -ForegroundColor Green
}
