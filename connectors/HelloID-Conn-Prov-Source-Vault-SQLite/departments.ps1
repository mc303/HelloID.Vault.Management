#####################################################
# HelloID-Conn-Prov-Source-Vault-SQLite-MicrosoftData-Departments
#
# Version: 1.0.0
# Description: HelloID source connector for Vault SQLite database
#              Uses Microsoft.Data.Sqlite via HelloID.SQLite wrapper DLL
#              Supports source filtering and field exclusion
#####################################################

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$databasePath = $c.databasePath
$wrapperDllPath = $c.wrapperDllPath
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
        [string]$WrapperDllPath
    )

    try {
        Write-Verbose "Executing SQLite query using HelloID.SQLite wrapper..."

        if (-not (Test-Path $WrapperDllPath)) {
            throw "HelloID.SQLite wrapper DLL not found at: $WrapperDllPath"
        }

        Add-Type -Path $WrapperDllPath -ErrorAction Stop
        Write-Verbose "Loaded HelloID.SQLite wrapper from: $WrapperDllPath"

        $connectionString = "Data Source=$DatabasePath"
        
        $dictParams = New-Object 'System.Collections.Generic.Dictionary[string,object]'
        foreach ($key in $Parameters.Keys) {
            $dictParams.Add($key, $Parameters[$key])
        }
        
        $dataTable = [HelloID.SQLite.Query]::Execute($connectionString, $Query, $dictParams)

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

    $departmentsTable = Invoke-SqliteQuery -DatabasePath $databasePath -Query $departmentsQuery -Parameters $queryParams -WrapperDllPath $wrapperDllPath
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
