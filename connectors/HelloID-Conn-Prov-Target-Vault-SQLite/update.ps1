#####################################################
# HelloID-Conn-Prov-Target-Vault-SQLite-Update
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

function Invoke-SqliteQuery {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [object]$Connection,
        [Parameter(Mandatory)]
        [string]$Query,
        [Parameter()]
        [hashtable]$Parameters = @{}
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Query

    foreach ($key in $Parameters.Keys) {
        $command.Parameters.AddWithValue("@$key", $Parameters[$key]) | Out-Null
    }

    return $command.ExecuteNonQuery()
}

function Invoke-SqliteScalar {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [object]$Connection,
        [Parameter(Mandatory)]
        [string]$Query,
        [Parameter()]
        [hashtable]$Parameters = @{}
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Query

    foreach ($key in $Parameters.Keys) {
        $command.Parameters.AddWithValue("@$key", $Parameters[$key]) | Out-Null
    }

    return $command.ExecuteScalar()
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

    #region Load SQLite assembly
    Write-Information "Loading System.Data.SQLite assembly from '$($actionContext.Configuration.sqliteDllPath)'"
    try {
        Add-Type -Path $actionContext.Configuration.sqliteDllPath -ErrorAction Stop
    }
    catch {
        throw "Failed to load SQLite assembly: $($_.Exception.Message)"
    }
    #endregion Load SQLite assembly

    #region Parse field mappings
    $personsFields = @{}
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
                $personsFields[$columnName] = $fieldValue
                Write-Information "Persons field [$columnName] = [$fieldValue]"
            }
        }
        elseif ($fieldName -like 'contacts_*') {
            $parts = $fieldName -split '_'
            if ($parts.Count -ge 3) {
                $contactType = (Get-Culture).TextInfo.ToTitleCase($parts[1].ToLower())
                $columnName = $parts[2..($parts.Count - 1)] -join '_'

                if (-not $contactsUpdates.ContainsKey($contactType)) {
                    $contactsUpdates[$contactType] = @{}
                }
                $contactsUpdates[$contactType][$columnName] = $fieldValue
                Write-Information "Contacts [$contactType].[$columnName] = [$fieldValue]"
            }
        }
        else {
            Write-Information "Skipping unmapped field [$fieldName] (doesn't match persons_* or contacts_* pattern)"
        }
    }
    #endregion Parse field mappings

    #region Open connection
    $connectionString = "Data Source=$($actionContext.Configuration.databasePath);Version=3;"
    $connection = New-Object System.Data.SQLite.SQLiteConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    #endregion Open connection

    $updatedTables = @()
    $auditMessages = @()

    #region Update persons table
    if ($personsFields.Count -gt 0) {
        if (-not($actionContext.DryRun -eq $true)) {
            $setClause = ($personsFields.Keys | ForEach-Object { "$_ = @$_" }) -join ", "
            $query = "UPDATE persons SET $setClause WHERE person_id = @personId"

            Write-Information "Persons update query: $query"

            $parameters = @{ personId = $personId }
            foreach ($key in $personsFields.Keys) {
                $parameters[$key] = $personsFields[$key]
            }

            $rowsAffected = Invoke-SqliteQuery -Connection $connection -Query $query -Parameters $parameters

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

        $existingContactId = Invoke-SqliteScalar -Connection $connection -Query "SELECT contact_id FROM contacts WHERE person_id = @personId AND type = @type" -Parameters @{ personId = $personId; type = $contactType }

        if ($null -ne $existingContactId) {
            if (-not($actionContext.DryRun -eq $true)) {
                $setClause = ($contactFields.Keys | ForEach-Object { "$_ = @$_" }) -join ", "
                $query = "UPDATE contacts SET $setClause WHERE contact_id = @contactId"

                Write-Information "Contacts [$contactType] update query: $query"

                $parameters = @{ contactId = $existingContactId }
                foreach ($key in $contactFields.Keys) {
                    $parameters[$key] = $contactFields[$key]
                }

                Invoke-SqliteQuery -Connection $connection -Query $query -Parameters $parameters | Out-Null

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
                $columns = @('person_id', 'type') + @($contactFields.Keys)
                $values = @('@personId', '@type') + @($contactFields.Keys | ForEach-Object { "@$_" })
                $query = "INSERT INTO contacts ($($columns -join ', ')) VALUES ($($values -join ', '))"

                Write-Information "Contacts [$contactType] insert query: $query"

                $parameters = @{ personId = $personId; type = $contactType }
                foreach ($key in $contactFields.Keys) {
                    $parameters[$key] = $contactFields[$key]
                }

                Invoke-SqliteQuery -Connection $connection -Query $query -Parameters $parameters | Out-Null

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

    $connection.Close()

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
            Message = "Successfully updated Vault person [person_id=$personId]. $($auditMessages -join '; ')"
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
