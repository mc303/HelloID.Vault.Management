#####################################################
# Test-SQLite.ps1
# Simple test script to verify System.Data.SQLite works
#####################################################

param(
    [Parameter(Mandatory = $false)]
    [string]$DllPath,

    [Parameter(Mandatory = $false)]
    [string]$DatabasePath
)

$ErrorActionPreference = "Stop"

Write-Host "=== SQLite Connection Test ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Find and load the DLL
Write-Host "[1/4] Loading System.Data.SQLite..." -ForegroundColor Yellow

$assemblyLoaded = $false

# Option 1: Use provided path
if (-not $assemblyLoaded -and -not [string]::IsNullOrWhiteSpace($DllPath)) {
    if (Test-Path $DllPath) {
        try {
            Add-Type -Path $DllPath
            $assemblyLoaded = $true
            Write-Host "  Loaded from: $DllPath" -ForegroundColor Green
        }
        catch {
            Write-Host "  Failed to load from provided path: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Option 2: Auto-detect NuGet locations
if (-not $assemblyLoaded) {
    $nugetPatterns = @(
        "${env:USERPROFILE}\.nuget\packages\system.data.sqlite\*\lib\net6.0\System.Data.SQLite.dll",
        "${env:USERPROFILE}\.nuget\packages\system.data.sqlite\*\lib\net8.0\System.Data.SQLite.dll",
        "${env:USERPROFILE}\.nuget\packages\system.data.sqlite\*\lib\net46\System.Data.SQLite.dll",
        "${env:USERPROFILE}\.nuget\packages\system.data.sqlite\*\lib\netstandard2.0\System.Data.SQLite.dll"
    )

    foreach ($pathPattern in $nugetPatterns) {
        if (-not $assemblyLoaded) {
            $foundDlls = Get-Item $pathPattern -ErrorAction SilentlyContinue
            foreach ($dllItem in $foundDlls) {
                try {
                    Add-Type -Path $dllItem.FullName
                    $assemblyLoaded = $true
                    Write-Host "  Loaded from: $($dllItem.FullName)" -ForegroundColor Green
                    break
                }
                catch {
                    # Continue to next
                }
            }
        }
    }
}

if (-not $assemblyLoaded) {
    Write-Host "  ERROR: Could not load System.Data.SQLite" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install it with: dotnet add package System.Data.SQLite --version 2.0.2" -ForegroundColor White
    Write-Host "Or download from: https://system.data.sqlite.org" -ForegroundColor White
    exit 1
}

# Step 2: Show assembly info
Write-Host ""
Write-Host "[2/4] Assembly Info:" -ForegroundColor Yellow
try {
    $asm = [System.Data.SQLite.SQLiteConnection].Assembly
    Write-Host "  Version: $($asm.GetName().Version)" -ForegroundColor Green
    Write-Host "  Location: $($asm.Location)" -ForegroundColor Gray
}
catch {
    Write-Host "  Could not get assembly info" -ForegroundColor Yellow
}

# Step 3: Find database
Write-Host ""
Write-Host "[3/4] Locating Database..." -ForegroundColor Yellow

if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
    # Try to find database in common locations
    $searchPaths = @(
        "$PSScriptRoot\..\..\db\vault.db",
        "$PSScriptRoot\..\..\db\sqlite\vault.db",
        "$PSScriptRoot\vault.db",
        ".\vault.db",
        ".\db\vault.db"
    )

    foreach ($path in $searchPaths) {
        $fullPath = (Resolve-Path $path -ErrorAction SilentlyContinue).Path
        if ($fullPath -and (Test-Path $fullPath)) {
            $DatabasePath = $fullPath
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
    Write-Host "  No database path provided and could not auto-detect." -ForegroundColor Yellow
    Write-Host ""
    $DatabasePath = Read-Host "Enter database path (or press Enter to skip DB test)"
}

if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
    Write-Host ""
    Write-Host "=== TEST PASSED (DLL only) ===" -ForegroundColor Green
    Write-Host "System.Data.SQLite is working correctly!" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $DatabasePath)) {
    Write-Host "  ERROR: Database not found: $DatabasePath" -ForegroundColor Red
    exit 1
}

Write-Host "  Found: $DatabasePath" -ForegroundColor Green

# Step 4: Test connection and query
Write-Host ""
Write-Host "[4/4] Testing Connection..." -ForegroundColor Yellow

try {
    $connection = New-Object System.Data.SQLite.SQLiteConnection
    $connection.ConnectionString = "Data Source=$DatabasePath;Version=3;"
    $connection.Open()

    Write-Host "  Connection: OK" -ForegroundColor Green
    Write-Host "  SQLite Version: $($connection.ServerVersion)" -ForegroundColor Gray

    # Test query - check tables
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"

    $adapter = New-Object System.Data.SQLite.SQLiteDataAdapter($command)
    $dataTable = New-Object System.Data.DataTable
    $adapter.Fill($dataTable) | Out-Null

    Write-Host "  Tables found: $($dataTable.Rows.Count)" -ForegroundColor Green
    foreach ($row in $dataTable.Rows) {
        Write-Host "    - $($row['name'])" -ForegroundColor Gray
    }

    # Test count on persons table if exists
    $command.CommandText = "SELECT COUNT(*) as cnt FROM persons"
    try {
        $countResult = $command.ExecuteScalar()
        Write-Host "  Persons count: $countResult" -ForegroundColor Green
    }
    catch {
        Write-Host "  (persons table not found - this is OK for empty DB)" -ForegroundColor Yellow
    }

    $adapter.Dispose()
    $command.Dispose()
    $connection.Close()
    $connection.Dispose()

    Write-Host ""
    Write-Host "=== ALL TESTS PASSED ===" -ForegroundColor Green
    Write-Host "System.Data.SQLite is working correctly with your database!" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "=== TEST FAILED ===" -ForegroundColor Red
    exit 1
}
