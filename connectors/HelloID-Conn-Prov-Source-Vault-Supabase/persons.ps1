#####################################################
# HelloID-Conn-Prov-Source-Vault-Supabase-Persons
#
# Version: 1.0.0
# Description: HelloID source connector for Vault Supabase database
#              Uses PostgREST API with flattened output structure
#              Supports source filtering and field exclusion
#####################################################

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls -bor [Net.SecurityProtocolType]::Tls11 -bor [Net.SecurityProtocolType]::Tls12

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$baseUri = $c.baseUrl.TrimEnd('/')
$token = $c.apiKey
$useAuth = $c.useAuthentication -ne $false
$includeInactiveContracts = $c.includeInactiveContracts -eq $true
$excludedFields = if ($c.fieldsToExclude) { $c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() } } else { @() }
$sourceFilter = $c.sourceFilter

switch ($($c.isDebug)) {
    $true { $VerbosePreference = 'Continue' }
    $false { $VerbosePreference = 'SilentlyContinue' }
}

Write-Information "Start person import: Base URL: $baseUri, Use Authentication: $useAuth"

#region functions
function Resolve-HTTPError {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipeline)]
        [object]$ErrorObject
    )
    process {
        $httpErrorObj = [PSCustomObject]@{
            FullyQualifiedErrorId = $ErrorObject.FullyQualifiedErrorId
            MyCommand             = $ErrorObject.InvocationInfo.MyCommand
            RequestUri            = $ErrorObject.TargetObject.RequestUri
            ScriptStackTrace      = $ErrorObject.ScriptStackTrace
            ErrorMessage          = ''
        }
        if ($ErrorObject.Exception.GetType().FullName -eq 'Microsoft.PowerShell.Commands.HttpResponseException') {
            $httpErrorObj.ErrorMessage = $ErrorObject.ErrorDetails.Message
        }
        elseif ($ErrorObject.Exception.GetType().FullName -eq 'System.Net.WebException') {
            $httpErrorObj.ErrorMessage = [System.IO.StreamReader]::new($ErrorObject.Exception.Response.GetResponseStream()).ReadToEnd()
        }
        Write-Output $httpErrorObj
    }
}

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

        if ($($ErrorObject.Exception.GetType().FullName -eq 'Microsoft.PowerShell.Commands.HttpResponseException') -or $($ErrorObject.Exception.GetType().FullName -eq 'System.Net.WebException')) {
            $httpErrorObject = Resolve-HTTPError -Error $ErrorObject
            $errorMessage.VerboseErrorMessage = $httpErrorObject.ErrorMessage
            $errorMessage.AuditErrorMessage = $httpErrorObject.ErrorMessage
        }

        if ([String]::IsNullOrEmpty($errorMessage.VerboseErrorMessage)) {
            $errorMessage.VerboseErrorMessage = $ErrorObject.Exception.Message
        }
        if ([String]::IsNullOrEmpty($errorMessage.AuditErrorMessage)) {
            $errorMessage.AuditErrorMessage = $ErrorObject.Exception.Message
        }

        Write-Output $errorMessage
    }
}

function Get-PostgRESTData {
    param(
        [parameter(Mandatory = $true)]$useAuth,
        [parameter(Mandatory = $false)]$Token,
        [parameter(Mandatory = $true)]$BaseUri,
        [parameter(Mandatory = $true)]$Endpoint,
        [parameter(Mandatory = $false)]$Query,
        [parameter(Mandatory = $true)][ref]$data
    )

    try {
        Write-Verbose "Starting downloading objects from endpoint [$Endpoint]"
        $Headers = @{
            "apikey" = $Token
            "Accept" = "application/json"
        }
        if ($useAuth) {
            if ([string]::IsNullOrEmpty($Token)) {
                throw "Authentication is enabled, but the token is empty."
            }
            $Headers.Add("Authorization", "Bearer $Token")
        }

        $uri = "$BaseUri/rest/v1/$Endpoint"
        if (-not [string]::IsNullOrEmpty($Query)) {
            $uri += "?$Query"
        }

        $response = Invoke-RestMethod -Method Get -Uri $uri -Headers $Headers -UseBasicParsing

        foreach ($record in $response) {
            [void]$data.Value.add($record)
        }

        Write-Verbose "Downloaded [$($data.Value.count)] records from endpoint [$Endpoint]"
    }
    catch {
        $data.Value = $null
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Error querying data from [$uri]. Error Message: $($errorMessage.AuditErrorMessage)"
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

function Flatten-Object {
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,
        [Parameter(Mandatory = $false)]
        [string]$Prefix = ""
    )

    $resultHashtable = @{}

    if ($null -eq $InputObject -or -not $InputObject.PSObject) {
        return $resultHashtable
    }

    foreach ($property in $InputObject.PSObject.Properties) {
        $key = if ([string]::IsNullOrEmpty($Prefix)) { $property.Name } else { "${Prefix}_$($property.Name)" }
        $value = $property.Value

        if ($null -ne $value -and $value -is [PSCustomObject] -and $value.GetType().Name -ne 'DateTime') {
            $nestedHashtable = Flatten-Object -InputObject $value -Prefix $key
            $nestedHashtable.GetEnumerator() | ForEach-Object { $resultHashtable[$_.Name] = $_.Value }
        }
        elseif ($null -ne $value -and $value -is [array]) {
            $resultHashtable[$key] = $value
        }
        else {
            $resultHashtable[$key] = $value
        }
    }
    return $resultHashtable
}

function Expand-JsonFields {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $true)]
        [string[]]$JsonFields
    )

    foreach ($fieldName in $JsonFields) {
        if ($Hashtable.Contains($fieldName)) {
            $jsonValue = $Hashtable[$fieldName]
            if ($null -ne $jsonValue -and $jsonValue -is [PSCustomObject]) {
                foreach ($prop in $jsonValue.PSObject.Properties) {
                    $Hashtable["custom_$($prop.Name)"] = $prop.Value
                }
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

function Move-ToFront {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $true)]
        [string[]]$FieldNames
    )

    $ordered = [ordered]@{}
    foreach ($fieldName in $FieldNames) {
        if ($Hashtable.Contains($fieldName)) {
            $ordered[$fieldName] = $Hashtable[$fieldName]
        }
    }
    foreach ($key in $Hashtable.Keys) {
        if (-not $ordered.Contains($key)) {
            $ordered[$key] = $Hashtable[$key]
        }
    }
    return $ordered
}

function Sort-HashtableKeys {
    param(
        [Parameter(Mandatory = $true)]
        $Hashtable,
        [Parameter(Mandatory = $false)]
        [string[]]$FrontFields = @('DisplayName', 'ExternalId')
    )

    $ordered = [ordered]@{}
    
    # Add front fields first
    foreach ($fieldName in $FrontFields) {
        if ($Hashtable.Contains($fieldName)) {
            $ordered[$fieldName] = $Hashtable[$fieldName]
        }
    }
    
    # Sort remaining keys alphabetically
    $remainingKeys = $Hashtable.Keys | Where-Object { -not $ordered.Contains($_) } | Sort-Object
    foreach ($key in $remainingKeys) {
        $ordered[$key] = $Hashtable[$key]
    }
    
    return $ordered
}
#endregion

try {
    Write-Information 'Querying Persons with related data'
    $persons = [System.Collections.ArrayList]::new()

    $query = "select=*,contracts!contracts_person_id_fkey(*,location:locations(external_id,code,name),cost_center:cost_centers(external_id,code,name),cost_bearer:cost_bearers(external_id,code,name),employer:employers(external_id,code,name),manager:persons!manager_person_external_id(person_id,external_id,display_name),team:teams(external_id,code,name),department:departments(external_id,display_name,code),division:divisions(external_id,code,name),title:titles(external_id,code,name),organization:organizations(external_id,code,name)),contacts(*)"

    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $query += "&source=eq.$sourceFilter"
    }

    Get-PostgRESTData -useAuth $useAuth -Token $token -BaseUri $baseUri -Endpoint "persons" -Query $query ([ref]$persons)
    $persons = $persons | Sort-Object -Property external_id
    Write-Information "Successfully queried Persons. Result count: $($persons.count)"
}
catch {
    $ex = $PSItem
    $errorMessage = Get-ErrorMessage -ErrorObject $ex
    Write-Verbose "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    throw "Could not query Persons. Error Message: $($errorMessage.AuditErrorMessage)"
}

try {
    Write-Information 'Enhancing and exporting person objects to HelloID'
    $exportedPersons = 0

    $persons | ForEach-Object {
        $personInProcess = $_

        $personDataToFlatten = $_ | Select-Object * -ExcludeProperty 'contracts', 'contacts'
        $flatPerson = Flatten-Object -InputObject $personDataToFlatten

        $flatPerson = Format-DateFields -Hashtable $flatPerson -DateFields @('birth_date')

        $flatPerson['blocked'] = $_.blocked -eq $true
        $flatPerson['excluded'] = $_.excluded -eq $true

        $flatPerson = Expand-JsonFields -Hashtable $flatPerson -JsonFields @('custom_fields')

        $contactFields = @('email', 'phone_fixed', 'phone_mobile', 'address_street', 'address_street_ext',
                           'address_house_number', 'address_house_number_ext', 'address_postal',
                           'address_locality', 'address_country')

        foreach ($contactType in @('business', 'personal')) {
            foreach ($field in $contactFields) {
                $flatPerson["contact_${contactType}_$field"] = $null
            }
        }

        if ($_.contacts) {
            foreach ($contact in [array]$_.contacts) {
                $contactType = 'unknown'
                if ($contact.type -eq 'Business') {
                    $contactType = 'business'
                }
                elseif ($contact.type -eq 'Personal') {
                    $contactType = 'personal'
                }

                $flatContact = Flatten-Object -InputObject $contact -Prefix "contact_$contactType"
                $flatContact.GetEnumerator() | ForEach-Object {
                    $flatPerson[$_.Name] = $_.Value
                }
            }
        }

        $outputContracts = @()
        if ($_.contracts) {
            foreach ($contract in [array]$_.contracts) {
                if (-not $includeInactiveContracts -and $contract.end_date) {
                    try {
                        $endDateParsed = [DateTime]::Parse($contract.end_date)
                        if ($endDateParsed -lt (Get-Date)) {
                            continue
                        }
                    }
                    catch { }
                }

                $contractDataToFlatten = $contract | Select-Object * -ExcludeProperty 'location', 'cost_center', 'cost_bearer', 'employer', 'manager', 'team', 'department', 'division', 'title', 'organization'
                $flatContract = Flatten-Object -InputObject $contractDataToFlatten

                $flatContract = Format-DateFields -Hashtable $flatContract -DateFields @('start_date', 'end_date')
                $flatContract = Expand-JsonFields -Hashtable $flatContract -JsonFields @('custom_fields')
                $flatContract = Rename-HelloIDFields -Hashtable $flatContract -RenameMap @{ 'external_id' = 'ExternalId' }
                $flatContract = Sort-HashtableKeys -Hashtable $flatContract -FrontFields @('ExternalId')

                $outputContracts += $flatContract
            }
        }

        $flatPerson = Rename-HelloIDFields -Hashtable $flatPerson

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

    if ($c.isDebug -eq $true) {
        Write-Warning "Error occurred for person [$($personInProcess.external_id)]. Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    }

    throw "Could not enhance and export person objects to HelloID. Error Message: $($errorMessage.AuditErrorMessage)"
}
