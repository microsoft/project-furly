<#
 .SYNOPSIS
    Deploys Azure services

 .PARAMETER resourceGroupName
    Can be the name of an existing or new resource group.

 .PARAMETER resourceGroupLocation
    Optional, a resource group location. If specified, will try to create a
    new resource group in this location.

 .PARAMETER subscriptionId
    Optional, the subscription id where resources will be deployed.

 .PARAMETER subscriptionName
    Or alternatively the subscription name.

 .PARAMETER tenantId
    The Azure Active Directory tenant tied to the subscription(s) that should
    be listed as options.

 .PARAMETER accountName
    The account name to use if not to use default.
#>

param(
    [string] $resourceGroupName,
    [string] $resourceGroupLocation,
    [string] $subscriptionName,
    [string] $subscriptionId,
    [string] $accountName,
    [string] $tenantId,
    [string] $environmentName = "AzureCloud"
)

# -------------------------------------------------------------------------
# Filter locations for provider and resource type
Function Select-ResourceGroupLocations() {
    param (
        $locations,
        $provider,
        $typeName
    )
    $regions = @()
    foreach ($item in $(Get-AzResourceProvider -ProviderNamespace $provider)) {
        foreach ($resourceType in $item.ResourceTypes) {
            if ($resourceType.ResourceTypeName -eq $typeName) {
                foreach ($region in $resourceType.Locations) {
                    $regions += $region
                }
            }
        }
    }
    if ($regions.Count -gt 0) {
        $locations = $locations | Where-Object {
            return $_.DisplayName -in $regions
        }
    }
    return $locations
}

# -------------------------------------------------------------------------
# Get locations
Function Get-ResourceGroupLocations() {
    # Filter resource namespaces
    $locations = Get-AzLocation | Where-Object {
        foreach ($provider in $script:requiredProviders) {
            if ($_.Providers -notcontains $provider) {
                return $false
            }
        }
        return $true
    }
    # Filter resource types - TODO read parameters from table
    $locations = Select-ResourceGroupLocations -locations $locations `
        -provider "Microsoft.Devices" -typeName "ProvisioningServices"
    return $locations
}

# -------------------------------------------------------------------------
# Select location
Function Select-ResourceGroupLocation() {
    $locations = Get-ResourceGroupLocations
    if (![string]::IsNullOrEmpty($script:resourceGroupLocation)) {
        foreach ($location in $locations) {
            if ($location.Location -eq $script:resourceGroupLocation -or `
                    $location.DisplayName -eq $script:resourceGroupLocation) {
                $script:resourceGroupLocation = $location.Location
                return
            }
        }
        if ($interactive) {
throw "Location '$script:resourceGroupLocation' is not a valid location."
        }
    }
    Write-Host "Please choose a location from this list (using its Index):"
    $script:index = 0
    $locations | Format-Table -AutoSize -property `
            @{Name="Index"; Expression = {($script:index++)}},`
            @{Name="Location"; Expression = {$_.DisplayName}} `
    | Out-Host
    while ($true) {
        $option = Read-Host -Prompt ">"
        try {
            if ([int]$option -ge 1 -and [int]$option -le $locations.Count) {
                break
            }
        }
        catch {
            Write-Host "Invalid index '$($option)' provided."
        }
Write-Host "Choose from the list using an index between 1 and $($locations.Count)."
    }
    $script:resourceGroupLocation = $locations[$option - 1].Location
}

# -------------------------------------------------------------------------
# Get or create new resource group for deployment
Function Select-ResourceGroup() {
    $first = $true
    while ([string]::IsNullOrEmpty($script:resourceGroupName) `
            -or ($script:resourceGroupName -notmatch "^[a-z0-9-_]*$")) {
        if (!$script:interactive) {
            throw "Invalid resource group name specified."
        }
        if ($first -eq $false) {
            Write-Host "Use alphanumeric characters as well as '-' or '_'."
        }
        else {
            Write-Host
            Write-Host "Please provide a name for the resource group."
            $first = $false
        }
        $script:resourceGroupName = Read-Host -Prompt ">"
    }
    $resourceGroup = Get-AzResourceGroup -Name $script:resourceGroupName `
        -ErrorAction SilentlyContinue
    if (!$resourceGroup) {
        Write-Host "Resource group '$script:resourceGroupName' does not exist."
        Select-ResourceGroupLocation
        $resourceGroup = New-AzResourceGroup -Name $script:resourceGroupName `
            -Location $script:resourceGroupLocation
        Write-Host "Created new resource group $($script:resourceGroupName)."
        return $True
    }
    else {
        $script:resourceGroupLocation = $resourceGroup.Location
        Write-Host "Using existing resource group $($script:resourceGroupName)..."
        return $False
    }
}

# -------------------------------------------------------------------------
# Get env file content from deployment
Function Get-EnvironmentVariables() {
    Param(
        $deployment
    )
    if (![string]::IsNullOrEmpty($script:resourceGroupName)) {
        Write-Output "PCS_RESOURCE_GROUP=$($script:resourceGroupName)"
    }
    $var = $deployment.Outputs["iotHubConnectionString"].Value
    if (![string]::IsNullOrEmpty($var)) {
        Write-Output "PCS_IOTHUB_CONNSTRING=$($var)"
    }
    $var = $deployment.Outputs["iotHubEventHubEndpoint"].Value
    if (![string]::IsNullOrEmpty($var)) {
        Write-Output "PCS_IOTHUB_EVENTHUBENDPOINT=$($var)"
    }
    $var = $deployment.Outputs["storageConnectionString"].Value
    if (![string]::IsNullOrEmpty($var)) {
        Write-Output "PCS_STORAGE_CONNSTRING=$($var)"
    }
}

# -------------------------------------------------------------------------
# Write or output .env file
Function Write-EnvironmentVariables() {
    Param(
        $deployment
    )

    # find the top most folder
    $rootDir = Get-RootFolder $script:ScriptDir

    $writeFile = $false
    if ($script:interactive) {
        $ENVVARS = Join-Path $rootDir ".env"
        $prompt = "Save environment as $ENVVARS for local development? [y/n]"
        $reply = Read-Host -Prompt $prompt
        if ($reply -match "[yY]") {
            $writeFile = $true
        }
        if ($writeFile) {
            if (Test-Path $ENVVARS) {
                $prompt = "Overwrite existing .env file in $rootDir? [y/n]"
                if ($reply -match "[yY]") {
                    Remove-Item $ENVVARS -Force
                }
                else {
                    $writeFile = $false
                }
            }
        }
    }
    if ($writeFile) {
        Get-EnvironmentVariables $deployment | Out-File -Encoding ascii `
            -FilePath $ENVVARS
        Write-Host
        Write-Host ".env file created in $rootDir."
        Write-Host
    }
    else {
        Get-EnvironmentVariables $deployment | Out-Default
    }
}

# -------------------------------------------------------------------------
# Script body
Set-Item Env:\SuppressAzurePowerShellBreakingChangeWarnings "true"
Import-Module Az.Accounts -MaximumVersion "2.4.0"
Import-Module Az.Resources
$script:ScriptDir = Split-Path $script:MyInvocation.MyCommand.Path
Remove-Module deploy -ErrorAction SilentlyContinue
Import-Module $(join-path $script:ScriptDir deploy.psm1)
$ErrorActionPreference = "Stop"
$script:interactive = !$script:context

$script:requiredProviders = @(
    "microsoft.devices",
    "microsoft.storage"
)

# -------------------------------------------------------------------------
# Log in - allow user to switch subscription
Write-Host "Preparing deployment..."
$context = Connect-ToAzure -EnvironmentName $script:EnvironmentName `
    -TenantId $script:TenantId -SwitchSubscription `
    -SubscriptionId $script:Subscription -SubscriptionName $script:Subscription
$script:TenantId = $context.Tenant.Id
$subscriptionName = $context.Subscription.Name
$subscriptionId = $context.Subscription.Id
Write-Host "... Subscription $subscriptionName ($subscriptionId) selected."

$script:deleteOnErrorPrompt = Select-ResourceGroup

$templateParameters = @{ }
$StartTime = $(get-date)
Write-Host "Start time: $($StartTime.ToShortTimeString())"

# register providers
$script:requiredProviders | ForEach-Object {
    Register-AzResourceProvider -ProviderNamespace $_
} | Out-Null

try {
    Write-Host "Starting deployment..."
    # Start the deployment
    $templateFilePath = Join-Path $ScriptDir "azuredeploy.json"
    $deployment = New-AzResourceGroupDeployment -ResourceGroupName $resourceGroupName `
        -TemplateFile $templateFilePath -TemplateParameterObject $templateParameters

    if ($deployment.ProvisioningState -ne "Succeeded") {
        throw "Deployment $($deployment.ProvisioningState)."
    }

    $elapsedTime = $(get-date) - $StartTime
    Write-Host "Elapsed time (hh:mm:ss): $($elapsedTime.ToString("hh\:mm\:ss"))"

    #
    # Create environment file
    #
    Write-EnvironmentVariables -deployment $deployment
    return
}
catch {
    $ex = $_
    Write-Host $_.Exception.Message
    Write-Host "Deployment failed."

    $deleteResourceGroup = $false
    if (!$script:interactive) {
        $deleteResourceGroup = $script:deleteOnErrorPrompt
    }
    else {
        if ($script:deleteOnErrorPrompt) {
            $reply = Read-Host -Prompt "Delete resource group? [y/n]"
            $deleteResourceGroup = ($reply -match "[yY]")
        }
    }
    if ($deleteResourceGroup) {
        try {
            Write-Host "Removing resource group $($script:resourceGroupName)..."
            Remove-AzResourceGroup -ResourceGroupName $script:resourceGroupName -Force
        }
        catch {
            Write-Warning $_.Exception.Message
        }
    }
    throw $ex
}

