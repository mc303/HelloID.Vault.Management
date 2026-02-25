@echo off
REM Build script for HelloID.PostgreSQL wrapper
REM Downloads Npgsql 8.0.8 and dependencies, builds the project

setlocal enabledelayedexpansion

set PROJECT_DIR=%~dp0
set EMBEDDED_DIR=%PROJECT_DIR%Embedded
set NUGET_DIR=%PROJECT_DIR%packages

echo === HelloID.PostgreSQL Build Script ===
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
echo Step 1: Downloading Npgsql 8.0.8 and dependencies...
echo.

REM Download Npgsql 8.0.8 with all netstandard2.0 dependencies
"%PROJECT_DIR%nuget.exe" install Npgsql -Version 8.0.8 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install Microsoft.Extensions.Logging.Abstractions -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install Microsoft.Extensions.DependencyInjection.Abstractions -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install Microsoft.Extensions.Options -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install Microsoft.Extensions.Primitives -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Threading.Tasks.Extensions -Version 4.5.4 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Runtime.CompilerServices.Unsafe -Version 6.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Memory -Version 4.5.5 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install Microsoft.Bcl.AsyncInterfaces -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Text.Json -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Buffers -Version 4.5.1 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Numerics.Vectors -Version 4.5.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Threading.Channels -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json
"%PROJECT_DIR%nuget.exe" install System.Diagnostics.DiagnosticSource -Version 8.0.0 -OutputDirectory "%NUGET_DIR%" -Source https://api.nuget.org/v3/index.json

echo.
echo Step 2: Copying DLLs to Embedded folder...
echo.

REM Copy all DLLs (all netstandard2.0)
copy /Y "%NUGET_DIR%\Npgsql.8.0.8\lib\netstandard2.0\Npgsql.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Npgsql.dll && exit /b 1 )
echo   Copied: Npgsql.dll

copy /Y "%NUGET_DIR%\Microsoft.Extensions.Logging.Abstractions.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Microsoft.Extensions.Logging.Abstractions.dll && exit /b 1 )
echo   Copied: Microsoft.Extensions.Logging.Abstractions.dll

copy /Y "%NUGET_DIR%\Microsoft.Extensions.DependencyInjection.Abstractions.8.0.0\lib\netstandard2.0\Microsoft.Extensions.DependencyInjection.Abstractions.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Microsoft.Extensions.DependencyInjection.Abstractions.dll && exit /b 1 )
echo   Copied: Microsoft.Extensions.DependencyInjection.Abstractions.dll

copy /Y "%NUGET_DIR%\Microsoft.Extensions.Options.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Options.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Microsoft.Extensions.Options.dll && exit /b 1 )
echo   Copied: Microsoft.Extensions.Options.dll

copy /Y "%NUGET_DIR%\Microsoft.Extensions.Primitives.8.0.0\lib\netstandard2.0\Microsoft.Extensions.Primitives.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Microsoft.Extensions.Primitives.dll && exit /b 1 )
echo   Copied: Microsoft.Extensions.Primitives.dll

copy /Y "%NUGET_DIR%\System.Threading.Tasks.Extensions.4.5.4\lib\netstandard2.0\System.Threading.Tasks.Extensions.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Threading.Tasks.Extensions.dll && exit /b 1 )
echo   Copied: System.Threading.Tasks.Extensions.dll

copy /Y "%NUGET_DIR%\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Runtime.CompilerServices.Unsafe.dll && exit /b 1 )
echo   Copied: System.Runtime.CompilerServices.Unsafe.dll

copy /Y "%NUGET_DIR%\System.Memory.4.5.5\lib\netstandard2.0\System.Memory.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Memory.dll && exit /b 1 )
echo   Copied: System.Memory.dll

copy /Y "%NUGET_DIR%\Microsoft.Bcl.AsyncInterfaces.8.0.0\lib\netstandard2.0\Microsoft.Bcl.AsyncInterfaces.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy Microsoft.Bcl.AsyncInterfaces.dll && exit /b 1 )
echo   Copied: Microsoft.Bcl.AsyncInterfaces.dll

copy /Y "%NUGET_DIR%\System.Text.Json.8.0.0\lib\netstandard2.0\System.Text.Json.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Text.Json.dll && exit /b 1 )
echo   Copied: System.Text.Json.dll

copy /Y "%NUGET_DIR%\System.Buffers.4.5.1\lib\netstandard2.0\System.Buffers.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Buffers.dll && exit /b 1 )
echo   Copied: System.Buffers.dll

copy /Y "%NUGET_DIR%\System.Numerics.Vectors.4.5.0\lib\netstandard2.0\System.Numerics.Vectors.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Numerics.Vectors.dll && exit /b 1 )
echo   Copied: System.Numerics.Vectors.dll

copy /Y "%NUGET_DIR%\System.Threading.Channels.8.0.0\lib\netstandard2.0\System.Threading.Channels.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Threading.Channels.dll && exit /b 1 )
echo   Copied: System.Threading.Channels.dll

copy /Y "%NUGET_DIR%\System.Diagnostics.DiagnosticSource.8.0.0\lib\netstandard2.0\System.Diagnostics.DiagnosticSource.dll" "%EMBEDDED_DIR%\"
if errorlevel 1 ( echo ERROR: Failed to copy System.Diagnostics.DiagnosticSource.dll && exit /b 1 )
echo   Copied: System.Diagnostics.DiagnosticSource.dll

echo.
echo Step 3: Verifying Embedded DLLs...
echo.

for %%f in (Npgsql.dll Microsoft.Extensions.Logging.Abstractions.dll Microsoft.Extensions.DependencyInjection.Abstractions.dll Microsoft.Extensions.Options.dll Microsoft.Extensions.Primitives.dll System.Threading.Tasks.Extensions.dll System.Runtime.CompilerServices.Unsafe.dll System.Memory.dll Microsoft.Bcl.AsyncInterfaces.dll System.Text.Json.dll System.Buffers.dll System.Numerics.Vectors.dll System.Threading.Channels.dll System.Diagnostics.DiagnosticSource.dll) do (
    if exist "%EMBEDDED_DIR%\%%f" (
        echo   OK: %%f
    ) else (
        echo   MISSING: %%f
        exit /b 1
    )
)

echo.
echo Step 4: Cleaning previous build...
echo.

if exist "%PROJECT_DIR%bin" rmdir /s /q "%PROJECT_DIR%bin"
if exist "%PROJECT_DIR%obj" rmdir /s /q "%PROJECT_DIR%obj"
echo   Cleaned bin/ and obj/

echo.
echo Step 5: Building project...
echo.

dotnet build "%PROJECT_DIR%HelloID.PostgreSQL.csproj" -c Release

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
echo Deploy to: C:\HelloID\SourceData\HelloID.PostgreSQL.dll
echo.

endlocal
