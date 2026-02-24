#!/usr/bin/env pwsh
# Build script for HelloID.SQLite wrapper
# Downloads Microsoft.Data.Sqlite 10.0.3 and dependencies, builds the project

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$EmbeddedDir = Join-Path $ProjectDir "Embedded"
$NugetDir = Join-Path $ProjectDir "packages"

Write-Host "=== HelloID.SQLite Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Create directories
if (-not (Test-Path $EmbeddedDir)) {
    New-Item -Path $EmbeddedDir -ItemType Directory -Force | Out-Null
}
if (-not (Test-Path $NugetDir)) {
    New-Item -Path $NugetDir -ItemType Directory -Force | Out-Null
}

# Download NuGet.exe if not present
$nugetExe = Join-Path $ProjectDir "nuget.exe"
if (-not (Test-Path $nugetExe)) {
    Write-Host "Downloading NuGet.exe..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetExe
}

Write-Host ""
Write-Host "Step 1: Downloading Microsoft.Data.Sqlite 10.0.3 and dependencies..." -ForegroundColor Yellow
Write-Host ""

# Microsoft.Data.Sqlite 10.0.3 with netstandard2.0 dependencies
$packages = @(
    @{ Name = "Microsoft.Data.Sqlite.core"; Version = "10.0.3"; SourcePath = "lib\netstandard2.0\Microsoft.Data.Sqlite.dll" },
    @{ Name = "SQLitePCLRaw.config.e_sqlite3"; Version = "3.0.2"; SourcePath = "lib\netstandard2.0\SQLitePCLRaw.batteries_v2.dll" },
    @{ Name = "SQLitePCLRaw.core"; Version = "3.0.2"; SourcePath = "lib\netstandard2.0\SQLitePCLRaw.core.dll" },
    @{ Name = "SQLitePCLRaw.provider.e_sqlite3"; Version = "3.0.2"; SourcePath = "lib\netstandard2.0\SQLitePCLRaw.provider.e_sqlite3.dll" },
    @{ Name = "System.Memory"; Version = "4.6.3"; SourcePath = "lib\netstandard2.0\System.Memory.dll" },
    @{ Name = "System.Buffers"; Version = "4.6.1"; SourcePath = "lib\netstandard2.0\System.Buffers.dll" },
    @{ Name = "System.Runtime.CompilerServices.Unsafe"; Version = "6.1.2"; SourcePath = "lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" },
    @{ Name = "System.Numerics.Vectors"; Version = "4.6.1"; SourcePath = "lib\netstandard2.0\System.Numerics.Vectors.dll" }
)

# Native SQLite package (not embedded - copied to output)
$nativePackage = @{ Name = "SourceGear.sqlite3"; Version = "3.50.4.5" }

foreach ($pkg in $packages) {
    Write-Host "  Downloading $($pkg.Name) v$($pkg.Version)..." -ForegroundColor Gray
    & $nugetExe install $pkg.Name -Version $pkg.Version -OutputDirectory $NugetDir -Source "https://api.nuget.org/v3/index.json" | Out-Null
}

# Download native SQLite
Write-Host "  Downloading $($nativePackage.Name) v$($nativePackage.Version) (native)..." -ForegroundColor Gray
& $nugetExe install $nativePackage.Name -Version $nativePackage.Version -OutputDirectory $NugetDir -Source "https://api.nuget.org/v3/index.json" | Out-Null

Write-Host ""
Write-Host "Step 2: Copying DLLs to Embedded folder..." -ForegroundColor Yellow
Write-Host ""

foreach ($pkg in $packages) {
    $folderPattern = "$($pkg.Name).$($pkg.Version)"
    $sourcePath = Join-Path $NugetDir "$folderPattern\$($pkg.SourcePath)"
    $destPath = Join-Path $EmbeddedDir (Split-Path $pkg.SourcePath -Leaf)
    
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath $destPath -Force
        $fileInfo = Get-Item $destPath
        Write-Host "  Copied: $(Split-Path $pkg.SourcePath -Leaf) ($([math]::Round($fileInfo.Length / 1KB, 1)) KB)" -ForegroundColor Green
    }
    else {
        Write-Host "  ERROR: Not found: $sourcePath" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Step 3: Verifying Embedded DLLs..." -ForegroundColor Yellow
Write-Host ""

$expectedDlls = @(
    "Microsoft.Data.Sqlite.dll",
    "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.core.dll",
    "SQLitePCLRaw.provider.e_sqlite3.dll",
    "System.Memory.dll",
    "System.Buffers.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Numerics.Vectors.dll"
)

foreach ($dll in $expectedDlls) {
    $dllPath = Join-Path $EmbeddedDir $dll
    if (Test-Path $dllPath) {
        $fileInfo = Get-Item $dllPath
        Write-Host "  OK: $dll ($([math]::Round($fileInfo.Length / 1KB, 1)) KB)" -ForegroundColor Green
    }
    else {
        Write-Host "  MISSING: $dll" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Step 4: Cleaning previous build..." -ForegroundColor Yellow
Write-Host ""

$binDir = Join-Path $ProjectDir "bin"
$objDir = Join-Path $ProjectDir "obj"
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
Write-Host "  Cleaned bin/ and obj/" -ForegroundColor Green

Write-Host ""
Write-Host "Step 5: Building project..." -ForegroundColor Yellow
Write-Host ""

& dotnet build (Join-Path $ProjectDir "HelloID.SQLite.csproj") -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

$outputPath = Join-Path $ProjectDir "bin\$Configuration\HelloID.SQLite.dll"

# Copy native SQLite runtime to output
Write-Host ""
Write-Host "Step 6: Copying native SQLite runtime..." -ForegroundColor Yellow
Write-Host ""

$nativeSourceDir = Join-Path $NugetDir "$($nativePackage.Name).$($nativePackage.Version)\runtimes\win-x64\native"
$nativeDestDir = Join-Path $ProjectDir "bin\$Configuration\runtimes\win-x64\native"

if (Test-Path $nativeSourceDir) {
    if (-not (Test-Path $nativeDestDir)) {
        New-Item -Path $nativeDestDir -ItemType Directory -Force | Out-Null
    }
    Copy-Item -Path "$nativeSourceDir\*" -Destination $nativeDestDir -Force
    Write-Host "  Copied native SQLite to: runtimes\win-x64\native\" -ForegroundColor Green
}
else {
    Write-Host "  WARNING: Native runtime not found at $nativeSourceDir" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $outputPath" -ForegroundColor Cyan
$outputInfo = Get-Item $outputPath
Write-Host "Size: $([math]::Round($outputInfo.Length / 1KB, 1)) KB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Deploy to HelloID SourceData folder:" -ForegroundColor White
Write-Host "  - HelloID.SQLite.dll" -ForegroundColor Gray
Write-Host "  - runtimes\win-x64\native\e_sqlite3.dll" -ForegroundColor Gray
Write-Host ""
