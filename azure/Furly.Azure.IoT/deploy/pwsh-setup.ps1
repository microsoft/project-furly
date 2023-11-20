<#
 .SYNOPSIS
    Install all required dependencies.

 .DESCRIPTION
    Installs all commandlets needed to run the deployment scripts if they
    are not already installed.

 .PARAMETER Scope
    AllUsers or CurrentUser
#>

param(
    [string] $Scope = "AllUsers"
)

# -------------------------------------------------------------------------
$dependencies = @{
                                     "Az.Accounts" = @{  "Max" = "2.4.0"  }
                                    "Az.Resources" = @{  "Min" = "4.2.0"  }
}
$dependencies.Keys | ForEach-Object {
    $req = $dependencies[$_]
    $mod = $null
    for ($i = 0; $i -lt 2; $i++) {
        $mod = Get-Module -ListAvailable -Name $_ `
        | Where-Object {
            (!$req.Min -or ($_.Version -ge $req.Min)) -and `
            (!$req.Max -or ($_.Version -le $req.Max))
        } `
        | Select-Object -Last 1
        if (!$mod) {
            Write-Host "Installing $_..."
            if ($req.Max) {
                Install-Module -Name $_ -AllowClobber -Scope $script:Scope `
                    -RequiredVersion $req.Max
            }
            else {
                Install-Module -Name $_ -AllowClobber -Scope $script:Scope
            }
        }
        else {
            Write-Host "$_ ($($mod.Version)) installed."
            break
        }
    }
    if (!$mod) {
        throw "Failed to install $_!"
    }
}
# -------------------------------------------------------------------------
