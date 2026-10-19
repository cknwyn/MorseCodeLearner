param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Locate MSVC toolset with ml64.exe and link.exe
$ml64 = Get-ChildItem -Path "C:\Program Files\Microsoft Visual Studio" -Filter "ml64.exe" -Recurse -ErrorAction SilentlyContinue | 
        Where-Object { $_.FullName -like "*Hostx64\x64\ml64.exe*" } | 
        Select-Object -First 1 -ExpandProperty FullName

if (-not $ml64) {
    throw "ml64.exe could not be located in Visual Studio installations."
}

$msvcBinDir = Split-Path -Parent $ml64
$link = Join-Path $msvcBinDir "link.exe"

$objDir = Join-Path $ScriptDir "obj\$Configuration"
if (-not (Test-Path $objDir)) {
    New-Item -ItemType Directory -Path $objDir -Force | Out-Null
}

$coreDir = Join-Path $ScriptDir "core"
$tablesDir = Join-Path $ScriptDir "tables"
$defFile = Join-Path $ScriptDir "MorseCore.def"

$asmFiles = @(
    (Join-Path $tablesDir "lookup_tables.asm"),
    (Join-Path $coreDir "encoder.asm"),
    (Join-Path $coreDir "decoder.asm"),
    (Join-Path $coreDir "quantizer.asm"),
    (Join-Path $coreDir "benchmark.asm"),
    (Join-Path $coreDir "dllmain.asm")
)

$objFiles = @()

foreach ($asm in $asmFiles) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($asm)
    $objPath = Join-Path $objDir "$baseName.obj"
    & $ml64 /c /nologo /Fo"$objPath" "$asm"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed assembling $asm"
    }
    $objFiles += $objPath
}

# Output targets
$outDirs = @(
    (Join-Path $ScriptDir "bin\x64\$Configuration"),
    (Join-Path $ScriptDir "..\MorseApp\bin\$Configuration\net8.0-windows"),
    (Join-Path $ScriptDir "..\MorseApp\bin\x64\$Configuration\net8.0-windows")
)

foreach ($targetDir in $outDirs) {
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    $dllOut = Join-Path $targetDir "MorseCore.dll"
    & $link /DLL /NOENTRY /NODEFAULTLIB /DEF:"$defFile" /OUT:"$dllOut" @objFiles
    if ($LASTEXITCODE -ne 0) {
        throw "Failed linking $dllOut"
    }
}

Write-Host "MorseCore.dll successfully compiled and deployed for $Configuration."
