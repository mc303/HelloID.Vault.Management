#####################################################
# HelloID-Conn-Prov-Target-Vault-Postgres-Npgsql-Create
# PowerShell V2
# Version: 1.0.0
#####################################################

# Set default output context
$outputContext.Success = $false
$outputContext.AccountReference = 'Currently not available'

# Set debug logging
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

    #region Load wrapper DLL and build connection string
    Write-Information "Loading HelloID.PostgreSQL wrapper DLL from '$($actionContext.Configuration.wrapperDllPath)'"
    try {
        Add-Type -Path $actionContext.Configuration.wrapperDllPath -ErrorAction Stop
    }
    catch {
        throw "Failed to load wrapper DLL: $($_.Exception.Message)"
    }

    $dbHost = $actionContext.Configuration.host
    $dbPort = $actionContext.Configuration.port
    $dbDatabase = $actionContext.Configuration.database
    $dbUsername = $actionContext.Configuration.username
    $dbPassword = $actionContext.Configuration.password

    $connectionString = "Host=$dbHost;Port=$dbPort;Database=$dbDatabase;Username=$dbUsername;Password=$dbPassword"

    Write-Information "Testing connection to PostgreSQL..."
    if (-not [HelloID.PostgreSQL.Query]::TestConnection($connectionString)) {
        throw "Failed to connect to PostgreSQL database"
    }
    #endregion Load wrapper DLL and build connection string

    #region Query database for existing person
    Write-Information "Querying PostgreSQL database for person where [$correlationField] = [$correlationValue]"

    try {
        $sql = "SELECT person_id, external_id, given_name, family_name FROM persons WHERE external_id = @external_id"
        $parameters = New-Object 'System.Collections.Generic.Dictionary[string,object]'
        $parameters.Add('external_id', $correlationValue)

        $result = [HelloID.PostgreSQL.Query]::Execute($connectionString, $sql, $parameters)
        $correlatedAccount = $result.Rows
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
    } elseif ($correlatedAccount.Count -eq 1) {
        $action = 'CorrelateAccount'
    } elseif ($correlatedAccount.Count -gt 1) {
        throw "Multiple accounts found for person where [$correlationField] is: [$correlationValue]"
    }
    #endregion Determine action

    #region Process action
    switch ($action) {
        'CreateAccount' {
            throw "No person found in Vault database where [$correlationField] = [$correlationValue]. This connector only correlates existing persons."
        }

        'CorrelateAccount' {
            Write-Information 'Correlating Vault-PostgreSQL account'
            
            $personId = $correlatedAccount[0]["person_id"]
            
            $aRef = [PSCustomObject]@{
                PersonId   = $personId.ToString()
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
