#####################################################
# HelloID-Conn-Prov-Source-Vault-SQLite-Departments
#
# Version: 1.2.0
# Description: HelloID source connector for Vault SQLite database
#              Uses System.Data.SQLite with flattened output structure
#              Supports source filtering and field exclusion
#              v1.2.0: Changed to flattened output structure
#              v1.1.0: Switched to System.Data.SQLite
#              v1.0.0: Initial release
#####################################################

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$databasePath = $c.databasePath
$dllPath = $c.sqliteDllPath
$excludedFields = if ($c.fieldsToExclude) { $c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() } } else { @() }
$sourceFilter = $c.sourceFilter
$isDebug = $c.isDebug -eq $true

if ($isDebug) {
    $VerbosePreference = 'Continue'
}

Write-Information "Starting Vault SQLite department import from: $databasePath"

#region functions
function Get-ErrorMessage {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipeline)]
        [object]$ErrorObject
    )
    process {
        $errorMessage = [PSCustomObject]@{
            VerboseErrorMessage = $null
            AuditErrorMessage   = $null
        }

        if ($ErrorObject.Exception) {
            $errorMessage.VerboseErrorMessage = $ErrorObject.Exception.Message
            $errorMessage.AuditErrorMessage = $ErrorObject.Exception.Message

            if ($ErrorObject.ScriptStackTrace) {
                $errorMessage.VerboseErrorMessage += "`nStack Trace: $($ErrorObject.ScriptStackTrace)"
            }
        }

        if ([String]::IsNullOrEmpty($errorMessage.VerboseErrorMessage)) {
            $errorMessage.VerboseErrorMessage = "Unknown error occurred"
            $errorMessage.AuditErrorMessage = "Unknown error occurred"
        }

        Write-Output $errorMessage
    }
}

function Invoke-SqliteQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabasePath,
        [Parameter(Mandatory = $true)]
        [string]$Query,
        [Parameter(Mandatory = $false)]
        [hashtable]$Parameters = @{},
        [Parameter(Mandatory = $false)]
        [string]$DllPath
    )

    try {
        Write-Verbose "Executing SQLite query using System.Data.SQLite..."

        $assemblyLoaded = $false
        $loadErrors = @()

        if (-not $assemblyLoaded -and -not [string]::IsNullOrWhiteSpace($DllPath)) {
            if (Test-Path $DllPath) {
                try {
                    Add-Type -Path $DllPath
                    $assemblyLoaded = $true
                    Write-Verbose "Loaded System.Data.SQLite from: $DllPath"
                }
                catch {
                    $loadErrors += "Configured path: $($_.Exception.Message)"
                }
            }
            else {
                $loadErrors += "Configured path not found: $DllPath"
            }
        }

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
                            Write-Verbose "Loaded System.Data.SQLite from NuGet: $($dllItem.FullName)"
                            break
                        }
                        catch {
                            $loadErrors += "NuGet $($dllItem.FullName): $($_.Exception.Message)"
                        }
                    }
                }
            }
        }

        if (-not $assemblyLoaded) {
            try {
                Add-Type -AssemblyName "System.Data.SQLite" -ErrorAction Stop
                $assemblyLoaded = $true
                Write-Verbose "Loaded System.Data.SQLite from GAC"
            }
            catch {
                $loadErrors += "GAC: $($_.Exception.Message)"
            }
        }

        if (-not $assemblyLoaded) {
            $errorMsg = @"
Failed to load System.Data.SQLite assembly.

INSTALLATION:
1. Download from: https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki
2. Extract and copy System.Data.SQLite.dll to your connector folder
3. Set 'SQLite DLL Path' configuration to the full path of System.Data.SQLite.dll

Errors encountered:
$($loadErrors -join "`n")
"@
            throw $errorMsg
        }

        $connection = New-Object System.Data.SQLite.SQLiteConnection
        $connection.ConnectionString = "Data Source=$DatabasePath;Version=3;"
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = $Query

        foreach ($key in $Parameters.Keys) {
            $command.Parameters.AddWithValue("@$key", $Parameters[$key]) | Out-Null
        }

        $adapter = New-Object System.Data.SQLite.SQLiteDataAdapter($command)
        $dataTable = New-Object System.Data.DataTable
        $adapter.Fill($dataTable) | Out-Null

        $adapter.Dispose()
        $command.Dispose()
        $connection.Close()
        $connection.Dispose()

        Write-Verbose "Query returned $($dataTable.Rows.Count) rows"
        return ,$dataTable
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "SQLite query error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "SQLite query error: $($errorMessage.AuditErrorMessage)"
    }
}
#endregion

try {
    Write-Information "Connecting to SQLite..."
    $departmentInProcess = @{ external_id = "(not started)" }

    $sourceFilterClause = ""
    $queryParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $sourceFilterClause = "AND d.source = @sourceFilter"
        $queryParams["sourceFilter"] = $sourceFilter
    }

    $departmentsQuery = @"
SELECT
    d.external_id,
    d.display_name,
    d.code,
    d.parent_external_id,
    d.manager_person_id,
    d.source,
    mgr.external_id as manager_external_id,
    mgr.display_name as manager_display_name
FROM departments d
LEFT JOIN persons mgr ON d.manager_person_id = mgr.person_id
WHERE d.external_id IS NOT NULL
$sourceFilterClause
ORDER BY d.display_name
"@

    $departmentsTable = Invoke-SqliteQuery -DatabasePath $databasePath -Query $departmentsQuery -Parameters $queryParams -DllPath $dllPath
    Write-Information "Found $($departmentsTable.Rows.Count) departments"

    Write-Information "Enhancing and exporting department objects to HelloID"
    $exportedDepartments = 0

    foreach ($deptRow in $departmentsTable.Rows) {
        $departmentInProcess = $deptRow
        $externalId = $deptRow["external_id"]

        Write-Verbose "Processing department: $externalId"

        $flatDepartment = @{
            DisplayName = $deptRow["display_name"]
            ExternalId = $externalId
            code = $deptRow["code"]
            parent_external_id = $deptRow["parent_external_id"]
            manager_person_id = $deptRow["manager_person_id"]
            ManagerExternalId = $deptRow["manager_external_id"]
            manager_display_name = $deptRow["manager_display_name"]
            source = $deptRow["source"]
        }

        foreach ($fieldToExclude in $excludedFields) {
            if ($flatDepartment.ContainsKey($fieldToExclude)) {
                [void]$flatDepartment.Remove($fieldToExclude)
            }
        }

        # Sort keys: DisplayName, ExternalId first, then alphabetically
        $orderedDept = [ordered]@{}
        foreach ($key in @('DisplayName', 'ExternalId')) {
            if ($flatDepartment.Contains($key)) {
                $orderedDept[$key] = $flatDepartment[$key]
            }
        }
        foreach ($key in ($flatDepartment.Keys | Sort-Object)) {
            if (-not $orderedDept.Contains($key)) {
                $orderedDept[$key] = $flatDepartment[$key]
            }
        }

        $departmentJson = $orderedDept | ConvertTo-Json -Depth 10
        Write-Output $departmentJson
        $exportedDepartments++
    }

    Write-Information "Successfully enhanced and exported department objects to HelloID. Result count: $exportedDepartments"
    Write-Information "Department import completed"
}
catch {
    $ex = $PSItem
    $errorMessage = Get-ErrorMessage -ErrorObject $ex

    if ($isDebug) {
        Write-Warning "Error occurred for department [$($departmentInProcess.external_id)]. Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    }

    throw "Could not enhance and export department objects to HelloID. Error Message: $($errorMessage.AuditErrorMessage)"
}
