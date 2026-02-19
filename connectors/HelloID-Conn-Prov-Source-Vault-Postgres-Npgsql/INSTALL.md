# Installation Guide: HelloID PostgreSQL Connector

This guide explains how to build and deploy the HelloID PostgreSQL connector wrapper DLL.

---

## Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         BUILD (Windows)                              │
│                                                                      │
│  src/HelloID.PostgreSQL/                                            │
│  ├── build.ps1 ─────────► Build with Npgsql 8.0.8 (default)        │
│  ├── build-4.0.12.ps1 ───► Build with Npgsql 4.0.12 (alternative)  │
│  └── bin/Release/                                                   │
│      └── HelloID.PostgreSQL.dll  ◄── Single file to deploy         │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      DEPLOY (HelloID Server)                         │
│                                                                      │
│  C:\HelloID\SourceData\                                              │
│  └── HelloID.PostgreSQL.dll  ◄── Copy here                          │
│                                                                      │
│  Configure connector:                                                │
│  Wrapper DLL Path = C:\HelloID\SourceData\HelloID.PostgreSQL.dll   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

### For Building (Development Machine)

| Requirement | Notes |
|-------------|-------|
| Windows OS | Required for .NET Framework build |
| .NET SDK 8.0+ | For building the project |
| Internet access | To download Npgsql from NuGet |
| PowerShell 5.1+ | To run build script |

### For Running (HelloID Server)

| Requirement | Notes |
|-------------|-------|
| Windows Server | HelloID agent runs here |
| .NET Framework 4.7.2+ | Included in Windows Server 2019+ |
| Network access | To PostgreSQL database |

---

## Step 1: Choose Npgsql Version

Before building, decide which Npgsql version to use:

### Npgsql 8.0.8 (Recommended)

- **14 embedded DLLs**
- Full feature support
- Best for most environments

**Use when:**
- Standard Windows Server environments
- No GAC assembly conflicts
- Need latest Npgsql features

**Build command:** `.\build.ps1` or `build.bat`

### Npgsql 4.0.12 (Alternative)

- **6 embedded DLLs**
- Fewer dependencies
- Best for constrained environments

**Use when:**
- Server has Microsoft.Extensions.* assemblies in GAC
- Assembly version conflicts with 8.0.8
- Stricter security policies limiting loaded assemblies
- Need to minimize memory footprint

**Build command:** `.\build-4.0.12.ps1` or `build-4.0.12.bat`

---

## Step 2: Build the Wrapper DLL

### Option A: Using PowerShell (Recommended)

1. Open PowerShell and navigate to the project:
   ```powershell
   cd C:\path\to\connector\src\HelloID.PostgreSQL
   ```

2. Run the appropriate build script:
   ```powershell
   # For Npgsql 8.0.8 (default, recommended)
   .\build.ps1
   
   # OR for Npgsql 4.0.12 (alternative)
   .\build-4.0.12.ps1
   ```

3. The script will:
   - Download NuGet.exe (if not present)
   - Download Npgsql and all dependencies
   - Copy DLLs to `Embedded\` folder
   - Build the wrapper project

4. Verify the output:
   ```powershell
   dir .\bin\Release\HelloID.PostgreSQL.dll
   
   # Check file size (should be 2-4 MB depending on version)
   Get-Item .\bin\Release\HelloID.PostgreSQL.dll | Select-Object Name, Length
   ```

### Option B: Using Command Prompt (Batch Files)

```cmd
cd C:\path\to\connector\src\HelloID.PostgreSQL

# For Npgsql 8.0.8
build.bat

# OR for Npgsql 4.0.12
build-4.0.12.bat
```

### Option C: Manual Build

If the build scripts don't work, you can build manually:

#### For Npgsql 8.0.8

1. **Download NuGet.exe**
   ```powershell
   Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "nuget.exe"
   ```

2. **Download Npgsql 8.0.8 and all dependencies** (14 packages)
   ```cmd
   nuget.exe install Npgsql -Version 8.0.8 -OutputDirectory packages
   nuget.exe install Microsoft.Extensions.Logging.Abstractions -Version 8.0.0 -OutputDirectory packages
   nuget.exe install Microsoft.Extensions.DependencyInjection.Abstractions -Version 8.0.0 -OutputDirectory packages
   nuget.exe install Microsoft.Extensions.Options -Version 8.0.0 -OutputDirectory packages
   nuget.exe install Microsoft.Extensions.Primitives -Version 8.0.0 -OutputDirectory packages
   nuget.exe install System.Threading.Tasks.Extensions -Version 4.5.4 -OutputDirectory packages
   nuget.exe install System.Runtime.CompilerServices.Unsafe -Version 6.0.0 -OutputDirectory packages
   nuget.exe install System.Memory -Version 4.5.5 -OutputDirectory packages
   nuget.exe install Microsoft.Bcl.AsyncInterfaces -Version 8.0.0 -OutputDirectory packages
   nuget.exe install System.Text.Json -Version 8.0.0 -OutputDirectory packages
   nuget.exe install System.Buffers -Version 4.5.1 -OutputDirectory packages
   nuget.exe install System.Numerics.Vectors -Version 4.5.0 -OutputDirectory packages
   nuget.exe install System.Threading.Channels -Version 8.0.0 -OutputDirectory packages
   nuget.exe install System.Diagnostics.DiagnosticSource -Version 8.0.0 -OutputDirectory packages
   ```

3. **Copy DLLs to Embedded folder**
   ```cmd
   mkdir Embedded
   copy packages\Npgsql.8.0.8\lib\netstandard2.0\Npgsql.dll Embedded\
   copy packages\Microsoft.Extensions.Logging.Abstractions.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll Embedded\
   copy packages\Microsoft.Extensions.DependencyInjection.Abstractions.8.0.0\lib\netstandard2.0\Microsoft.Extensions.DependencyInjection.Abstractions.dll Embedded\
   copy packages\Microsoft.Extensions.Options.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Options.dll Embedded\
   copy packages\Microsoft.Extensions.Primitives.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Primitives.dll Embedded\
   copy packages\System.Threading.Tasks.Extensions.4.5.4\lib\netstandard2.0\System.Threading.Tasks.Extensions.dll Embedded\
   copy packages\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll Embedded\
   copy packages\System.Memory.4.5.5\lib\netstandard2.0\System.Memory.dll Embedded\
   copy packages\Microsoft.Bcl.AsyncInterfaces.8.0.0\lib\netstandard2.0\Microsoft.Bcl.AsyncInterfaces.dll Embedded\
   copy packages\System.Text.Json.8.0.0\lib\netstandard2.0\System.Text.Json.dll Embedded\
   copy packages\System.Buffers.4.5.1\lib\netstandard2.0\System.Buffers.dll Embedded\
   copy packages\System.Numerics.Vectors.4.5.0\lib\netstandard2.0\System.Numerics.Vectors.dll Embedded\
   copy packages\System.Threading.Channels.8.0.0\lib\netstandard2.0\System.Threading.Channels.dll Embedded\
   copy packages\System.Diagnostics.DiagnosticSource.8.0.0\lib\netstandard2.0\System.Diagnostics.DiagnosticSource.dll Embedded\
   ```

4. **Build the project**
   ```cmd
   dotnet build -c Release
   ```

#### For Npgsql 4.0.12

1. **Download NuGet.exe**
   ```powershell
   Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "nuget.exe"
   ```

2. **Download Npgsql 4.0.12 and dependencies** (6 packages)
   ```cmd
   nuget.exe install Npgsql -Version 4.0.12 -OutputDirectory packages
   nuget.exe install System.Threading.Tasks.Extensions -Version 4.5.4 -OutputDirectory packages
   nuget.exe install System.Runtime.CompilerServices.Unsafe -Version 6.0.0 -OutputDirectory packages
   nuget.exe install System.Memory -Version 4.5.5 -OutputDirectory packages
   nuget.exe install System.ValueTuple -Version 4.5.0 -OutputDirectory packages
   nuget.exe install System.Diagnostics.DiagnosticSource -Version 4.7.1 -OutputDirectory packages
   ```

3. **Copy DLLs to Embedded folder**
   ```cmd
   mkdir Embedded
   copy packages\Npgsql.4.0.12\lib\netstandard2.0\Npgsql.dll Embedded\
   copy packages\System.Threading.Tasks.Extensions.4.5.4\lib\netstandard2.0\System.Threading.Tasks.Extensions.dll Embedded\
   copy packages\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll Embedded\
   copy packages\System.Memory.4.5.5\lib\netstandard2.0\System.Memory.dll Embedded\
   copy packages\System.ValueTuple.4.5.0\lib\netstandard2.0\System.ValueTuple.dll Embedded\
   copy packages\System.Diagnostics.DiagnosticSource.4.7.1\lib\netstandard2.0\System.Diagnostics.DiagnosticSource.dll Embedded\
   ```

4. **Build the project**
   ```cmd
   dotnet build -c Release
   ```

---

## Step 3: Create Folder Structure on HelloID Server

On your HelloID agent server, create the folder structure:

```powershell
# Run on HelloID server
New-Item -Path "C:\HelloID\SourceData" -ItemType Directory -Force
```

**Expected folder structure:**
```
C:\HelloID\
├── Agent\
│   └── ... (HelloID agent files)
└── SourceData\
    └── HelloID.PostgreSQL.dll   <-- Copy the wrapper here
```

---

## Step 4: Copy Wrapper DLL

Copy the built DLL to the HelloID server:

### Option A: Direct Copy (if building on same machine)
```powershell
copy .\bin\Release\HelloID.PostgreSQL.dll C:\HelloID\SourceData\
```

### Option B: Remote Copy (if building on different machine)
```powershell
# From build machine
Copy-Item .\bin\Release\HelloID.PostgreSQL.dll \\HelloID-Server\C$\HelloID\SourceData\
```

### Option C: Manual Copy
1. Locate `bin\Release\HelloID.PostgreSQL.dll` on build machine
2. Copy to `C:\HelloID\SourceData\` on HelloID server

---

## Step 5: Verify Installation

On the HelloID server, test the wrapper:

### Basic Verification

```powershell
# Open PowerShell on HelloID server

# 1. Load the wrapper
Add-Type -Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"

# 2. Verify the class loaded
[HelloID.PostgreSQL.Query]
# Should display type information
```

### Connection Test

```powershell
# Build connection string (replace with your details)
$connStr = "Host=your-server.com;Port=5432;Database=vault;Username=postgres;Password=yourpassword;SSL Mode=Prefer;Trust Server Certificate=true"

# Test connection
[HelloID.PostgreSQL.Query]::TestConnection($connStr)
# Should return: True
```

### Query Test

```powershell
# Test a simple query
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, "SELECT 1 as test, 'Hello' as message")
$table.Rows[0]["test"]      # Should return: 1
$table.Rows[0]["message"]   # Should return: Hello

# Test with persons table
$count = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, "SELECT COUNT(*) FROM persons")
Write-Host "Found $count persons in database"
```

### If Tests Fail

See the Troubleshooting section below.

---

## Step 6: Configure HelloID Connector

In HelloID, configure the connector with these settings:

| Setting | Value |
|---------|-------|
| **Wrapper DLL Path** | `C:\HelloID\SourceData\HelloID.PostgreSQL.dll` |
| **Host** | Your PostgreSQL hostname |
| **Port** | 5432 (or your port) |
| **Database** | Your database name |
| **Username** | Database username |
| **Password** | Database password |

### Example Configuration

```
Wrapper DLL Path:  C:\HelloID\SourceData\HelloID.PostgreSQL.dll
Host:              pg-25fe80a8-helloid-vault.c.aivencloud.com
Port:              24392
Database:          defaultdb
Username:          avnadmin
Password:          [your-password]
```

---

## Troubleshooting

### Build Error: "dotnet not found"

**Cause:** .NET SDK not installed.

**Solution:**
1. Download from: https://dotnet.microsoft.com/download
2. Install .NET SDK 8.0 or later
3. Restart PowerShell and try again

### Build Error: "NuGet packages not found"

**Cause:** Internet connectivity or NuGet source issues.

**Solution:**
1. Check internet connection
2. Try manual download (see Option C above)
3. Check corporate proxy settings if applicable

### Build Error: "Could not find embedded resource"

**Cause:** DLLs not copied to Embedded folder before build.

**Solution:**
1. Ensure `Embedded\` folder exists
2. Run the build script again
3. Or manually copy DLLs as shown in Option C

### Runtime Error: "Wrapper DLL not found"

**Cause:** Incorrect path in configuration.

**Solution:**
```powershell
# Verify the path matches exactly
Test-Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"
# Should return: True

# Check for typos, extra spaces, or wrong drive letter
```

### Runtime Error: "Unable to load assembly"

**Cause:** Assembly conflict or wrong Npgsql version for your environment.

**Solution:**
1. If using Npgsql 8.0.8, try rebuilding with 4.0.12:
   ```powershell
   .\build-4.0.12.ps1
   copy .\bin\Release\HelloID.PostgreSQL.dll C:\HelloID\SourceData\
   ```
2. Verify .NET Framework 4.7.2+ is installed:
   ```powershell
   Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' | 
     Get-ItemProperty -Name Release | 
     Where-Object { $_.Release -ge 461808 }
   ```

### Runtime Error: "Could not load file or assembly 'Microsoft.Extensions.*'"

**Cause:** GAC conflict with pre-installed Microsoft.Extensions.* assemblies.

**Solution:** Use Npgsql 4.0.12 build which doesn't include Microsoft.Extensions.*:
```powershell
.\build-4.0.12.ps1
copy .\bin\Release\HelloID.PostgreSQL.dll C:\HelloID\SourceData\
```

### Test Connection Returns False

**Cause:** Network, credentials, or firewall issues.

**Solutions:**
1. Check network connectivity:
   ```powershell
   Test-NetConnection -ComputerName "your-server.com" -Port 5432
   ```
2. Verify credentials are correct
3. Check firewall allows outbound connections
4. Ensure PostgreSQL accepts connections from HelloID server IP
5. For Aiven: Verify port from console (not standard 5432)

### Query Test Returns No Data

**Cause:** Table is empty or query is wrong.

**Solutions:**
```powershell
# Check if table exists
$tables = [HelloID.PostgreSQL.Query]::Execute($connStr, @"
    SELECT table_name FROM information_schema.tables 
    WHERE table_schema = 'public'
"@)
$tables.Rows | ForEach-Object { $_["table_name"] }

# Check row count
$count = [HelloID.PostgreSQL.Query]::ExecuteScalar($connStr, "SELECT COUNT(*) FROM persons")
```

### Temp Folder Permission Denied

**Cause:** Embedded DLLs cannot be extracted to temp folder.

**Solution:**
```powershell
# Check temp folder
dir $env:TEMP\HelloID.PostgreSQL\

# If needed, clear temp and retry
Remove-Item -Recurse -Force "$env:TEMP\HelloID.PostgreSQL"
# Then run your connector again
```

### PowerShell Session Becomes Unstable

**Cause:** Assembly loading issues in current session.

**Solution:**
1. Close PowerShell completely
2. Open a new PowerShell session
3. Reload the wrapper DLL

---

## Which Npgsql Version Am I Using?

To check which version is embedded in your DLL:

```powershell
# Load the DLL
Add-Type -Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"

# Check embedded resources (via temp folder)
$tempPath = "$env:TEMP\HelloID.PostgreSQL"
if (Test-Path $tempPath) {
    Get-ChildItem $tempPath -Filter "*.dll" | Select-Object Name
} else {
    Write-Host "Run a query first to extract DLLs to temp folder"
}
```

**Npgsql 8.0.8 has 14 DLLs:**
- Npgsql.dll
- Microsoft.Extensions.Logging.Abstractions.dll
- Microsoft.Extensions.DependencyInjection.Abstractions.dll
- Microsoft.Extensions.Options.dll
- Microsoft.Extensions.Primitives.dll
- System.Threading.Tasks.Extensions.dll
- System.Runtime.CompilerServices.Unsafe.dll
- System.Memory.dll
- Microsoft.Bcl.AsyncInterfaces.dll
- System.Text.Json.dll
- System.Buffers.dll
- System.Numerics.Vectors.dll
- System.Threading.Channels.dll
- System.Diagnostics.DiagnosticSource.dll

**Npgsql 4.0.12 has 6 DLLs:**
- Npgsql.dll
- System.Threading.Tasks.Extensions.dll
- System.Runtime.CompilerServices.Unsafe.dll
- System.Memory.dll
- System.ValueTuple.dll
- System.Diagnostics.DiagnosticSource.dll

---

## File Reference

### Files to Deploy

| File | Location | Purpose |
|------|----------|---------|
| `HelloID.PostgreSQL.dll` | `C:\HelloID\SourceData\` | Wrapper DLL |
| `persons.ps1` | Connector folder | Person import script |
| `departments.ps1` | Connector folder | Department import script |
| `configuration.json` | Connector folder | Configuration schema |

### Connector Files (in HelloID)

The connector scripts (`persons.ps1`, `departments.ps1`, `configuration.json`) are uploaded to HelloID through the admin interface - they are not file-based.

---

## Summary

```
┌─────────────────────────────────────────────────────────────────┐
│  1. CHOOSE VERSION                                               │
│     8.0.8 (default) or 4.0.12 (for constrained environments)    │
│                                                                 │
│  2. BUILD (Development Machine)                                 │
│     cd src\HelloID.PostgreSQL                                   │
│     .\build.ps1         (for 8.0.8)                             │
│     .\build-4.0.12.ps1  (for 4.0.12)                            │
│                                                                 │
│  3. CREATE FOLDER (HelloID Server)                              │
│     New-Item -Path C:\HelloID\SourceData -ItemType Directory    │
│                                                                 │
│  4. COPY DLL                                                    │
│     copy bin\Release\HelloID.PostgreSQL.dll C:\HelloID\SourceData\ │
│                                                                 │
│  5. VERIFY                                                      │
│     Add-Type -Path C:\HelloID\SourceData\HelloID.PostgreSQL.dll │
│     [HelloID.PostgreSQL.Query]::TestConnection($connStr)        │
│                                                                 │
│  6. CONFIGURE                                                   │
│     Set Wrapper DLL Path in HelloID connector config            │
└─────────────────────────────────────────────────────────────────┘
```
