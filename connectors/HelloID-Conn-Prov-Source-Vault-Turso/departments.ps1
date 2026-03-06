#####################################################
# HelloID-Conn-Prov-Source-Vault-Turso-Departments
#
# Version: 1.0.2
# Description: HelloID source connector for Vault Turso database
#              Uses Turso HTTP API for SQLite-compatible queries
#              Supports source filtering and field exclusion
#####################################################

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$databaseUrl = $c.databaseUrl
$authToken = $c.authToken
# Initialize excluded fields array (must be done explicitly to avoid null issues)
$excludedFields = @()
if ($c.fieldsToExclude -and -not [string]::IsNullOrWhiteSpace($c.fieldsToExclude)) {
    $excludedFields = @($c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() })
}
$sourceFilter = $c.sourceFilter
$isDebug = $c.isDebug -eq $true

if ($isDebug) {
    $VerbosePreference = 'Continue'
}

Write-Information "Starting Vault Turso department import from: $databaseUrl"

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

function ConvertTo-TursoType {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    if ($null -eq $Value) {
        return @{ type = "null"; value = $null }
    }

    if ($Value -is [bool]) {
        return @{ type = "integer"; value = if ($Value) { 1 } else { 0 } }
    }

    if ($Value -is [int] -or $Value -is [long]) {
        return @{ type = "integer"; value = [long]$Value }
    }

    if ($Value -is [double]) {
        return @{ type = "float"; value = [double]$Value }
    }

    if ($Value -is [DateTime]) {
        return @{ type = "text"; value = $Value.ToString("yyyy-MM-dd") }
    }

    # Default to text/string
    return @{ type = "text"; value = [string]$Value }
}

function Invoke-TursoQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$AuthToken,
        [Parameter(Mandatory = $true)]
        [string]$Query,
        [Parameter(Mandatory = $false)]
        [hashtable]$Parameters = @{}
    )

    try {
        Write-Verbose "Executing Turso query..."

        # Extract host from database URL (libsql://host or https://host)
        $hostUrl = $DatabaseUrl -replace '^libsql://', 'https://'

        # Build args array
        $argsArray = @()
        foreach ($key in $Parameters.Keys) {
            $typedValue = ConvertTo-TursoType -Value $Parameters[$key]
            $argsArray += $typedValue
        }

        $body = @{
            requests = @(
                @{
                    type = "execute"
                    stmt = @{
                        sql  = $Query
                        args = $argsArray
                    }
                }
            )
        } | ConvertTo-Json -Depth 10

        $headers = @{
            "Authorization" = "Bearer $AuthToken"
            "Content-Type"  = "application/json"
        }

        $splatParams = @{
            Method      = "POST"
            Uri         = "$hostUrl/v2/pipeline"
            Headers     = $headers
            Body        = $body
            ErrorAction = "Stop"
        }

        $response = Invoke-RestMethod @splatParams

        if ($response.results -and $response.results[0].response) {
            $result = $response.results[0].response.result

            if ($result.error) {
                throw "Turso query error: $($result.error.message)"
            }

            # Convert rows to PowerShell objects
            $rows = @()
            if ($result.rows) {
                $cols = $result.cols | ForEach-Object { $_.name }

                foreach ($row in $result.rows) {
                    $obj = [PSCustomObject]@{}
                    for ($i = 0; $i -lt $cols.Count; $i++) {
                        $colName = $cols[$i]
                        $colValue = $row[$i]

                        # Handle Turso value types
                        # Turso returns: @{type="text|integer|null"; value="..."}
                        if ($null -eq $colValue) {
                            $obj | Add-Member -NotePropertyName $colName -NotePropertyValue $null
                        }
                        elseif ($colValue.PSObject.Properties.Match('type').Count -gt 0 -and $colValue.PSObject.Properties.Match('value').Count -gt 0) {
                            # Turso typed response: @{type="..."; value="..."}
                            if ($colValue.type -eq "null") {
                                $obj | Add-Member -NotePropertyName $colName -NotePropertyValue $null
                            }
                            else {
                                $obj | Add-Member -NotePropertyName $colName -NotePropertyValue $colValue.value
                            }
                        }
                        elseif ($colValue -is [string] -or $colValue -is [long] -or $colValue -is [double] -or $colValue -is [int]) {
                            # Direct value (primitive types)
                            $obj | Add-Member -NotePropertyName $colName -NotePropertyValue $colValue
                        }
                        else {
                            # Any other type - convert to string
                            $obj | Add-Member -NotePropertyName $colName -NotePropertyValue ([string]$colValue)
                        }
                    }
                    $rows += $obj
                }
            }

            Write-Verbose "Query returned $($rows.Count) rows"
            return $rows
        }

        return @()
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "Turso query error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Turso query error: $($errorMessage.AuditErrorMessage)"
    }
}

function ConvertTo-FlatHashtable {
    param(
        [Parameter(Mandatory = $true)]
        [object]$PSCustomObject
    )

    $result = @{}
    foreach ($prop in $PSCustomObject.PSObject.Properties) {
        $result[$prop.Name] = $prop.Value
    }
    return $result
}
#endregion

try {
    Write-Information "Connecting to Turso..."
    $departmentInProcess = @{ external_id = "(not started)" }

    # Build source filter clause
    $sourceFilterClause = ""
    $queryParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $sourceFilterClause = "AND d.source = ?"
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

    $departments = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $departmentsQuery -Parameters $queryParams
    Write-Information "Found $($departments.Count) departments"

    Write-Information "Enhancing and exporting department objects to HelloID"
    $exportedDepartments = 0

    foreach ($dept in $departments) {
        $departmentInProcess = $dept
        $externalId = $dept.external_id

        Write-Verbose "Processing department: $externalId"

        $flatDepartment = @{
            DisplayName         = $dept.display_name
            ExternalId           = $externalId
            code                 = $dept.code
            parent_external_id   = $dept.parent_external_id
            manager_person_id    = $dept.manager_person_id
            ManagerExternalId    = $dept.manager_external_id
            manager_display_name = $dept.manager_display_name
            source               = $dept.source
        }

        # Remove excluded fields
        foreach ($fieldToExclude in $excludedFields) {
            if ($flatDepartment.ContainsKey($fieldToExclude)) {
                [void]$flatDepartment.Remove($fieldToExclude)
            }
        }

        # Order with DisplayName and ExternalId first
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
