<#
.SYNOPSIS
    Genera el certificado KEK de PlatformVault (DEC-35, IMP-19, IMP-31) y su respaldo cifrado.
.DESCRIPTION
    1. Crea un certificado RSA-3072 autofirmado, exportable solo durante este script.
    2. Exporta el respaldo PFX protegido con una contraseña que se solicita de forma segura.
    3. Elimina el certificado y lo vuelve a importar como NO exportable en el almacén indicado.
    El respaldo PFX debe guardarse fuera del servidor (custodia dual). Sin respaldo verificado no se pasa a producción.
.EXAMPLE
    .\New-PlatformVaultKek.ps1 -StoreLocation CurrentUser -BackupPath C:\secure\kek-dev.pfx          # desarrollo
    .\New-PlatformVaultKek.ps1 -StoreLocation LocalMachine -BackupPath E:\kek-prod.pfx -ValidYears 5  # servidor (administrador)
#>
[CmdletBinding()]
param(
    [ValidateSet('CurrentUser', 'LocalMachine')] [string] $StoreLocation = 'CurrentUser',
    [Parameter(Mandatory)] [string] $BackupPath,
    [int] $ValidYears = 3,
    [string] $Subject = 'CN=PlatformVault KEK',
    [securestring] $BackupPassword
)
$ErrorActionPreference = 'Stop'
if (Test-Path $BackupPath) { throw "Ya existe $BackupPath; no se sobrescriben respaldos." }
if (-not $BackupPassword) { $BackupPassword = Read-Host -AsSecureString 'Contraseña del respaldo PFX (mínimo 20 caracteres)' }
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($BackupPassword))
if ($plain.Length -lt 20) { throw 'La contraseña del respaldo debe tener al menos 20 caracteres.' }
$plain = $null

$store = "Cert:\$StoreLocation\My"
$cert = New-SelfSignedCertificate -Subject $Subject -CertStoreLocation $store -KeyAlgorithm RSA -KeyLength 3072 `
    -KeyExportPolicy Exportable -KeyUsage KeyEncipherment, DataEncipherment -KeySpec KeyExchange `
    -Provider 'Microsoft Enhanced RSA and AES Cryptographic Provider' -NotAfter (Get-Date).AddYears($ValidYears)
try {
    Export-PfxCertificate -Cert $cert -FilePath $BackupPath -Password $BackupPassword -CryptoAlgorithmOption AES256_SHA256 | Out-Null
}
finally {
    Remove-Item -Path "$store\$($cert.Thumbprint)" -DeleteKey
}
$imported = Import-PfxCertificate -FilePath $BackupPath -CertStoreLocation $store -Password $BackupPassword
Write-Host "KEK creada en $store (llave no exportable)."
Write-Host "Huella (KeyProtection:ActiveThumbprint): $($imported.Thumbprint)"
Write-Host "Respaldo cifrado: $BackupPath  -> guárdelo fuera del servidor y verifique su restauración."
