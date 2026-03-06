#####################################################
# HelloID-Conn-Prov-Target-Vault-Turso-Create
# PowerShell V2
# Version: 1.0.2
#####################################################

$outputContext.Success = $false
$outputContext.AccountReference = 'Currently not available'

switch ($($actionContext.Configuration.isDebug)) {
    $true { $InformationPreference = 'Continue' }
    $false { $InformationPreference = 'SilentlyContinue' }
}

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
        }
        else {
            $errorMessage.VerboseErrorMessage = "$ErrorObject"
            $errorMessage.AuditErrorMessage = "$ErrorObject"
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
#endregion functions

try {
    #region Validate correlation configuration
    if ($actionContext.CorrelationConfiguration.Enabled) {
        $correlationField = $actionContext.CorrelationConfiguration.accountField
        $correlationValue = $actionContext.CorrelationConfiguration.accountFieldValue

        if ([string]::IsNullOrEmpty($correlationField)) {
            throw "Correlation is enabled but not configured correctly."
        }

        if ([string]::IsNullOrEmpty($correlationValue)) {
            throw "The correlation value for [$correlationField] is empty."
        }
    }
    else {
        throw "Configuration of correlation is mandatory."
    }
    #endregion Validate correlation configuration

    $databaseUrl = $actionContext.Configuration.databaseUrl
    $authToken = $actionContext.Configuration.authToken

    #region Query database for existing person
    Write-Information "Querying Turso database for person where [$correlationField] = [$correlationValue]"

    try {
        $query = "SELECT person_id, external_id, given_name, family_name FROM persons WHERE external_id = ?"
        $parameters = @{
            external_id = $correlationValue
        }

        $correlatedAccount = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $query -Parameters $parameters
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Warning "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Error querying Vault database: $($errorMessage.AuditErrorMessage)"
    }
    #endregion Query database for existing person

    #region Determine action
    if ($correlatedAccount.Count -eq 0) {
        $action = 'CreateAccount'
    }
    elseif ($correlatedAccount.Count -eq 1) {
        $action = 'CorrelateAccount'
    }
    elseif ($correlatedAccount.Count -gt 1) {
        throw "Multiple accounts found for person where [$correlationField] is: [$correlationValue]"
    }
    #endregion Determine action

    #region Process action
    switch ($action) {
        'CreateAccount' {
            throw "No person found in Vault database where [$correlationField] = [$correlationValue]. This connector only correlates existing persons."
        }

        'CorrelateAccount' {
            Write-Information 'Correlating Vault-Turso account'

            $personId = $correlatedAccount[0].person_id

            $aRef = [PSCustomObject]@{
                PersonId   = $personId
                ExternalId = $correlationValue
            }

            $outputContext.AccountReference = $aRef
            $outputContext.AccountCorrelated = $true

            $auditLogMessage = "Correlated account: [person_id=$personId] on field: [$correlationField] with value: [$correlationValue]"
        }
    }

    $outputContext.Success = $true
    $outputContext.AuditLogs.Add([PSCustomObject]@{
        Action  = $action
        Message = $auditLogMessage
        IsError = $false
    })
    #endregion Process action
}
catch {
    $ex = $PSItem
    Write-Warning "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($ex.Exception.Message)"

    $outputContext.AuditLogs.Add([PSCustomObject]@{
        Action  = 'CreateAccount'
        Message = "Error: $($ex.Exception.Message)"
        IsError = $true
    })
}
