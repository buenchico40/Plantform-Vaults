<#
.SYNOPSIS
    Elimina la base de datos de PlatformVault. Solo desarrollo y pruebas: destruye la auditoría (RN-078).
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $Server = '(localdb)\MSSQLLocalDB',
    [string] $DatabaseName = 'PlatformVault'
)
$ErrorActionPreference = 'Stop'
if ($PSCmdlet.ShouldProcess("$Server / $DatabaseName", 'Eliminar base de datos')) {
    $env:DatabaseName = $DatabaseName
    $env:ConfirmDrop = 'YES'
    try {
        & sqlcmd -S $Server -E -l 60 -b -f 65001 -d master -i (Join-Path $PSScriptRoot '09-Rollback\900-DropDatabase.sql')
        if ($LASTEXITCODE -ne 0) { throw "Falló el rollback (código $LASTEXITCODE)." }
    }
    finally { Remove-Item Env:DatabaseName, Env:ConfirmDrop -ErrorAction SilentlyContinue }
}
