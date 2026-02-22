#####################################################
# HelloID-Conn-Prov-Source-Vault-Supabase-Departments
#
# Version: 1.1.0
# Description: HelloID source connector for Vault Supabase database
#              Uses PostgREST API with flattened output structure
#              Supports source filtering and field exclusion
#              v1.1.0: Match field naming with PostgreSQL-Npgsql connector
#####################################################

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls -bor [Net.SecurityProtocolType]::Tls11 -bor [Net.SecurityProtocolType]::Tls12

$VerbosePreference = "SilentlyContinue"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$c = $configuration | ConvertFrom-Json

$baseUri = $c.baseUrl.TrimEnd('/')
$token = $c.apiKey
$useAuth = $c.useAuthentication -ne $false
$excludedFields = if ($c.fieldsToExclude) { $c.fieldsToExclude.Split(',') | ForEach-Object { $_.Trim() } } else { @() }
$sourceFilter = $c.sourceFilter

switch ($($c.isDebug)) {
    $true { $VerbosePreference = 'Continue' }
    $false { $VerbosePreference = 'SilentlyContinue' }
}

Write-Information "Start department import: Base URL: $baseUri, Use Authentication: $useAuth"

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
#endregion

try {
    Write-Information 'Querying Departments'
    $departments = [System.Collections.ArrayList]::new()

    $query = "select=*,manager:persons(person_id,external_id,display_name)"

    if (-not [string]::IsNullOrWhiteSpace($sourceFilter)) {
        $query += "&source=eq.$sourceFilter"
    }

    Get-PostgRESTData -useAuth $useAuth -Token $token -BaseUri $baseUri -Endpoint "departments" -Query $query ([ref]$departments)
    $departments = $departments | Sort-Object -Property external_id
    Write-Information "Successfully queried Departments. Result count: $($departments.count)"
}
catch {
    $ex = $PSItem
    $errorMessage = Get-ErrorMessage -ErrorObject $ex
    Write-Verbose "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    throw "Could not query Departments. Error Message: $($errorMessage.AuditErrorMessage)"
}

try {
    Write-Information 'Enhancing and exporting department objects to HelloID'
    $exportedDepartments = 0

    $departments | ForEach-Object {
        $departmentInProcess = $_

        $flatDepartment = @{
            DisplayName         = $_.display_name
            ExternalId          = $_.external_id
            code                = $_.code
            parent_external_id  = $_.parent_external_id
            manager_external_id = if ($_.manager) { $_.manager.external_id } else { $null }
            manager_display_name = if ($_.manager) { $_.manager.display_name } else { $null }
            source              = $_.source
        }

        foreach ($fieldToExclude in $excludedFields) {
            if ($flatDepartment.Contains($fieldToExclude)) {
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

    if ($c.isDebug -eq $true) {
        Write-Warning "Error occurred for department [$($departmentInProcess.external_id)]. Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
    }

    throw "Could not enhance and export department objects to HelloID. Error Message: $($errorMessage.AuditErrorMessage)"
}
