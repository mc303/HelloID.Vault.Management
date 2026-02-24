#####################################################
# HelloID-Conn-Prov-Target-Vault-SQLite-MicrosoftData-Update
# PowerShell V2
# Version: 1.0.0
#####################################################

$outputContext.Success = $false

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

function ConvertTo-Dictionary {
    param (
        [Parameter(Mandatory)]
        [hashtable]$Hashtable
    )
    $dict = New-Object 'System.Collections.Generic.Dictionary[string,object]'
    foreach ($key in $Hashtable.Keys) {
        $dict.Add($key, $Hashtable[$key])
    }
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

    #region Load HelloID.SQLite wrapper
    Write-Information "Loading HelloID.SQLite wrapper from '$($actionContext.Configuration.wrapperDllPath)'"
    try {
        Add-Type -Path $actionContext.Configuration.wrapperDllPath -ErrorAction Stop
    }
    catch {
        throw "Failed to load HelloID.SQLite wrapper: $($_.Exception.Message)"
    }
    #endregion Load HelloID.SQLite wrapper

    #region Parse field mappings
    $personsFields = @{}
    $customFieldsUpdates = @{}
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

        if ($fieldName -like 'persons_custom_field_*') {
            $customFieldName = $fieldName -replace '^persons_custom_field_', ''
            $customFieldsUpdates[$customFieldName] = $fieldValue
            Write-Information "Persons custom field [$customFieldName] = [$fieldValue]"
        }
        elseif ($fieldName -like 'persons_*') {
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
            Write-Information "Skipping unmapped field [$fieldName] (doesn't match persons_*, contacts_*, or persons_custom_field_* pattern)"
        }
    }
    #endregion Parse field mappings

    $connectionString = "Data Source=$($actionContext.Configuration.databasePath)"

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

            $rowsAffected = [HelloID.SQLite.Query]::ExecuteNonQuery($connectionString, $query, (ConvertTo-Dictionary $parameters))

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

    #region Update persons custom_fields JSON
    if ($customFieldsUpdates.Count -gt 0) {
        if (-not($actionContext.DryRun -eq $true)) {
            $lookupParams = ConvertTo-Dictionary @{ personId = $personId }
            $currentCustomFieldsJson = [HelloID.SQLite.Query]::ExecuteScalar($connectionString,
                "SELECT custom_fields FROM persons WHERE person_id = @personId",
                $lookupParams)

            if ($null -eq $currentCustomFieldsJson) {
                throw "Person with person_id=$personId not found."
            }

            $customFields = @{}
            if (-not [string]::IsNullOrWhiteSpace($currentCustomFieldsJson)) {
                try {
                    $customFields = $currentCustomFieldsJson | ConvertFrom-Json
                    if ($customFields -isnot [hashtable]) {
                        $customFields = @{}
                    }
                }
                catch {
                    Write-Information "Could not parse existing custom_fields JSON, starting fresh"
                    $customFields = @{}
                }
            }

            foreach ($key in $customFieldsUpdates.Keys) {
                $customFields[$key] = $customFieldsUpdates[$key]
            }

            $updatedJson = $customFields | ConvertTo-Json -Compress -Depth 10
            $query = "UPDATE persons SET custom_fields = @customFields WHERE person_id = @personId"

            Write-Information "Persons custom_fields update query: $query"

            $params = ConvertTo-Dictionary @{ personId = $personId; customFields = $updatedJson }
            [HelloID.SQLite.Query]::ExecuteNonQuery($connectionString, $query, $params) | Out-Null

            $updatedTables += "persons_custom_fields"
            $auditMessages += "Updated persons custom fields: $($customFieldsUpdates.Keys -join ', ')"
            Write-Information "Successfully updated persons custom_fields for [person_id=$personId]"
        }
        else {
            $auditMessages += "DryRun: Would update persons custom fields: $($customFieldsUpdates.Keys -join ', ')"
        }
    }
    #endregion Update persons custom_fields JSON

    #region Update/Upsert contacts table
    foreach ($contactType in $contactsUpdates.Keys) {
        $contactFields = $contactsUpdates[$contactType]

        if ($contactFields.Count -eq 0) { continue }

        $lookupParams = ConvertTo-Dictionary @{ personId = $personId; type = $contactType }
        $existingContactId = [HelloID.SQLite.Query]::ExecuteScalar($connectionString,
            "SELECT contact_id FROM contacts WHERE person_id = @personId AND type = @type",
            $lookupParams)

        if ($null -ne $existingContactId) {
            if (-not($actionContext.DryRun -eq $true)) {
                $setClause = ($contactFields.Keys | ForEach-Object { "$_ = @$_" }) -join ", "
                $query = "UPDATE contacts SET $setClause WHERE contact_id = @contactId"

                Write-Information "Contacts [$contactType] update query: $query"

                $parameters = @{ contactId = $existingContactId }
                foreach ($key in $contactFields.Keys) {
                    $parameters[$key] = $contactFields[$key]
                }

                [HelloID.SQLite.Query]::ExecuteNonQuery($connectionString, $query, (ConvertTo-Dictionary $parameters)) | Out-Null

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

                [HelloID.SQLite.Query]::ExecuteNonQuery($connectionString, $query, (ConvertTo-Dictionary $parameters)) | Out-Null

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
    if ($personsFields.Count -eq 0 -and $customFieldsUpdates.Count -eq 0 -and $contactsUpdates.Count -eq 0) {
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
