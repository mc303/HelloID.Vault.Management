#####################################################
# HelloID-Conn-Prov-Source-Vault-SQLite-MicrosoftData-Persons
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
$includeInactiveContracts = $c.includeInactiveContracts -eq $true
$excludedFields = if ($c.fieldsToExclude) { $c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() } } else { @() }
$sourceFilter = $c.sourceFilter
$isDebug = $c.isDebug -eq $true

if ($isDebug) {
    $VerbosePreference = 'Continue'
}

Write-Information "Starting Vault SQLite person import from: $databasePath"

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
        [System.Data.DataRow]$DataRow
    )

    $result = @{}
    foreach ($col in $DataRow.Table.Columns) {
        $result[$col.ColumnName] = $DataRow[$col]
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
    Write-Information "Connecting to SQLite..."
    $personInProcess = @{ external_id = "(not started)" }

    $sourceFilterClause = ""
    $queryParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $sourceFilterClause = "AND p.source = @sourceFilter"
        $queryParams["sourceFilter"] = $sourceFilter
    }

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
WHERE p.external_id IS NOT NULL
$sourceFilterClause
ORDER BY p.display_name
"@

    $personsTable = Invoke-SqliteQuery -DatabasePath $databasePath -Query $personsQuery -Parameters $queryParams -WrapperDllPath $wrapperDllPath
    Write-Information "Found $($personsTable.Rows.Count) persons"

    $contractsSourceFilter = ""
    $contractsParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $contractsSourceFilter = "AND c.source = @sourceFilter"
        $contractsParams["sourceFilter"] = $sourceFilter
    }

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
$contractsSourceFilter
ORDER BY c.person_id, c.sequence
"@

    $contractsTable = Invoke-SqliteQuery -DatabasePath $databasePath -Query $contractsQuery -Parameters $contractsParams -WrapperDllPath $wrapperDllPath
    Write-Information "Found $($contractsTable.Rows.Count) contracts"

    $contactsSourceFilter = ""
    $contactsParams = @{}
    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $contactsSourceFilter = "AND ct.source = @sourceFilter"
        $contactsParams["sourceFilter"] = $sourceFilter
    }

    $contactsQuery = @"
SELECT
    ct.person_id,
    ct.type as contact_type,
    ct.email as contact_email,
    ct.phone_fixed as contact_phone_fixed,
    ct.phone_mobile as contact_phone_mobile,
    ct.address_street as contact_address_street,
    ct.address_street_ext as contact_address_street_ext,
    ct.address_house_number as contact_address_house_number,
    ct.address_house_number_ext as contact_address_house_number_ext,
    ct.address_postal as contact_address_postal,
    ct.address_locality as contact_address_locality,
    ct.address_country as contact_address_country
FROM contacts ct
WHERE ct.person_id IS NOT NULL
$contactsSourceFilter
ORDER BY ct.person_id, ct.type
"@

    $contactsTable = Invoke-SqliteQuery -DatabasePath $databasePath -Query $contactsQuery -Parameters $contactsParams -WrapperDllPath $wrapperDllPath
    Write-Information "Found $($contactsTable.Rows.Count) contacts"

    $contractsByPerson = @{}
    $contractsTable.Rows | ForEach-Object {
        $contractRow = $_
        if ($null -eq $contractRow) { return }
        $personId = $contractRow["person_id"]
        if ([string]::IsNullOrWhiteSpace($personId)) { return }
        
        if (-not $contractsByPerson.ContainsKey($personId)) {
            $contractsByPerson[$personId] = @()
        }

        $endDate = $contractRow["contract_end_date"]
        if (-not $includeInactiveContracts -and $null -ne $endDate -and [string]::IsNullOrWhiteSpace($endDate) -eq $false) {
            try {
                $endDateParsed = [DateTime]::Parse($endDate)
                if ($endDateParsed -lt (Get-Date)) { return }
            }
            catch { }
        }

        $contractsByPerson[$personId] += $contractRow
    }

    $contactsByPerson = @{}
    $contactsTable.Rows | ForEach-Object {
        $contactRow = $_
        if ($null -eq $contactRow) { return }
        $personId = $contactRow["person_id"]
        if ([string]::IsNullOrWhiteSpace($personId)) { return }
        
        if (-not $contactsByPerson.ContainsKey($personId)) {
            $contactsByPerson[$personId] = @()
        }
        $contactsByPerson[$personId] += $contactRow
    }

    Write-Information "Enhancing and exporting person objects to HelloID"
    $exportedPersons = 0

    foreach ($personRow in $personsTable.Rows) {
        if ($null -eq $personRow) { continue }
        $personInProcess = $personRow
        $personId = $personRow["person_id"]

        Write-Verbose "Processing person: $($personRow["external_id"])"

        $flatPerson = ConvertTo-FlatHashtable -DataRow $personRow

        $flatPerson = Format-DateFields -Hashtable $flatPerson -DateFields @('birth_date')

        $flatPerson['blocked'] = $personRow["blocked"] -eq $true
        $flatPerson['excluded'] = $personRow["excluded"] -eq $true

        $flatPerson = Expand-JsonFields -Hashtable $flatPerson -JsonFields @('custom_fields')

        $contactFields = @('email', 'phone_fixed', 'phone_mobile', 'address_street', 'address_street_ext',
                           'address_house_number', 'address_house_number_ext', 'address_postal',
                           'address_locality', 'address_country')
        
        foreach ($contactType in @('business', 'personal')) {
            foreach ($field in $contactFields) {
                $flatPerson["contact_${contactType}_$field"] = $null
            }
        }

        if ($contactsByPerson.ContainsKey($personId)) {
            foreach ($contactRow in $contactsByPerson[$personId]) {
                $contactType = 'unknown'
                if ($contactRow["contact_type"] -eq 'Business') {
                    $contactType = 'business'
                }
                elseif ($contactRow["contact_type"] -eq 'Personal') {
                    $contactType = 'personal'
                }

                foreach ($field in $contactFields) {
                    $flatPerson["contact_${contactType}_$field"] = $contactRow["contact_$field"]
                }
            }
        }

        $flatPerson = Rename-HelloIDFields -Hashtable $flatPerson

        $outputContracts = @()
        if ($contractsByPerson.ContainsKey($personId)) {
            foreach ($contractRow in $contractsByPerson[$personId]) {
                $flatContract = ConvertTo-FlatHashtable -DataRow $contractRow
                $flatContract = Format-DateFields -Hashtable $flatContract -DateFields @('contract_start_date', 'contract_end_date')
                $flatContract = Expand-JsonFields -Hashtable $flatContract -JsonFields @('contract_custom_fields')
                $flatContract = Sort-HashtableKeys -Hashtable $flatContract -FrontFields @('contract_external_id')
                $outputContracts += $flatContract
            }
        }

        $flatPerson['Contracts'] = $outputContracts

        foreach ($fieldToExclude in $excludedFields) {
            if ($flatPerson.Contains($fieldToExclude)) {
                [void]$flatPerson.Remove($fieldToExclude)
            }
        }

        $flatPerson = Sort-HashtableKeys -Hashtable $flatPerson -FrontFields @('DisplayName', 'ExternalId')

        $personJson = $flatPerson | ConvertTo-Json -Depth 10
        Write-Output $personJson
        $exportedPersons++
    }

    Write-Information "Successfully enhanced and exported person objects to HelloID. Result count: $exportedPersons"
    Write-Information "Person import completed"
}
catch {
    $ex = $PSItem
    $errorMessage = Get-ErrorMessage -ErrorObject $ex

    if ($isDebug) {
        Write-Warning "Error occurred for person [$($personInProcess.external_id)]. Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    }

    throw "Could not enhance and export person objects to HelloID. Error Message: $($errorMessage.AuditErrorMessage)"
}
