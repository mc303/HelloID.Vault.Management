#####################################################
# HelloID-Conn-Prov-Target-Vault-Supabase-Create
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

    $baseUrl = $actionContext.Configuration.baseUrl.TrimEnd('/')
    $apiKey = $actionContext.Configuration.apiKey
    $authEmail = $actionContext.Configuration.authEmail
    $authPassword = $actionContext.Configuration.authPassword

    #region Authenticate and get access token
    if ([string]::IsNullOrWhiteSpace($authEmail) -or [string]::IsNullOrWhiteSpace($authPassword)) {
        throw "authEmail and authPassword are required for RLS authentication"
    }

    $accessToken = Get-SupabaseAuthToken -BaseUrl $baseUrl -ApiKey $apiKey -Email $authEmail -Password $authPassword
    #endregion Authenticate and get access token

    #region Query Supabase for existing person
    Write-Information "Querying Supabase for person where [$correlationField] = [$correlationValue]"

    try {
        $query = "select=person_id,external_id,given_name,family_name&external_id=eq.$correlationValue"
        
        $result = Invoke-SupabaseRequest -BaseUrl $baseUrl -ApiKey $apiKey -AccessToken $accessToken -Endpoint "persons" -Method "GET" -Query $query

        $correlatedAccount = $result
    }
    catch {
        $ex = $PSItem
        $errorMessage = Get-ErrorMessage -ErrorObject $ex
        Write-Warning "Error at Line [$($ex.InvocationInfo.ScriptLineNumber)]: $($ex.InvocationInfo.Line). Error: $($errorMessage.VerboseErrorMessage)"
        throw "Error querying Supabase: $($errorMessage.AuditErrorMessage)"
    }
    #endregion Query Supabase for existing person

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
            throw "No person found in Supabase where [$correlationField] = [$correlationValue]. This connector only correlates existing persons."
        }

        'CorrelateAccount' {
            Write-Information 'Correlating Vault-Supabase account'
            
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
