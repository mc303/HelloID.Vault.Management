@echo off
REM Build script for HelloID.PostgreSQL wrapper using Npgsql 4.0.12
REM This version has fewer dependencies (4 DLLs vs 13)

setlocal enabledelayedexpansion

set PROJECT_DIR=%~dp0
set EMBEDDED_DIR=%PROJECT_DIR%Embedded
set NUGET_DIR=%PROJECT_DIR%packages

echo === HelloID.PostgreSQL Build Script (Npgsql 4.0.12) ===
echo.

REM Create directories
if not exist "%EMBEDDED_DIR%" mkdir "%EMBEDDED_DIR%"
if not exist "%NUGET_DIR%" mkdir "%NUGET_DIR%"

REM Download NuGet.exe if not present
if not exist "%PROJECT_DIR%nuget.exe" (
    echo Downloading NuGet.exe...
    powershell -Command "Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile '%PROJECT_DIR%nuget.exe'"
)

echo.
echo Step 1: Downloading Npgsql 4.0.12 and dependencies...
echo.

REM Download Npgsql 4.0.12 with net45 target (fewer dependencies)
"%PROJECT_DIR%nuget.exe" install Npgsql -Version 4.0.12 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Threading.Tasks.Extensions -Version 4.5.4 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Runtime.CompilerServices.Unsafe -Version 5.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Memory -Version 4.5.4 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.ValueTuple -Version 4.5.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Diagnostics.DiagnosticSource -Version 4.7.1 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json

echo.
echo Step 2: Copying DLLs to Embedded folder...
echo.

REM Copy all DLLs (net45 for Npgsql, netstandard1.x for others)
copy /Y "%NUGET_DIR%\Npgsql.4.0.12\lib\net45\Npgsql.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Npgsql.dll && exit /b 1 )
echo   Copied: Npgsql.dll

copy /Y "%NUGET_DIR%\System.Threading.Tasks.Extensions.4.5.4\lib\netstandard1.0\System.Threading.Tasks.Extensions.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Threading.Tasks.Extensions.dll && exit /b 1 )
echo   Copied: System.Threading.Tasks.Extensions.dll

copy /Y "%NUGET_DIR%\System.Runtime.CompilerServices.Unsafe.5.0.0\lib\net45\System.Runtime.CompilerServices.Unsafe.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Runtime.CompilerServices.Unsafe.dll && exit /b 1 )
echo   Copied: System.Runtime.CompilerServices.Unsafe.dll

copy /Y "%NUGET_DIR%\System.Memory.4.5.4\lib\netstandard1.1\System.Memory.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Memory.dll && exit /b 1 )
echo   Copied: System.Memory.dll

copy /Y "%NUGET_DIR%\System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.ValueTuple.dll && exit /b 1 )
echo   Copied: System.ValueTuple.dll

copy /Y "%NUGET_DIR%\System.Diagnostics.DiagnosticSource.4.7.1\lib\net46\System.Diagnostics.DiagnosticSource.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Diagnostics.DiagnosticSource.dll && exit /b 1 )
echo   Copied: System.Diagnostics.DiagnosticSource.dll

echo.
echo Step 3: Verifying Embedded DLLs...
echo.

for %%f in (Npgsql.dll System.Threading.Tasks.Extensions.dll System.Runtime.CompilerServices.Unsafe.dll System.Memory.dll System.ValueTuple.dll System.Diagnostics.DiagnosticSource.dll) do (
    if exist "%EMBEDDED_DIR%\%%f" (
        echo   OK: %%f
    ) else (
        echo   MISSING: %%f
        exit /b 1
    )
)

echo.
echo Step 4: Creating version-specific project file...
echo.

REM Create temporary csproj for 4.0.12 build
set TEMPCSPROJ=%PROJECT_DIR%HelloID.PostgreSQL.4.0.12.csproj

(
echo ^<Project Sdk="Microsoft.NET.Sdk"^>
echo   ^<PropertyGroup^>
echo     ^<TargetFramework^>net472^</TargetFramework^>
echo     ^<AssemblyName^>HelloID.PostgreSQL^</AssemblyName^>
echo     ^<RootNamespace^>HelloID.PostgreSQL^</RootNamespace^>
echo     ^<OutputType^>Library^</OutputType^>
echo     ^<GenerateAssemblyInfo^>true^</GenerateAssemblyInfo^>
echo     ^<AppendTargetFrameworkToOutputPath^>false^</AppendTargetFrameworkToOutputPath^>
echo     ^<OutputPath^>bin\Release\^</OutputPath^>
echo   ^</PropertyGroup^>
echo.
echo   ^<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|AnyCPU'"^>
echo     ^<DebugType^>none^</DebugType^>
echo     ^<Optimize^>true^</Optimize^>
echo   ^</PropertyGroup^>
echo.
echo   ^<ItemGroup^>
echo     ^<Reference Include="System.Data" /^>
echo     ^<Reference Include="System.Xml" /^>
echo     ^<Reference Include="System.Xml.Linq" /^>
echo     ^<Reference Include="Microsoft.CSharp" /^>
echo     ^<Reference Include="System.Core" /^>
echo   ^</ItemGroup^>
echo.
echo   ^<ItemGroup^>
echo     ^<EmbeddedResource Include="Embedded\Npgsql.dll" /^>
echo     ^<EmbeddedResource Include="Embedded\System.Threading.Tasks.Extensions.dll" /^>
echo     ^<EmbeddedResource Include="Embedded\System.Runtime.CompilerServices.Unsafe.dll" /^>
echo     ^<EmbeddedResource Include="Embedded\System.Memory.dll" /^>
echo     ^<EmbeddedResource Include="Embedded\System.ValueTuple.dll" /^>
echo     ^<EmbeddedResource Include="Embedded\System.Diagnostics.DiagnosticSource.dll" /^>
echo   ^</ItemGroup^>
echo ^</Project^>
) > "%TEMPCSPROJ%"

echo   Created: HelloID.PostgreSQL.4.0.12.csproj

echo.
echo Step 5: Updating AssemblyLoader.cs for 4 DLLs...
echo.

REM Update AssemblyLoader.cs for 4 DLLs only (using PowerShell for string replacement)
powershell -Command "$content = Get-Content '%PROJECT_DIR%AssemblyLoader.cs' -Raw; $old = @'
        private static readonly string[] _expectedDlls = new[]
        {
            \"Npgsql.dll\",
            \"Microsoft.Extensions.Logging.Abstractions.dll\",
            \"Microsoft.Extensions.DependencyInjection.Abstractions.dll\",
            \"Microsoft.Extensions.Options.dll\",
            \"Microsoft.Extensions.Primitives.dll\",
            \"System.Threading.Tasks.Extensions.dll\",
            \"System.Runtime.CompilerServices.Unsafe.dll\",
            \"System.Memory.dll\",
            \"Microsoft.Bcl.AsyncInterfaces.dll\",
            \"System.Text.Json.dll\",
            \"System.Buffers.dll\",
            \"System.Numerics.Vectors.dll\",
            \"System.Threading.Channels.dll\",
            \"System.Diagnostics.DiagnosticSource.dll\"
        };
'@; $new = @'
        private static readonly string[] _expectedDlls = new[]
        {
            \"Npgsql.dll\",
            \"System.Threading.Tasks.Extensions.dll\",
            \"System.Runtime.CompilerServices.Unsafe.dll\",
            \"System.Memory.dll\",
            \"System.ValueTuple.dll\",
            \"System.Diagnostics.DiagnosticSource.dll\"
        };
'@; $content.Replace($old, $new) ^| Set-Content '%PROJECT_DIR%AssemblyLoader.cs' -Encoding UTF8"

echo   Updated AssemblyLoader.cs for 6 DLLs

echo.
echo Step 6: Cleaning previous build...
echo.

if exist "%PROJECT_DIR%bin" rmdir /s /q "%PROJECT_DIR%bin"
if exist "%PROJECT_DIR%obj" rmdir /s /q "%PROJECT_DIR%obj"
echo   Cleaned bin/ and obj/

echo.
echo Step 7: Building project...
echo.

dotnet build "%TEMPCSPROJ%" -c Release

if errorlevel 1 (
    echo.
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo === Build Complete ===
echo.
echo Output: %PROJECT_DIR%bin\Release\HelloID.PostgreSQL.dll
echo.
echo Npgsql Version: 4.0.12 (6 dependencies)
echo.
echo Deploy to: C:\HelloID\SourceData\HelloID.PostgreSQL.dll
echo.

endlocal
