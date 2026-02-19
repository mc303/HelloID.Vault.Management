# Installation Guide: HelloID PostgreSQL Connector

This guide explains how to build and deploy the HelloID PostgreSQL connector wrapper DLL.

---

## Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         BUILD (Windows)                              │
│                                                                      │
│  src/HelloID.PostgreSQL/                                            │
│  ├── build.ps1  ──────────► Downloads Npgsql + builds wrapper      │
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

## Step 1: Build the Wrapper DLL

### Option A: Using PowerShell (Recommended)

1. Open PowerShell and navigate to the project:
   ```powershell
   cd C:\path\to\HelloID.Vault.Management\src\HelloID.PostgreSQL
   ```

2. Run the build script:
   ```powershell
   .\build.ps1
   ```

3. The script will:
   - Download NuGet.exe
   - Download Npgsql 8.0.8 and dependencies
   - Copy DLLs to `Embedded\` folder
   - Build the wrapper project

4. Verify the output:
   ```powershell
   dir .\bin\Release\HelloID.PostgreSQL.dll
   ```

### Option B: Using Command Prompt

```cmd
cd C:\path\to\HelloID.Vault.Management\src\HelloID.PostgreSQL
build.bat
```

### Option C: Manual Build

If the build scripts don't work, you can build manually:

1. **Download NuGet.exe**
   ```powershell
   Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "nuget.exe"
   ```

2. **Download Npgsql and dependencies**
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
   ```

4. **Build the project**
   ```cmd
   dotnet build -c Release
   ```

---

## Step 2: Create Folder Structure on HelloID Server

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

## Step 3: Copy Wrapper DLL

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

## Step 4: Verify Installation

On the HelloID server, test the wrapper:

```powershell
# Open PowerShell on HelloID server
Add-Type -Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"

# Test connection (replace with your connection string)
$connStr = "Host=localhost;Port=5432;Database=vault;Username=postgres;Password=yourpassword"
[Test-Connection]::TestConnection($connStr)
# Should return: True

# Test query
$table = [HelloID.PostgreSQL.Query]::Execute($connStr, "SELECT 1 as test")
$table.Rows[0]["test"]
# Should return: 1
```

If this works, the wrapper is installed correctly.

---

## Step 5: Configure HelloID Connector

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

Install .NET SDK from: https://dotnet.microsoft.com/download

### Build Error: "NuGet packages not found"

Check internet connection and try again. You can also manually download packages.

### Runtime Error: "Wrapper DLL not found"

Verify the path in configuration matches the actual file location:
```powershell
Test-Path "C:\HelloID\SourceData\HelloID.PostgreSQL.dll"
# Should return: True
```

### Runtime Error: "Unable to load assembly"

This should not happen with the wrapper. If it does:
1. Ensure .NET Framework 4.7.2+ is installed on HelloID server
2. Rebuild the wrapper and redeploy
3. Check Windows Event Viewer for details

### Test Connection Returns False

1. Check network connectivity to PostgreSQL server
2. Verify credentials are correct
3. Check firewall rules
4. Ensure PostgreSQL accepts connections from HelloID server

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
│  1. BUILD (Development Machine)                                 │
│     cd src\HelloID.PostgreSQL && .\build.ps1                    │
│                                                                 │
│  2. CREATE FOLDER (HelloID Server)                              │
│     New-Item -Path C:\HelloID\SourceData -ItemType Directory    │
│                                                                 │
│  3. COPY DLL                                                    │
│     copy bin\Release\HelloID.PostgreSQL.dll C:\HelloID\SourceData\ │
│                                                                 │
│  4. VERIFY                                                      │
│     Add-Type -Path C:\HelloID\SourceData\HelloID.PostgreSQL.dll │
│     [HelloID.PostgreSQL.Query]::TestConnection($connStr)        │
│                                                                 │
│  5. CONFIGURE                                                   │
│     Set Wrapper DLL Path in HelloID connector config            │
└─────────────────────────────────────────────────────────────────┘
```
