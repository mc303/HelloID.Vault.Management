#####################################################
# HelloID-Conn-Prov-Target-Vault-SQLite-Create
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

    #region Load SQLite assembly
    Write-Information "Loading System.Data.SQLite assembly from '$($actionContext.Configuration.sqliteDllPath)'"
    try {
        Add-Type -Path $actionContext.Configuration.sqliteDllPath -ErrorAction Stop
    }
    catch {
        throw "Failed to load SQLite assembly: $($_.Exception.Message)"
    }
    #endregion Load SQLite assembly

    #region Query database for existing person
    Write-Information "Querying SQLite database for person where [$correlationField] = [$correlationValue]"

    try {
        $connectionString = "Data Source=$($actionContext.Configuration.databasePath);Version=3;Read Only=True;"
        $query = "SELECT person_id, external_id, given_name, family_name FROM persons WHERE external_id = @external_id"
        
        $connection = New-Object System.Data.SQLite.SQLiteConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $command.Parameters.AddWithValue("@external_id", $correlationValue) | Out-Null

        $adapter = New-Object System.Data.SQLite.SQLiteDataAdapter($command)
        $dataTable = New-Object System.Data.DataTable
        $adapter.Fill($dataTable) | Out-Null

        $connection.Close()

        $correlatedAccount = $dataTable.Rows
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
            # This connector only correlates - does not create new accounts
            throw "No person found in Vault database where [$correlationField] = [$correlationValue]. This connector only correlates existing persons."
        }

        'CorrelateAccount' {
            Write-Information 'Correlating Vault-SQLite account'
            
            $personId = $correlatedAccount[0]["person_id"]
            
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
