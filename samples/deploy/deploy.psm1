# -------------------------------------------------------------------------
Import-Module Az.Accounts -MaximumVersion "2.4.0"

# -------------------------------------------------------------------------
<#
 .Synopsis
  Returns the root folder

 .Description
  Returns the root folder of the repo.

 .Parameter StartDir
  The directory from which to start searching upwards.
  Defaults to script directory.

 .Example
  # Gets the root folder from here.
  Get-RootFolder
#>
Function Get-RootFolder() {
    param(
        [string] $StartDir
    )
    if ([string]::IsNullOrEmpty($StartDir)) {
        $StartDir = Split-Path $MyInvocation.MyCommand.Module.Path
    }
    $cur = $StartDir
    while (![string]::IsNullOrEmpty($cur)) {
        if (Test-Path -Path (Join-Path $cur "Tunnel.sln") -PathType Leaf) {
            return $cur
        }
        $cur = Split-Path $cur
    }
    return $StartDir
}

# Login and select subscription to deploy into
<#
 .Synopsis
  Login and select subscription to deploy into if not provided

 .Description
  Login and select subscription to deploy. Returns the context
  and subscription information.

 .PARAMETER SubscriptionId
  Subscription id.
  If not provided and results are ambiguous user will be
  prompted for.

 .PARAMETER SubscriptionName
  Subscription name.
  If not provided and results are ambiguous user will be
  prompted for.

 .PARAMETER EnvironmentName
  Azure cloud to use - defaults to Global cloud.

 .PARAMETER TenantId
  Tenant id to use.

 .PARAMETER SwitchSubscription
  Switch subscription and update any previously saved context.
  Otherwise just return any previously saved context.

 .Example
  # Connects to azure
  Connect-ToAzure
#>
Function Connect-ToAzure() {
    [OutputType([Microsoft.Azure.Commands.Profile.Models.Core.PSAzureContext])]
    Param(
        [string] $EnvironmentName = "AzureCloud",
        [string] $SubscriptionId,
        [string] $SubscriptionName,
        [string] $TenantId,
        [switch] $SwitchSubscription
    )

    $scriptDir = Split-Path $MyInvocation.MyCommand.Module.Path
    $environment = Get-AzEnvironment -Name $EnvironmentName `
        -ErrorAction SilentlyContinue
    if (!$environment) {
        throw "Environment $EnvironmentName does not exist."
    }

    $rootDir = Get-RootFolder $scriptDir
    $contextFile = Join-Path $rootDir ".user"
    # Migrate .user file into root (next to .env)
    if (!(Test-Path $contextFile)) {
        $oldFile = Join-Path $scriptDir ".user"
        if (Test-Path $oldFile) {
            Move-Item -Path $oldFile -Destination $contextFile
        }
    }
    if (Test-Path $contextFile) {
        $imported = Import-AzContext -Path $contextFile
        if ($imported `
                -and ($null -ne $imported.Context) `
                -and ($null -ne (Get-AzSubscription))) {
            $context = $imported.Context
            if (!$SwitchSubscription.IsPresent) {
                return $context
            }
        }
    }

    if (!$context) {
        try {
            Write-Host "Signing in ..."
            Write-Host
            if (![string]::IsNullOrEmpty($TenantId)) {
                $connection = Connect-AzAccount -Environment $environment.Name `
                    -WarningAction SilentlyContinue -ErrorAction Stop `
                    -Tenant $TenantId
            }
            else {
                $connection = Connect-AzAccount -Environment $environment.Name `
                    -WarningAction SilentlyContinue -ErrorAction Stop
            }
            $context = $connection.Context
        }
        catch {
            throw "The login to the Azure account was not successful."
        }
    }

    $subscriptionDetails = $null
    if (![string]::IsNullOrEmpty($SubscriptionName)) {
        $subscriptionDetails = Get-AzSubscription -SubscriptionName `
            $SubscriptionName -TenantId $TenantId -ErrorAction SilentlyContinue
    }
    if (!$subscriptionDetails -and ![string]::IsNullOrEmpty($SubscriptionId)) {
        $subscriptionDetails = Get-AzSubscription -SubscriptionId `
            $SubscriptionId -TenantId $TenantId -ErrorAction SilentlyContinue
    }
    if (!$subscriptionDetails) {
        $subscriptions = Get-AzSubscription -TenantId $TenantId `
            | Where-Object { $_.State -eq "Enabled" }

        if ($subscriptions.Count -eq 0) {
            throw "No active subscriptions found - exiting."
        }
        elseif ($subscriptions.Count -eq 1) {
            $SubscriptionId = $subscriptions[0].Id
        }
        else {
            Write-Host "Please choose a subscription from this list (using its index):"
            $script:index = 0
            $subscriptions | Format-Table -AutoSize -Property `
            @{Name = "Index"; Expression = { ($script:index++) } }, `
            @{Name = "Subscription"; Expression = { $_.Name } }, `
            @{Name = "Id"; Expression = { $_.SubscriptionId } }`
            | Out-Host
            while ($true) {
                $option = Read-Host ">"
                try {
                    if ([int]$option -ge 1 -and [int]$option -le $subscriptions.Count) {
                        break
                    }
                }
                catch {
                    Write-Host "Invalid index '$($option)' provided."
                }
Write-Host "Choose from the list using an index between 1 and $($subscriptions.Count)."
            }
            $SubscriptionId = $subscriptions[$option - 1].Id
        }
        $subscriptionDetails = Get-AzSubscription -SubscriptionId $SubscriptionId `
            -TenantId $TenantId
        if (!$subscriptionDetails) {
            throw "Failed to get details for subscription $($SubscriptionId)"
        }
    }

    # Update context
    $writeProfile = $false
    if ($context.Subscription.Id -ne $subscriptionDetails.Id) {
        $context = ($subscriptionDetails | Set-AzContext)
        # If file exists - silently update profile
        $writeProfile = Test-Path $contextFile
    }
    # If file does not exist yet - ask
    if (!(Test-Path $contextFile)) {
        $reply = Read-Host -Prompt `
"To avoid logging in again next time, would you like to save your credentials? [y/n]"
        if ($reply -match "[yY]") {
            Write-Host "Your Azure login context will be saved to:"
            Write-Host $contextFile
            Write-Host "Make sure you do not share it and delete it when no longer needed."
            $writeProfile = $true
        }
    }
    if ($writeProfile) {
        Save-AzContext -Path $contextFile
    }
    return $context
}
# -------------------------------------------------------------------------

Export-ModuleMember -Function Get-RootFolder
Export-ModuleMember -Function Connect-ToAzure

# -------------------------------------------------------------------------
