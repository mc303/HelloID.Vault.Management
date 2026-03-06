#####################################################
# HelloID-Conn-Prov-Source-Vault-Turso-Persons
# PowerShell V2
# Version: 1.0.7
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
$includeInactiveContracts = $c.includeInactiveContracts -eq $true
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

Write-Information "Starting Vault Turso person import from: $databaseUrl"

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

function ConvertFrom-TursoValue {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [object]$Value
    )

    # Handle null
    if ($null -eq $Value) {
        return $null
    }

    # Check if it's a Turso typed response: @{type="..."; value="..."}
    if ($Value.PSObject.Properties.Match('type').Count -gt 0) {
        $type = $Value.type

        # Handle null FIRST (Turso returns {"type":"null"} without value property)
        if ($type -eq "null") {
            return $null
        }

        # For other types, extract value
        $val = $Value.value

        # Convert based on Turso type (value is always string in JSON)
        switch ($type) {
            "integer" {
                if ($null -eq $val -or $val -eq "") { return $null }
                return [long]$val
            }
            "float" {
                if ($null -eq $val -or $val -eq "") { return $null }
                return [double]$val
            }
            "text" { return $val }
            default { return $val }
        }
    }

    # Direct value (string, int, etc.)
    return $Value
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
                        $colValue = ConvertFrom-TursoValue -Value $row[$i]
                        $obj | Add-Member -NotePropertyName $colName -NotePropertyValue $colValue
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

function Get-DateForHelloID {
    param(
        [Parameter(Mandatory = $false)]
        [object]$DateValue
    )

    if ($null -eq $DateValue -or [string]::IsNullOrWhiteSpace($DateValue)) {
        return $null
    }

    try {
        $date = [DateTime]::Parse($DateValue)
        return $date.ToString("yyyy-MM-dd")
    }
    catch {
        return $null
    }
}

function ConvertTo-FlatHashtable {
    param(
        [Parameter(Mandatory = $true)]
        [object]$PSCustomObject
    )

    $result = @{}
    foreach ($prop in $PSCustomObject.PSObject.Properties) {
        # Convert Turso typed values to plain values
        $result[$prop.Name] = ConvertFrom-TursoValue -Value $prop.Value
    }

    return $result
}

function Expand-JsonFields {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Hashtable,
        [Parameter(Mandatory = $true)]
        [string[]]$JsonFields
    )

    foreach ($fieldName in $JsonFields) {
        if ($Hashtable.Contains($fieldName)) {
            $jsonValue = $Hashtable[$fieldName]
            if (-not [string]::IsNullOrWhiteSpace($jsonValue)) {
                try {
                    $parsed = $jsonValue | ConvertFrom-Json
                    foreach ($prop in $parsed.PSObject.Properties) {
                        $Hashtable["custom_$($prop.Name)"] = $prop.Value
                    }
                }
                catch { }
            }
            [void]$Hashtable.Remove($fieldName)
        }
    }
    return $Hashtable
}

function Format-DateFields {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $true)]
        [string[]]$DateFields
    )

    foreach ($fieldName in $DateFields) {
        if ($Hashtable.Contains($fieldName)) {
            $Hashtable[$fieldName] = Get-DateForHelloID $Hashtable[$fieldName]
        }
    }
    return $Hashtable
}

function Rename-HelloIDFields {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $false)]
        $RenameMap = @{
            'display_name' = 'DisplayName'
            'external_id'  = 'ExternalId'
        }
    )

    foreach ($oldName in $RenameMap.Keys) {
        if ($Hashtable.Contains($oldName)) {
            $Hashtable[$RenameMap[$oldName]] = $Hashtable[$oldName]
            [void]$Hashtable.Remove($oldName)
        }
    }
    return $Hashtable
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
    Write-Information "Connecting to Turso..."
    $personInProcess = @{ external_id = "(not started)" }

    # Build source filter clause
    $sourceFilterClause = ""
    $queryParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $sourceFilterClause = "AND p.source = ?"
        $queryParams["sourceFilter"] = $sourceFilter
    }

    # Query persons from person_details_view
    $personsQuery = @"
SELECT
    p.person_id,
    p.external_id,
    p.display_name,
    p.given_name,
    p.family_name,
    p.user_name,
    p.primary_manager_person_id,
    p.gender,
    p.birth_date,
    p.birth_locality,
    p.marital_status,
    p.initials,
    p.nick_name,
    p.family_name_prefix,
    p.family_name_partner,
    p.family_name_partner_prefix,
    p.convention,
    p.honorific_prefix,
    p.honorific_suffix,
    p.blocked,
    p.status_reason,
    p.excluded,
    p.custom_fields,
    p.source
FROM persons p
WHERE p.external_id IS NOT NULL AND p.external_id != ''
$sourceFilterClause
ORDER BY p.display_name
"@

    $persons = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $personsQuery -Parameters $queryParams
    Write-Information "Found $($persons.Count) persons"

    # Build contracts lookup
    $contractsByPerson = @{}

    $contractsQuery = @"
SELECT
    c.external_id as contract_external_id,
    c.person_id,
    c.start_date as contract_start_date,
    c.end_date as contract_end_date,
    c.sequence as contract_sequence,
    c.type_code as contract_type_code,
    c.type_description as contract_type_description,
    c.fte as contract_fte,
    c.hours_per_week as contract_hours_per_week,
    c.percentage as contract_percentage,
    c.custom_fields as contract_custom_fields,
    c.source as contract_source,
    c.department_external_id,
    d.display_name as department_display_name,
    d.code as department_code,
    c.title_external_id,
    t.name as title_name,
    t.code as title_code,
    c.location_external_id,
    l.name as location_name,
    l.code as location_code,
    c.cost_center_external_id,
    cc.name as cost_center_name,
    cc.code as cost_center_code,
    c.cost_bearer_external_id,
    cb.name as cost_bearer_name,
    cb.code as cost_bearer_code,
    c.employer_external_id,
    e.name as employer_name,
    e.code as employer_code,
    c.manager_person_external_id,
    mgr.external_id as manager_external_id,
    mgr.display_name as manager_display_name,
    c.team_external_id,
    tm.name as team_name,
    tm.code as team_code,
    c.division_external_id,
    dv.name as division_name,
    dv.code as division_code,
    c.organization_external_id,
    o.name as organization_name,
    o.code as organization_code
FROM contracts c
LEFT JOIN departments d ON c.department_external_id = d.external_id
LEFT JOIN titles t ON c.title_external_id = t.external_id
LEFT JOIN locations l ON c.location_external_id = l.external_id
LEFT JOIN cost_centers cc ON c.cost_center_external_id = cc.external_id
LEFT JOIN cost_bearers cb ON c.cost_bearer_external_id = cb.external_id
LEFT JOIN employers e ON c.employer_external_id = e.external_id
LEFT JOIN teams tm ON c.team_external_id = tm.external_id
LEFT JOIN divisions dv ON c.division_external_id = dv.external_id
LEFT JOIN organizations o ON c.organization_external_id = o.external_id
LEFT JOIN persons mgr ON c.manager_person_external_id = mgr.person_id
WHERE c.person_id IS NOT NULL
"@

    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $contractsQuery += "AND c.source = ?"
    }

    $contracts = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $contractsQuery -Parameters $queryParams
    Write-Information "Found $($contracts.Count) contracts"

    # Group contracts by person_id
    foreach ($contract in $contracts) {
        $personId = $contract.person_id
        if ($null -eq $personId) { continue }

        # Convert to string for consistent hashtable key
        $personIdKey = [string]$personId

        if (-not $contractsByPerson.ContainsKey($personIdKey)) {
            $contractsByPerson[$personIdKey] = @()
        }
        $contractsByPerson[$personIdKey] += $contract
    }

    # Build contacts lookup
    $contactsByPerson = @{}

    $contactsQuery = @"
SELECT
    ct.person_id,
    ct.type as contact_type,
    ct.email as contact_email,
    ct.phone_mobile as contact_phone_mobile,
    ct.phone_fixed as contact_phone_fixed
FROM contacts ct
WHERE ct.person_id IS NOT NULL
"@

    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $contactsQuery = @"
SELECT
    ct.person_id,
    ct.type as contact_type,
    ct.email as contact_email,
    ct.phone_mobile as contact_phone_mobile,
    ct.phone_fixed as contact_phone_fixed
FROM contacts ct
INNER JOIN persons p ON ct.person_id = p.person_id
WHERE ct.person_id IS NOT NULL AND p.source = ?
"@
    }

    $contacts = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $contactsQuery -Parameters $queryParams
    Write-Information "Found $($contacts.Count) contacts"

    # Group contacts by person_id
    foreach ($contact in $contacts) {
        $personId = $contact.person_id
        if ($null -eq $personId) { continue }

        # Convert to string for consistent hashtable key
        $personIdKey = [string]$personId

        if (-not $contactsByPerson.ContainsKey($personIdKey)) {
            $contactsByPerson[$personIdKey] = @()
        }
        $contactsByPerson[$personIdKey] += $contact
    }

    # Process and output persons
    foreach ($person in $persons) {
        try {
            $personInProcess = @{ external_id = $person.external_id }
            Write-Verbose "Processing person: $($person.display_name) [$($person.external_id)]"

            # Extract person_id and convert to string for consistent lookups
            $personId = $person.person_id
            if ($null -eq $personId) {
                Write-Warning "Skipping person with null person_id: external_id='$($person.external_id)'"
                continue
            }
            $personIdKey = [string]$personId

            # Skip invalid records - person without external_id
            if ([string]::IsNullOrWhiteSpace($person.external_id)) {
                Write-Warning "Skipping person with empty external_id: person_id='$personIdKey'"
                continue
            }

            # Convert to hashtable
            $personHash = ConvertTo-FlatHashtable -PSCustomObject $person

            # Expand custom_fields JSON
            $personHash = Expand-JsonFields -Hashtable $personHash -JsonFields @("custom_fields")

            # Format date fields
            $dateFields = @("birth_date", "primary_manager_updated_at")
            $personHash = Format-DateFields -Hashtable $personHash -DateFields $dateFields

            # Rename key fields for HelloID
            $personHash = Rename-HelloIDFields -Hashtable $personHash

            # Add contracts
            $personContracts = @()
            if ($contractsByPerson.ContainsKey($personIdKey)) {
                foreach ($contract in $contractsByPerson[$personIdKey]) {
                    $contractHash = ConvertTo-FlatHashtable -PSCustomObject $contract
                    $contractHash = Expand-JsonFields -Hashtable $contractHash -JsonFields @("contract_custom_fields")
                    $contractHash = Format-DateFields -Hashtable $contractHash -DateFields @("contract_start_date", "contract_end_date")
                    $contractHash = Sort-HashtableKeys -Hashtable $contractHash

                    # Filter inactive contracts if option is set
                    if (-not $includeInactiveContracts) {
                        $endDate = $contractHash["contract_end_date"]
                        $startDate = $contractHash["contract_start_date"]
                        $now = Get-Date

                        # Skip if contract has ended
                        # if (-not [string]::IsNullOrWhiteSpace($endDate)) {
                        #     try {
                        #         $contractEndDate = [DateTime]::Parse($endDate)
                        #         if ($contractEndDate -lt $now) {
                        #             continue
                        #         }
                        #     }
                        #     catch { }
                        # }
                    }

                    $personContracts += [PSCustomObject]$contractHash
                }
            }
            $personHash["Contracts"] = $personContracts

            # Add contacts
            $personContacts = @{}
            if ($contactsByPerson.ContainsKey($personIdKey)) {
                foreach ($contact in $contactsByPerson[$personIdKey]) {
                    $contactType = $contact.contact_type
                    if (-not [string]::IsNullOrWhiteSpace($contactType)) {
                        $personContacts["contact_$($contactType.ToLower())_email"] = ConvertFrom-TursoValue $contact.contact_email
                        $personContacts["contact_$($contactType.ToLower())_phone_mobile"] = ConvertFrom-TursoValue $contact.contact_phone_mobile
                        $personContacts["contact_$($contactType.ToLower())_phone_fixed"] = ConvertFrom-TursoValue $contact.contact_phone_fixed
                    }
                }
            }

            # Merge contacts into person hash
            foreach ($key in $personContacts.Keys) {
                $personHash[$key] = $personContacts[$key]
            }

            # Remove excluded fields
            foreach ($fieldToExclude in $excludedFields) {
                if ($personHash.Contains($fieldToExclude)) {
                    [void]$personHash.Remove($fieldToExclude)
                }
            }

            # Sort keys with DisplayName and ExternalId first
            $personHash = Sort-HashtableKeys -Hashtable $personHash

            # Output to HelloID as JSON (matching SQLite connector pattern)
            $personJson = $personHash | ConvertTo-Json -Depth 10
            Write-Output $personJson
        }
        catch {
            $ex = $PSItem
            $errorMessage = Get-ErrorMessage -ErrorObject $ex
            Write-Warning "Error processing person [$($personInProcess.external_id)]: $($errorMessage.VerboseErrorMessage)"
        }
    }

    Write-Information "Turso person import completed successfully"
}
catch {
    $ex = $PSItem
    $errorMessage = Get-ErrorMessage -ErrorObject $ex
    Write-Warning "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    Write-Error "Turso person import failed: $($errorMessage.AuditErrorMessage)"
}
