#!/usr/bin/env pwsh
# Build script for HelloID.PostgreSQL wrapper using Npgsql 4.0.12
# This version has fewer dependencies and may be more compatible with some environments

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot
$EmbeddedDir = Join-Path $ProjectDir "Embedded"
$NugetDir = Join-Path $ProjectDir "packages"

Write-Host "=== HelloID.PostgreSQL Build Script (Npgsql 4.0.12) ===" -ForegroundColor Cyan
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
Write-Host "Step 1: Downloading Npgsql 4.0.12 and dependencies..." -ForegroundColor Yellow
Write-Host ""

# Npgsql 4.0.12 with net45 target (fewer dependencies)
$packages = @(
    @{ Name = "Npgsql"; Version = "4.0.12"; SourcePath = "lib\net45\Npgsql.dll" },
    @{ Name = "System.Threading.Tasks.Extensions"; Version = "4.5.4"; SourcePath = "lib\netstandard1.0\System.Threading.Tasks.Extensions.dll" },
    @{ Name = "System.Runtime.CompilerServices.Unsafe"; Version = "5.0.0"; SourcePath = "lib\net45\System.Runtime.CompilerServices.Unsafe.dll" },
    @{ Name = "System.Memory"; Version = "4.5.4"; SourcePath = "lib\netstandard1.1\System.Memory.dll" },
    @{ Name = "System.ValueTuple"; Version = "4.5.0"; SourcePath = "lib\net47\System.ValueTuple.dll" },
    @{ Name = "System.Diagnostics.DiagnosticSource"; Version = "4.7.1"; SourcePath = "lib\net46\System.Diagnostics.DiagnosticSource.dll" }
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
    "System.Threading.Tasks.Extensions.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Memory.dll",
    "System.ValueTuple.dll",
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
Write-Host "Step 4: Creating version-specific project file..." -ForegroundColor Yellow
Write-Host ""

# Create a temporary csproj for this build
$csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>HelloID.PostgreSQL</AssemblyName>
    <RootNamespace>HelloID.PostgreSQL</RootNamespace>
    <OutputType>Library</OutputType>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>bin\$Configuration\</OutputPath>
  </PropertyGroup>

  <PropertyGroup Condition="'`$(Configuration)|`$(Platform)'=='Release|AnyCPU'">
    <DebugType>none</DebugType>
    <Optimize>true</Optimize>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System.Data" />
    <Reference Include="System.Xml" />
    <Reference Include="System.Xml.Linq" />
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System.Core" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Embedded\Npgsql.dll" />
    <EmbeddedResource Include="Embedded\System.Threading.Tasks.Extensions.dll" />
    <EmbeddedResource Include="Embedded\System.Runtime.CompilerServices.Unsafe.dll" />
    <EmbeddedResource Include="Embedded\System.Memory.dll" />
    <EmbeddedResource Include="Embedded\System.ValueTuple.dll" />
    <EmbeddedResource Include="Embedded\System.Diagnostics.DiagnosticSource.dll" />
  </ItemGroup>
</Project>
"@

$tempCsproj = Join-Path $ProjectDir "HelloID.PostgreSQL.4.0.12.csproj"
$csprojContent | Out-File -FilePath $tempCsproj -Encoding UTF8
Write-Host "  Created: HelloID.PostgreSQL.4.0.12.csproj" -ForegroundColor Green

Write-Host ""
Write-Host "Step 5: Updating AssemblyLoader.cs for 4 DLLs..." -ForegroundColor Yellow
Write-Host ""

# Update AssemblyLoader.cs for 4 DLLs only
$assemblyLoaderPath = Join-Path $ProjectDir "AssemblyLoader.cs"
$assemblyLoaderContent = Get-Content $assemblyLoaderPath -Raw

$oldDlls = @'
        private static readonly string[] _expectedDlls = new[]
        {
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
        };
'@

$newDlls = @'
        private static readonly string[] _expectedDlls = new[]
        {
            "Npgsql.dll",
            "System.Threading.Tasks.Extensions.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Memory.dll",
            "System.ValueTuple.dll",
            "System.Diagnostics.DiagnosticSource.dll"
        };
'@

$assemblyLoaderContent = $assemblyLoaderContent.Replace($oldDlls, $newDlls)
$assemblyLoaderContent | Out-File -FilePath $assemblyLoaderPath -Encoding UTF8
Write-Host "  Updated AssemblyLoader.cs for 6 DLLs" -ForegroundColor Green

Write-Host ""
Write-Host "Step 6: Cleaning previous build..." -ForegroundColor Yellow
Write-Host ""

$binDir = Join-Path $ProjectDir "bin"
$objDir = Join-Path $ProjectDir "obj"
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
Write-Host "  Cleaned bin/ and obj/" -ForegroundColor Green

Write-Host ""
Write-Host "Step 7: Building project..." -ForegroundColor Yellow
Write-Host ""

& dotnet build $tempCsproj -c $Configuration

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
Write-Host "Npgsql Version: 4.0.12 (6 dependencies)" -ForegroundColor White
Write-Host ""
Write-Host "Deploy to: C:\HelloID\SourceData\HelloID.PostgreSQL.dll" -ForegroundColor White
Write-Host ""
