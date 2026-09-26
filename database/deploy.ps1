<#
.SYNOPSIS
    Despliega la base de datos de PlatformVault (esquema, procedimientos, semillas, seguridad y validaciones).
.DESCRIPTION
    Idempotente. Ejecuta los scripts en orden con sqlcmd (-b: se detiene en el primer error; -I: QUOTED_IDENTIFIER ON).
    Se conecta con autenticación de Windows de quien despliega (debe ser dbcreator/db_owner).
    La contraseña de la cuenta técnica nunca se pasa por línea de comandos: se lee de forma segura y se entrega a sqlcmd
    como variable de entorno, que se borra al terminar.
.EXAMPLE
    .\deploy.ps1                                        # LocalDB, sin login técnico (desarrollo)
    .\deploy.ps1 -Server "SQL01" -AppLoginMode WINDOWS -AppLoginName "DOMINIO\svc-platformvault" -EnableTde
    .\deploy.ps1 -Server "SQL01" -AppLoginMode SQL -AppLoginName "pv_app"   # solicita la contraseña
#>
[CmdletBinding()]
param(
    [string] $Server = '(localdb)\ProjectModels',
    [string] $DatabaseName = 'PlatformVaultDB',
    [ValidateSet('NONE', 'SQL', 'WINDOWS')] [string] $AppLoginMode = 'NONE',
    [string] $AppLoginName = 'pv_app',
    [switch] $EnableTde,
    [switch] $SkipValidation
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'No se encontró sqlcmd en el PATH.' }
if ($Server -like '(localdb)*') { & sqllocaldb start ($Server -replace '^\(localdb\)\\', '') | Out-Null }

$env:DatabaseName = $DatabaseName
$env:AppLoginMode = $AppLoginMode
$env:AppLoginName = $AppLoginName
$env:EnableTde = if ($EnableTde) { '1' } else { '0' }
$env:AppLoginPassword = 'NOT-USED'   # una variable vacía no existe en Windows
if ($AppLoginMode -eq 'SQL') {
    $secure = Read-Host -AsSecureString "Contraseña para el login $AppLoginName"
    $env:AppLoginPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

function Invoke-Script([string] $file, [string] $database) {
    Write-Host ('  ' + $file.Substring($root.Length + 1))
    & sqlcmd -S $Server -E -l 60 -b -I -f 65001 -d $database -i $file
    if ($LASTEXITCODE -ne 0) { throw "Falló el script $file (código $LASTEXITCODE)." }
}
function Invoke-Folder([string] $folder) {
    $path = Join-Path $root $folder
    if (-not (Test-Path $path)) { return }
    Get-ChildItem $path -Filter *.sql | Sort-Object Name | ForEach-Object { Invoke-Script $_.FullName $DatabaseName }
}

try {
    Write-Host "PlatformVault · despliegue en $Server / $DatabaseName"
    Invoke-Script (Join-Path $root '00-Setup\000-CreateDatabase.sql') 'master'
    Get-ChildItem (Join-Path $root '00-Setup') -Filter *.sql | Where-Object Name -ne '000-CreateDatabase.sql' |
        Sort-Object Name | ForEach-Object { Invoke-Script $_.FullName $DatabaseName }
    '01-Tables', '02-Constraints', '03-Indexes', '04-Types',
    '05-StoredProcedures\Security', '05-StoredProcedures\Identity', '05-StoredProcedures\Audit',
    '05-StoredProcedures\Commands', '05-StoredProcedures\Queries', '05-StoredProcedures\Reports',
    '06-Views', '07-SeedData', '08-Security' | ForEach-Object { Invoke-Folder $_ }
    if (-not $SkipValidation) { Invoke-Folder '10-Validation' }
    Write-Host 'Despliegue completado.' -ForegroundColor Green
}
finally {
    Remove-Item Env:AppLoginPassword, Env:AppLoginMode, Env:AppLoginName, Env:EnableTde, Env:DatabaseName -ErrorAction SilentlyContinue
}
