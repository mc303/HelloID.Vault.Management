#####################################################
# HelloID-Conn-Prov-Source-Vault-Postgres-Npgsql-Departments
#
# Version: 0.6.0
# Description: HelloID source connector for Vault PostgreSQL database
#              Uses HelloID.PostgreSQL wrapper for PowerShell 5.1 compatibility
#              Uses flattened output structure with source filtering
#              v0.6.0: Uses HelloID.PostgreSQL wrapper DLL
#####################################################

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$sqlhost = $c.host
$port = $c.port
$database = $c.database
$username = $c.username
$password = $c.password
$wrapperDllPath = $c.wrapperDllPath
$excludedFields = if ($c.fieldsToExclude) { $c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() } } else { @() }
$sourceFilter = $c.sourceFilter
$isDebug = $c.isDebug -eq $true

if ($isDebug) {
    $VerbosePreference = 'Continue'
}

Write-Information "Starting Vault PostgreSQL department import from: $sqlhost`:$port/$database"

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

function Invoke-PostgresQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,
        [Parameter(Mandatory = $true)]
        [string]$Query,
        [Parameter(Mandatory = $false)]
        [hashtable]$Parameters = @{}
    )

    try {
        Write-Verbose "Executing PostgreSQL query..."
        
        $result = [HelloID.PostgreSQL.Query]::Execute($ConnectionString, $Query, $Parameters)
        
        Write-Verbose "Query returned $($result.Rows.Count) rows"
        return ,$result
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "PostgreSQL query error: $($errorMessage.VerboseErrorMessage)"
        throw "PostgreSQL query error: $($errorMessage.AuditErrorMessage)"
    }
}

function Sort-HashtableKeys {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $false)]
        [string[]]$FrontFields = @('DisplayName', 'ExternalId')
    )

    $ordered = [ordered]@{}
    
    foreach ($fieldName in $FrontFields) {
        if ($Hashtable.Contains($fieldName)) {
            $ordered[$fieldName] = $Hashtable[$fieldName]
        }
    }
    
    $remainingKeys = $Hashtable.Keys | Where-Object { -not $ordered.Contains($_) } | Sort-Object
    foreach ($key in $remainingKeys) {
        $ordered[$key] = $Hashtable[$key]
    }
    
    return $ordered
}
#endregion

try {
    if ([string]::IsNullOrWhiteSpace($wrapperDllPath)) {
        throw "Wrapper DLL Path is required. Please configure the path to HelloID.PostgreSQL.dll"
    }
    
    if (-not (Test-Path $wrapperDllPath)) {
        throw "Wrapper DLL not found at: $wrapperDllPath"
    }
    
    Write-Information "Loading HelloID.PostgreSQL wrapper from: $wrapperDllPath"
    Add-Type -Path $wrapperDllPath
    
    $connectionString = "Host=$sqlhost;Port=$port;Database=$database;Username=$username;Password=$password;SSL Mode=Prefer;Trust Server Certificate=true"
    
    Write-Information "Connecting to PostgreSQL..."
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
    mgr.display_name as manager_display_name,
    mgr.external_id as manager_external_id
FROM public.departments d
LEFT JOIN public.persons mgr ON d.manager_person_id = mgr.person_id
WHERE d.external_id IS NOT NULL
$sourceFilterClause
ORDER BY d.display_name
"@
    
    $departmentsTable = Invoke-PostgresQuery -ConnectionString $connectionString -Query $departmentsQuery -Parameters $queryParams
    Write-Information "Found $($departmentsTable.Rows.Count) departments"
    
    Write-Information "Enhancing and exporting department objects to HelloID"
    $exportedDepartments = 0
    
    foreach ($deptRow in $departmentsTable.Rows) {
        if ($null -eq $deptRow) { continue }
        $departmentInProcess = $deptRow
        $externalId = $deptRow["external_id"]
        
        Write-Verbose "Processing department: $externalId"
        
        $flatDepartment = @{
            DisplayName = $deptRow["display_name"]
            ExternalId = $externalId
            code = $deptRow["code"]
            parent_external_id = $deptRow["parent_external_id"]
            manager_external_id = $deptRow["manager_external_id"]
            manager_display_name = $deptRow["manager_display_name"]
            source = $deptRow["source"]
        }
        
        foreach ($fieldToExclude in $excludedFields) {
            if ($flatDepartment.Contains($fieldToExclude)) {
                [void]$flatDepartment.Remove($fieldToExclude)
            }
        }
        
        $flatDepartment = Sort-HashtableKeys -Hashtable $flatDepartment -FrontFields @('DisplayName', 'ExternalId')
        
        $departmentJson = $flatDepartment | ConvertTo-Json -Depth 10
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
