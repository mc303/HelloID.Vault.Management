#####################################################
# HelloID-Conn-Prov-Target-Vault-Postgres-Npgsql-Update
# PowerShell V2
# Version: 1.0.0
#####################################################

# Set default output context
$outputContext.Success = $false

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

function New-FieldDictionary {
    $dict = New-Object 'System.Collections.Generic.Dictionary[string,object]'
    return $dict
}
#endregion functions

try {
    $account = $actionContext.Data
    $aRef = $actionContext.References.Account

    #region Validate account reference
    if ($null -eq $aRef -or [string]::IsNullOrEmpty($aRef.PersonId)) {
        throw "AccountReference.PersonId is empty. Cannot update person without a valid PersonId."
    }
    $personId = $aRef.PersonId
    #endregion Validate account reference

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

    #region Parse field mappings
    $personsFields = New-FieldDictionary
    $contactsUpdates = @{}

    $protectedFields = @('person_id', 'external_id', 'PersonId', 'ExternalId')

    foreach ($property in $account.PSObject.Properties) {
        $fieldName = $property.Name
        $fieldValue = $property.Value

        if ($fieldName -in $protectedFields) {
            Write-Information "Skipping protected field [$fieldName]"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($fieldValue)) {
            continue
        }

        if ($fieldName -like 'persons_*') {
            $columnName = $fieldName -replace '^persons_', ''
            if ($columnName -notin $protectedFields) {
                $personsFields.Add($columnName, $fieldValue)
                Write-Information "Persons field [$columnName] = [$fieldValue]"
            }
        }
        elseif ($fieldName -like 'contacts_*') {
            $parts = $fieldName -split '_'
            if ($parts.Count -ge 3) {
                $contactType = (Get-Culture).TextInfo.ToTitleCase($parts[1].ToLower())
                $columnName = $parts[2..($parts.Count - 1)] -join '_'

                if (-not $contactsUpdates.ContainsKey($contactType)) {
                    $contactsUpdates[$contactType] = New-FieldDictionary
                }
                $contactsUpdates[$contactType].Add($columnName, $fieldValue)
                Write-Information "Contacts [$contactType].[$columnName] = [$fieldValue]"
            }
        }
        else {
            Write-Information "Skipping unmapped field [$fieldName] (doesn't match persons_* or contacts_* pattern)"
        }
    }
    #endregion Parse field mappings

    $updatedTables = @()
    $auditMessages = @()

    #region Update persons table
    if ($personsFields.Count -gt 0) {
        if (-not($actionContext.DryRun -eq $true)) {
            Write-Information "Updating persons table for [person_id=$personId] with fields: $($personsFields.Keys -join ', ')"

            $rowsAffected = [HelloID.PostgreSQL.Query]::UpdateFields($connectionString, 'persons', $personsFields, 'person_id', $personId)

            if ($rowsAffected -eq 0) {
                throw "No rows updated in persons table. Person with person_id=$personId may not exist."
            }

            $updatedTables += "persons"
            $auditMessages += "Updated persons fields: $($personsFields.Keys -join ', ')"
            Write-Information "Successfully updated persons table for [person_id=$personId]"
        }
        else {
            $auditMessages += "DryRun: Would update persons fields: $($personsFields.Keys -join ', ')"
        }
    }
    #endregion Update persons table

    #region Update/Upsert contacts table
    foreach ($contactType in $contactsUpdates.Keys) {
        $contactFields = $contactsUpdates[$contactType]

        if ($contactFields.Count -eq 0) { continue }

        $query = "SELECT contact_id FROM contacts WHERE person_id = @personId AND type = @type"
        $parameters = New-Object 'System.Collections.Generic.Dictionary[string,object]'
        $parameters.Add('personId', $personId)
        $parameters.Add('type', $contactType)
        
        $result = [HelloID.PostgreSQL.Query]::ExecuteScalar($connectionString, $query, $parameters)
        $existingContactId = $result

        if ($null -ne $existingContactId -and $existingContactId -ne [DBNull]::Value) {
            if (-not($actionContext.DryRun -eq $true)) {
                Write-Information "Updating contacts [$contactType] for [person_id=$personId] with fields: $($contactFields.Keys -join ', ')"

                $rowsAffected = [HelloID.PostgreSQL.Query]::UpdateFields($connectionString, 'contacts', $contactFields, 'contact_id', $existingContactId)

                $updatedTables += "contacts_$contactType"
                $auditMessages += "Updated contacts [$contactType] fields: $($contactFields.Keys -join ', ')"
                Write-Information "Successfully updated contacts [$contactType] for [person_id=$personId]"
            }
            else {
                $auditMessages += "DryRun: Would update contacts [$contactType] fields: $($contactFields.Keys -join ', ')"
            }
        }
        else {
            if (-not($actionContext.DryRun -eq $true)) {
                $contactFields.Add('person_id', $personId)
                $contactFields.Add('type', $contactType)

                Write-Information "Creating contacts [$contactType] for [person_id=$personId] with fields: $($contactFields.Keys -join ', ')"

                [HelloID.PostgreSQL.Query]::Insert($connectionString, 'contacts', $contactFields) | Out-Null

                $updatedTables += "contacts_$contactType"
                $auditMessages += "Created contacts [$contactType] with fields: $($contactFields.Keys -join ', ')"
                Write-Information "Successfully created contacts [$contactType] for [person_id=$personId]"
            }
            else {
                $auditMessages += "DryRun: Would create contacts [$contactType] with fields: $($contactFields.Keys -join ', ')"
            }
        }
    }
    #endregion Update/Upsert contacts table

    #region Set output
    if ($personsFields.Count -eq 0 -and $contactsUpdates.Count -eq 0) {
        Write-Information "No updateable fields provided. Skipping update."
        $outputContext.AuditLogs.Add([PSCustomObject]@{
            Action  = 'UpdateAccount'
            Message = "No updateable fields provided. Skipping update."
            IsError = $false
        })
    }
    else {
        $outputContext.Success = $true
        $outputContext.AuditLogs.Add([PSCustomObject]@{
            Action  = 'UpdateAccount'
            Message = "Successfully updated PostgreSQL person [person_id=$personId]. $($auditMessages -join '; ')"
            IsError = $false
        })
    }

    $outputContext.Data = $account
    $outputContext.PreviousData = $actionContext.Data
    #endregion Set output
}
catch {
    $ex = $PSItem
    Write-Warning "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($ex.Exception.Message)"

    $outputContext.AuditLogs.Add([PSCustomObject]@{
        Action  = 'UpdateAccount'
        Message = "Error: $($ex.Exception.Message)"
        IsError = $true
    })
}
