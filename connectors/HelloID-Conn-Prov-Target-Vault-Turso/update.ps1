#####################################################
# HelloID-Conn-Prov-Target-Vault-Turso-Update
# PowerShell V2
# Version: 1.0.5
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
        [System.Collections.IDictionary]$Parameters = @{}
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
            Write-Information "  Parameter [$key] = type:[$($typedValue.type)] value:[$($typedValue.value)]"
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

            # Return affected row count
            return $result.affected_row_count
        }

        return 0
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "Turso query error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Turso query error: $($errorMessage.AuditErrorMessage)"
    }
}

function Invoke-TursoScalarQuery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$AuthToken,
        [Parameter(Mandatory = $true)]
        [string]$Query,
        [Parameter(Mandatory = $false)]
        [System.Collections.IDictionary]$Parameters = @{}
    )

    try {
        Write-Verbose "Executing Turso scalar query..."

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

            # Return first value from first row
            if ($result.rows -and $result.rows.Count -gt 0) {
                $scalarValue = $result.rows[0][0]

                # Handle Turso typed response for scalar values
                if ($null -eq $scalarValue) {
                    return $null
                }
                elseif ($scalarValue.PSObject.Properties.Match('type').Count -gt 0 -and $scalarValue.PSObject.Properties.Match('value').Count -gt 0) {
                    # Turso typed response: @{type="..."; value="..."}
                    if ($scalarValue.type -eq "null") {
                        return $null
                    }
                    else {
                        return $scalarValue.value
                    }
                }
                else {
                    # Direct value
                    return $scalarValue
                }
            }

            return $null
        }

        return $null
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Verbose "Turso scalar query error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Turso query error: $($errorMessage.AuditErrorMessage)"
    }
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

    $databaseUrl = $actionContext.Configuration.databaseUrl
    $authToken = $actionContext.Configuration.authToken

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

    $updatedTables = @()
    $auditMessages = @()

    #region Update persons table
    if ($personsFields.Count -gt 0) {
        if (-not($actionContext.DryRun -eq $true)) {
            # Build SET clause and parameters
            $setParts = @()
            $parameters = @{ personId = $personId }

            foreach ($key in $personsFields.Keys) {
                $setParts += "$key = ?"
                $parameters["update_$key"] = $personsFields[$key]
            }

            $setClause = $setParts -join ", "
            $query = "UPDATE persons SET $setClause WHERE person_id = ?"

            Write-Information "Persons update query: $query"

            $rowsAffected = Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $query -Parameters $parameters

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
            $lookupParams = @{ personId = $personId }
            $currentCustomFieldsJson = Invoke-TursoScalarQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query "SELECT custom_fields FROM persons WHERE person_id = ?" -Parameters $lookupParams

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
            $query = "UPDATE persons SET custom_fields = ? WHERE person_id = ?"

            Write-Information "Persons custom_fields update query: $query"

            $params = @{ customFields = $updatedJson; personId = $personId }
            Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $query -Parameters $params | Out-Null

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

        $lookupParams = [ordered]@{ personId = $personId; type = $contactType }
        $existingContactId = Invoke-TursoScalarQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query "SELECT contact_id FROM contacts WHERE person_id = ? AND type = ?" -Parameters $lookupParams

        if ($null -ne $existingContactId) {
            if (-not($actionContext.DryRun -eq $true)) {
                # Update existing contact
                $setParts = @()
                $parameters = [ordered]@{}

                foreach ($key in $contactFields.Keys) {
                    $setParts += "$key = ?"
                    $parameters[$key] = $contactFields[$key]
                }

                # Add contactId LAST (for WHERE clause)
                $parameters["contactId"] = $existingContactId

                $setClause = $setParts -join ", "
                $query = "UPDATE contacts SET $setClause WHERE contact_id = ?"

                Write-Information "Contacts [$contactType] update query: $query"
                Write-Information "Parameters: $($parameters.Keys -join ', ')"
                Write-Information "Values: $($parameters.Values -join ', ')"

                Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $query -Parameters $parameters | Out-Null

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
                # Insert new contact
                $columns = @('person_id', 'type') + @($contactFields.Keys)
                $placeholders = @('?', '?') + @($contactFields.Keys | ForEach-Object { '?' })
                $query = "INSERT INTO contacts ($($columns -join ', ')) VALUES ($($placeholders -join ', '))"

                Write-Information "Contacts [$contactType] insert query: $query"

                $parameters = [ordered]@{ personId = $personId; type = $contactType }
                foreach ($key in $contactFields.Keys) {
                    $parameters["insert_$key"] = $contactFields[$key]
                }

                Invoke-TursoQuery -DatabaseUrl $databaseUrl -AuthToken $authToken -Query $query -Parameters $parameters | Out-Null

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
