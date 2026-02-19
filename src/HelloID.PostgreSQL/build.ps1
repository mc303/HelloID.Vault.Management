#!/usr/bin/env pwsh
# Build script for HelloID.PostgreSQL wrapper
# Downloads Npgsql 8.0.8 and dependencies, builds the project

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$EmbeddedDir = Join-Path $ProjectDir "Embedded"
$NugetDir = Join-Path $ProjectDir "packages"

Write-Host "=== HelloID.PostgreSQL Build Script ===" -ForegroundColor Cyan
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
Write-Host "Step 1: Downloading Npgsql 8.0.8 and dependencies..." -ForegroundColor Yellow
Write-Host ""

# Npgsql 8.0.8 with netstandard2.0 dependencies
$packages = @(
    @{ Name = "Npgsql"; Version = "8.0.8"; SourcePath = "lib\netstandard2.0\Npgsql.dll" },
    @{ Name = "Microsoft.Extensions.Logging.Abstractions"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll" },
    @{ Name = "Microsoft.Extensions.DependencyInjection.Abstractions"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\Microsoft.Extensions.DependencyInjection.Abstractions.dll" },
    @{ Name = "Microsoft.Extensions.Options"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\Microsoft.Extensions.Options.dll" },
    @{ Name = "Microsoft.Extensions.Primitives"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\Microsoft.Extensions.Primitives.dll" },
    @{ Name = "System.Threading.Tasks.Extensions"; Version = "4.5.4"; SourcePath = "lib\netstandard2.0\System.Threading.Tasks.Extensions.dll" },
    @{ Name = "System.Runtime.CompilerServices.Unsafe"; Version = "6.0.0"; SourcePath = "lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" },
    @{ Name = "System.Memory"; Version = "4.5.5"; SourcePath = "lib\netstandard2.0\System.Memory.dll" },
    @{ Name = "Microsoft.Bcl.AsyncInterfaces"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\Microsoft.Bcl.AsyncInterfaces.dll" },
    @{ Name = "System.Text.Json"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\System.Text.Json.dll" },
    @{ Name = "System.Buffers"; Version = "4.5.1"; SourcePath = "lib\netstandard2.0\System.Buffers.dll" },
    @{ Name = "System.Numerics.Vectors"; Version = "4.5.0"; SourcePath = "lib\netstandard2.0\System.Numerics.Vectors.dll" },
    @{ Name = "System.Threading.Channels"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\System.Threading.Channels.dll" },
    @{ Name = "System.Diagnostics.DiagnosticSource"; Version = "8.0.0"; SourcePath = "lib\netstandard2.0\System.Diagnostics.DiagnosticSource.dll" }
)

foreach ($pkg in $packages) {
    Write-Host "  Downloading $($pkg.Name) v$($pkg.Version)..." -ForegroundColor Gray
    & $nugetExe install $pkg.Name -Version $pkg.Version -OutputDirectory $NugetDir -Source "https://api.nuget.org/v3/index.json" | Out-Null
}

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
    "Npgsql.dll",
    "Microsoft.Extensions.Logging.Abstractions.dll",
    "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "Microsoft.Extensions.Options.dll",
    "Microsoft.Extensions.Primitives.dll",
    "System.Threading.Tasks.Extensions.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Memory.dll",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "System.Text.Json.dll",
    "System.Buffers.dll",
    "System.Numerics.Vectors.dll",
    "System.Threading.Channels.dll",
    "System.Diagnostics.DiagnosticSource.dll"
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

& dotnet build (Join-Path $ProjectDir "HelloID.PostgreSQL.csproj") -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

$outputPath = Join-Path $ProjectDir "bin\$Configuration\HelloID.PostgreSQL.dll"

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $outputPath" -ForegroundColor Cyan
$outputInfo = Get-Item $outputPath
Write-Host "Size: $([math]::Round($outputInfo.Length / 1KB, 1)) KB" -ForegroundColor Cyan
Write-Host ""
Write-Host "Deploy to: C:\HelloID\SourceData\HelloID.PostgreSQL.dll" -ForegroundColor White
Write-Host ""
