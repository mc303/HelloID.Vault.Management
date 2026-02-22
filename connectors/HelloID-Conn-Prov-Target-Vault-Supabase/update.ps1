#####################################################
# HelloID-Conn-Prov-Target-Vault-Supabase-Update
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

function Get-SupabaseAuthToken {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]$BaseUrl,
        [Parameter(Mandatory)]
        [string]$ApiKey,
        [Parameter(Mandatory)]
        [string]$Email,
        [Parameter(Mandatory)]
        [string]$Password
    )

    $headers = @{
        "apikey" = $ApiKey
        "Content-Type" = "application/json"
    }

    $body = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json -Compress

    $uri = "$BaseUrl/auth/v1/token?grant_type=password"

    $splatParams = @{
        Method = "POST"
        Uri = $uri
        Headers = $headers
        Body = $body
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }

    try {
        Write-Information "Authenticating with Supabase as [$Email]..."
        $response = Invoke-RestMethod @splatParams
        
        if ($response.access_token) {
            Write-Information "Successfully authenticated, received access token"
            return $response.access_token
        }
        else {
            throw "No access_token in authentication response"
        }
    }
    catch {
        $ex = $PSItem
        $errorDetails = "Authentication failed: $($ex.Exception.Message)"
        
        if ($ex.Exception.Response) {
            try {
                $response = $ex.Exception.Response
                if ($response.GetResponseStream) {
                    $stream = $response.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Close()
                    $errorDetails = "Authentication failed: $responseBody"
                }
            }
            catch {
                # Keep original error
            }
        }
        
        if ($ex.ErrorDetails) {
            $errorDetails = "Authentication failed: $($ex.ErrorDetails.Message)"
        }
        
        throw $errorDetails
    }
}

function Invoke-SupabaseRequest {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]$BaseUrl,
        [Parameter(Mandatory)]
        [string]$ApiKey,
        [Parameter(Mandatory)]
        [string]$AccessToken,
        [Parameter(Mandatory)]
        [string]$Endpoint,
        [Parameter(Mandatory)]
        [string]$Method,
        [Parameter()]
        [string]$Query,
        [Parameter()]
        [object]$Body
    )

    $headers = @{
        "apikey" = $ApiKey
        "Authorization" = "Bearer $AccessToken"
        "Content-Type" = "application/json"
        "Prefer" = "return=representation"
    }

    $uri = "$BaseUrl/rest/v1/$Endpoint"
    if (-not [string]::IsNullOrEmpty($Query)) {
        $uri += "?$Query"
    }

    $splatParams = @{
        Method = $Method
        Uri = $uri
        Headers = $headers
        UseBasicParsing = $true
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $splatParams.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        return Invoke-RestMethod @splatParams
    }
    catch {
        $ex = $PSItem
        $errorDetails = $ex.Exception.Message
        
        if ($ex.Exception.Response) {
            try {
                $response = $ex.Exception.Response
                if ($response.GetResponseStream) {
                    $stream = $response.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Close()
                    $errorDetails = "Supabase API error: $responseBody"
                }
            }
            catch {
                $errorDetails = "Supabase API error: $($ex.Exception.Message)"
            }
        }
        
        if ($ex.ErrorDetails) {
            $errorDetails = "Supabase API error: $($ex.ErrorDetails.Message)"
        }
        
        throw $errorDetails
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

    $baseUrl = $actionContext.Configuration.baseUrl.TrimEnd('/')
    $apiKey = $actionContext.Configuration.apiKey
    $authEmail = $actionContext.Configuration.authEmail
    $authPassword = $actionContext.Configuration.authPassword
    $encodedPersonId = [uri]::EscapeDataString($personId)

    #region Authenticate and get access token
    if ([string]::IsNullOrWhiteSpace($authEmail) -or [string]::IsNullOrWhiteSpace($authPassword)) {
        throw "authEmail and authPassword are required for RLS authentication"
    }

    $accessToken = Get-SupabaseAuthToken -BaseUrl $baseUrl -ApiKey $apiKey -Email $authEmail -Password $authPassword
    #endregion Authenticate and get access token

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

    $updatedTables = @()
    $auditMessages = @()

    #region Update persons table
    if ($personsFields.Count -gt 0) {
        if (-not($actionContext.DryRun -eq $true)) {
            Write-Information "Updating persons table for [person_id=$personId] with fields: $($personsFields.Keys -join ', ')"

            $query = "person_id=eq.$encodedPersonId"
            $result = Invoke-SupabaseRequest -BaseUrl $baseUrl -ApiKey $apiKey -AccessToken $accessToken -Endpoint "persons" -Method "PATCH" -Query $query -Body $personsFields

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

        $existingContacts = Invoke-SupabaseRequest -BaseUrl $baseUrl -ApiKey $apiKey -AccessToken $accessToken -Endpoint "contacts" -Method "GET" -Query "person_id=eq.$encodedPersonId&type=eq.$contactType&select=contact_id"

        if ($null -ne $existingContacts -and @($existingContacts).Count -gt 0) {
            $existingContactId = $existingContacts[0].contact_id
            $encodedContactId = [uri]::EscapeDataString($existingContactId.ToString())

            if (-not($actionContext.DryRun -eq $true)) {
                Write-Information "Updating contacts [$contactType] for [person_id=$personId] with fields: $($contactFields.Keys -join ', ')"

                $query = "contact_id=eq.$encodedContactId"
                $result = Invoke-SupabaseRequest -BaseUrl $baseUrl -ApiKey $apiKey -AccessToken $accessToken -Endpoint "contacts" -Method "PATCH" -Query $query -Body $contactFields

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
                $contactFields['person_id'] = $personId
                $contactFields['type'] = $contactType

                Write-Information "Creating contacts [$contactType] for [person_id=$personId] with fields: $($contactFields.Keys -join ', ')"

                $result = Invoke-SupabaseRequest -BaseUrl $baseUrl -ApiKey $apiKey -AccessToken $accessToken -Endpoint "contacts" -Method "POST" -Body $contactFields

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
            Message = "Successfully updated Supabase person [person_id=$personId]. $($auditMessages -join '; ')"
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
