<#
.SYNOPSIS
    Carga datos sintéticos de demostración en PlatformVault a través de la API.
.DESCRIPTION
    Crea áreas, usuarios de cada rol, grupos, objetos de los cinco tipos (con valores cifrados, certificados generados al
    vuelo, vencimientos próximos y expirados), solicitudes de acceso en distintos estados, revelados y una política de
    expiración. Todo pasa por la API: se aplican las reglas de negocio, el cifrado y la auditoría como en un uso real.

    Todos los valores son FICTICIOS y aleatorios. La contraseña común de los usuarios de demostración se genera en cada
    ejecución y se guarda fuera del repositorio, en %USERPROFILE%\PlatformVaultDev\demo-users.txt.

    Solo para desarrollo y demostraciones. Se niega a ejecutarse si los usuarios de demostración ya existen.
.EXAMPLE
    .\Seed-SyntheticData.ps1 -AdminPassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string] $ApiUrl = 'http://127.0.0.1:5080/api/v1',
    [string] $AdminUserName = 'admin',
    [Parameter(Mandatory)] [securestring] $AdminPassword,
    [string] $ApiKeyFile = (Join-Path $env:USERPROFILE 'PlatformVaultDev\web-api-key.txt'),
    [string] $OutputFile = (Join-Path $env:USERPROFILE 'PlatformVaultDev\demo-users.txt'),
    # Volumen adicional generado sobre el conjunto de ejemplo (0 = solo el conjunto de ejemplo).
    [int] $ObjectsPerArea = 30,
    [int] $RequestsPerArea = 20,
    [int] $RandomSeed = 20260926
)
$ErrorActionPreference = 'Stop'
$apiKey = (Get-Content $ApiKeyFile).Trim()
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()

function New-RandomText([int] $bytes = 24) {
    $b = New-Object byte[] $bytes; $rng.GetBytes($b)
    return [Convert]::ToBase64String($b).Replace('+', 'x').Replace('/', 'y').TrimEnd('=')
}
function Plain([securestring] $s) {
    [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
}
function Iso([double] $days) { (Get-Date).ToUniversalTime().AddDays($days).ToString('o') }

function Api([string] $method, [string] $path, $body = $null, [string] $token = $null, [string] $ifMatch = $null) {
    $headers = @{ 'X-API-Key' = $apiKey }
    if ($token) { $headers['X-User-Session'] = $token }
    if ($ifMatch) { $headers['If-Match'] = '"' + $ifMatch + '"' }
    $params = @{ Method = $method; Uri = "$ApiUrl$path"; Headers = $headers; UseBasicParsing = $true }
    if ($null -ne $body) {
        $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10))
        $params.ContentType = 'application/json; charset=utf-8'
    }
    try {
        $r = Invoke-WebRequest @params
        if ($r.Content) { return ($r.Content | ConvertFrom-Json) }
        return $null
    }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        $text = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
        throw "$method $path -> $([int]$resp.StatusCode) $text"
    }
}

# ---------------------------------------------------------------- Administrador
$demoPassword = 'Demo#' + (New-RandomText 15) + '7a!'
$adminPlain = Plain $AdminPassword
$login = Api POST '/auth/login' @{ userName = $AdminUserName; password = $adminPlain }
$adminNote = 'sin cambios'
if ($login.mustChangePassword) {
    # Contraseña temporal: se reemplaza por una nueva que se informa al final.
    $newAdmin = 'Adm#' + (New-RandomText 15) + '9b!'
    Api POST '/auth/change-password' @{ currentPassword = $adminPlain; newPassword = $newAdmin } $login.sessionToken | Out-Null
    $login = Api POST '/auth/login' @{ userName = $AdminUserName; password = $newAdmin }
    $adminNote = "cambiada a: $newAdmin"
}
$admin = $login.sessionToken

# Reanudable: si un usuario de demostración ya existe se le asigna una contraseña nueva; si un grupo ya existe se reutiliza.
function Get-UserId([string] $userName) {
    $found = Api GET "/users?q=$([uri]::EscapeDataString($userName))&pageSize=50" $null $admin
    return ($found.items | Where-Object { $_.userName -eq $userName } | Select-Object -First 1).id
}
function New-OrResetUser([string] $userName, [hashtable] $body) {
    try {
        $created = Api POST '/users' $body $admin
        return @($created.userId, $created.temporaryPassword)
    }
    catch {
        if ($_.Exception.Message -notmatch '-> (400|409|422) .*(existe|DUPLICATE|DuplicateUserName)') { throw }
        $id = Get-UserId $userName
        if (-not $id) { throw }
        $reset = Api POST "/users/$id/reset-password" $null $admin
        return @($id, $reset.temporaryPassword)
    }
}

# ---------------------------------------------------------------- Áreas
Write-Host 'Áreas...'
$areas = @{}
foreach ($a in Api GET '/areas' $null $admin) { $areas[$a.code] = $a.id }
foreach ($a in @(@('CAN', 'Canales Digitales'), @('TES', 'Tesorería'), @('INF', 'Infraestructura'))) {
    if (-not $areas.ContainsKey($a[0])) { $areas[$a[0]] = (Api POST '/areas' @{ code = $a[0]; name = $a[1] } $admin).id }
}

# ---------------------------------------------------------------- Usuarios
Write-Host 'Usuarios...'
$users = @{}
$sessions = @{}
$userDefs = @(
    @('seguridad.ana', 'Ana Morales', 'Seguridad', 'TI'),
    @('seguridad.raul', 'Raúl Paredes', 'Seguridad', 'TI'),
    @('auditor.luis', 'Luis Andrade', 'Auditor', 'TI'),
    @('custodio.maria', 'María Salazar', 'Custodio', 'TI'),
    @('custodio.pedro', 'Pedro Villacís', 'Custodio', 'CAN'),
    @('operador.jose', 'José Cevallos', 'Custodio', 'TI'),
    @('operador.carla', 'Carla Benítez', 'Custodio', 'TI'),
    @('operador.diego', 'Diego Ortega', 'Custodio', 'CAN'),
    @('operador.sofia', 'Sofía Herrera', 'Custodio', 'CAN'),
    @('operador.tomas', 'Tomás Vera', 'Custodio', 'TES')
)
foreach ($u in $userDefs) {
    $idAndTemp = New-OrResetUser $u[0] @{ userName = $u[0]; displayName = $u[1]; email = "$($u[0])@banco-demo.local"; areaId = $areas[$u[3]]; roles = @($u[2]) }
    $users[$u[0]] = $idAndTemp[0]
    $s = Api POST '/auth/login' @{ userName = $u[0]; password = $idAndTemp[1] }
    Api POST '/auth/change-password' @{ currentPassword = $idAndTemp[1]; newPassword = $demoPassword } $s.sessionToken | Out-Null
    $sessions[$u[0]] = (Api POST '/auth/login' @{ userName = $u[0]; password = $demoPassword }).sessionToken
}

# ---------------------------------------------------------------- Grupos
Write-Host 'Grupos...'
function New-Group($name, $description, $area, $responsible, $members) {
    try {
        $gid = (Api POST '/groups' @{ name = $name; description = $description; areaId = $areas[$area]; responsibleUserId = $users[$responsible] } $admin).id
    }
    catch {
        if ($_.Exception.Message -notmatch '-> 409') { throw }
        $found = Api GET "/groups?q=$([uri]::EscapeDataString($name))&pageSize=50" $null $admin
        $gid = ($found.items | Where-Object { $_.name -eq $name } | Select-Object -First 1).id
        if (-not $gid) { throw }
    }
    foreach ($m in $members) { Api POST "/groups/$gid/members" @{ userId = $users[$m]; isResponsible = $false } $admin | Out-Null }
    return $gid
}
$gPagos = New-Group 'Pagos SPI' 'Integraciones del sistema de pagos interbancarios' 'TI' 'custodio.maria' @('operador.jose', 'operador.carla')
$gInfra = New-Group 'Infraestructura Core' 'Bases de datos y equipos del núcleo bancario' 'TI' 'custodio.maria' @('operador.jose', 'operador.carla')
$gMovil = New-Group 'Banca Móvil' 'Servicios de la aplicación móvil y la banca en línea' 'CAN' 'custodio.pedro' @('operador.diego', 'operador.sofia')
$gTes = New-Group 'Tesorería Mesa de Dinero' 'Integraciones de la mesa de dinero' 'TES' 'operador.tomas' @()

# ---------------------------------------------------------------- Objetos
function New-Pfx([string] $cn, [double] $days, [string] $password) {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $req = New-Object Security.Cryptography.X509Certificates.CertificateRequest("CN=$cn, O=Banco Demo, C=EC", $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san = New-Object Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
    $san.AddDnsName($cn)
    $req.CertificateExtensions.Add($san.Build())
    $cert = $req.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-300), [DateTimeOffset]::UtcNow.AddDays($days))
    if ($password) { return [Convert]::ToBase64String($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password)) }
    return [Convert]::ToBase64String($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
}

$objects = @{}
function New-Object2([string] $key, [string] $creator, [hashtable] $body, $groups, [string[]] $states) {
    $o = Api POST '/objects' $body $sessions[$creator]
    $etag = $o.eTag
    if ($groups) {
        $etag = (Api PUT "/objects/$($o.id)/groups" @{ groupIds = @($groups); reason = 'Asignación inicial de datos de demostración' } $sessions[$creator] $etag).eTag
    }
    foreach ($s in $states) {
        $etag = (Api POST "/objects/$($o.id)/state" @{ action = $s; reason = "Datos de demostración: $s" } $sessions[$creator] $etag).eTag
    }
    $objects[$key] = $o.id
    Write-Host "  $($o.code) $($body.name)"
}
function Base([string] $type, [string] $subtype, [string] $name, [string] $criticality, [string] $sensitivity, [string] $area,
    [string] $functional, [string] $technical, [double] $days) {
    $b = @{ type = $type; subtype = $subtype; name = $name; criticality = $criticality; sensitivity = $sensitivity; environment = 'Producción';
        areaId = $areas[$area]; functionalOwnerId = $users[$functional]; technicalOwnerId = $users[$technical]; custodyMode = 'Internal' }
    if ($days -ne 0) { $b.expirationDate = Iso $days } else { $b.noExpirationJustified = $true }
    return $b
}

Write-Host 'Objetos...'
$pfxPass = New-RandomText 12
# --- Tecnología (custodio.maria)
$b = Base 'Secret' 'ApiKey' 'api-key-switch-transaccional' 'Crítico' 'Restringida' 'TI' 'custodio.maria' 'operador.jose' 20
$b.description = 'API Key del switch transaccional de pagos'; $b.initialValue = 'sk_demo_' + (New-RandomText 24)
New-Object2 'switch' 'custodio.maria' $b @($gPagos) @('Activate')

$b = Base 'Credential' 'Database' 'sql-core-bancario-svc' 'Alto' 'Confidencial' 'TI' 'custodio.maria' 'operador.carla' 45
$b.attributes = @{ targetSystem = 'sqlcore01.banco-demo.local'; accountName = 'svc_core_app'; authenticationType = 'SQL' }; $b.initialValue = New-RandomText 18
New-Object2 'sqlcore' 'custodio.maria' $b @($gInfra) @('Activate')

$b = Base 'Certificate' 'SslTls' 'portal-web-banco' 'Alto' 'Restringida' 'TI' 'custodio.maria' 'operador.jose' 0
$b.Remove('noExpirationJustified'); $b.certificateFileBase64 = New-Pfx 'portal.banco-demo.local' 12 $pfxPass; $b.certificateContainerPassword = $pfxPass
New-Object2 'portal' 'custodio.maria' $b @($gInfra) @('Activate')

$b = Base 'Certificate' 'Swift' 'swift-bic-produccion' 'Crítico' 'Restringida' 'TI' 'custodio.maria' 'operador.carla' 0
$b.Remove('noExpirationJustified'); $b.certificateFileBase64 = New-Pfx 'swift.banco-demo.local' 95 $pfxPass; $b.certificateContainerPassword = $pfxPass
New-Object2 'swift' 'custodio.maria' $b @($gPagos) @('Activate')

$key = New-Object byte[] 32; $rng.GetBytes($key)
$b = Base 'CryptographicKey' 'Symmetric' 'llave-tokenizacion-tarjetas' 'Crítico' 'Restringida' 'TI' 'custodio.maria' 'operador.jose' 300
$b.attributes = @{ algorithm = 'AES'; keyLength = '256'; keyUsage = 'Encrypt, Decrypt' }; $b.keyMaterialBase64 = [Convert]::ToBase64String($key)
New-Object2 'tokenizacion' 'custodio.maria' $b @($gPagos) @('Activate')

$b = Base 'ServiceAccount' 'ServicePrincipal' 'sp-conciliacion-batch' 'Medio' 'Confidencial' 'TI' 'custodio.maria' 'operador.carla' 150
$b.attributes = @{ directorySource = 'Local'; accountIdentifier = 'sp-conciliacion-batch'; accountKind = 'Batch' }; $b.initialValue = New-RandomText 20
New-Object2 'conciliacion' 'custodio.maria' $b @($gInfra) @('Activate')

$b = Base 'Secret' 'OAuthToken' 'token-open-banking-regulador' 'Alto' 'Confidencial' 'TI' 'custodio.maria' 'operador.jose' -3
$b.description = 'Token de acceso a la API del regulador (vencido)'; $b.initialValue = 'eyDemo.' + (New-RandomText 30)
New-Object2 'openbanking' 'custodio.maria' $b @($gPagos) @('Activate')

$b = Base 'Credential' 'NetworkDevice' 'firewall-perimetral-admin' 'Crítico' 'Restringida' 'TI' 'custodio.maria' 'operador.carla' 7
$b.attributes = @{ targetSystem = 'fw-perimetral-01'; accountName = 'admin-noc' }; $b.initialValue = New-RandomText 16
New-Object2 'firewall' 'custodio.maria' $b @($gInfra) @('Activate')

$b = Base 'Credential' 'TechnicalPassword' 'cuenta-tecnica-reportes' 'Bajo' 'Interna' 'TI' 'custodio.maria' 'operador.jose' 60
$b.custodyMode = 'MetadataOnly'; $b.attributes = @{ targetSystem = 'reportes.banco-demo.local'; accountName = 'rpt_reader' }
New-Object2 'reportes' 'custodio.maria' $b $null @('Activate')

$b = Base 'Secret' 'ApplicationSecret' 'secreto-app-onboarding' 'Medio' 'Confidencial' 'TI' 'custodio.maria' 'operador.carla' 180
$b.description = 'Pendiente de revisión: aún en borrador'; $b.initialValue = New-RandomText 20
New-Object2 'onboarding' 'custodio.maria' $b $null @()

# --- Canales Digitales (custodio.pedro)
$b = Base 'Secret' 'ApiKey' 'api-key-pasarela-pagos' 'Alto' 'Confidencial' 'CAN' 'custodio.pedro' 'operador.sofia' 28
$b.initialValue = 'pk_demo_' + (New-RandomText 24)
New-Object2 'pasarela' 'custodio.pedro' $b @($gMovil) @('Activate')

$b = Base 'Certificate' 'Api' 'mtls-app-movil' 'Alto' 'Restringida' 'CAN' 'custodio.pedro' 'operador.diego' 0
$b.Remove('noExpirationJustified'); $b.certificateFileBase64 = New-Pfx 'api-movil.banco-demo.local' 40 $pfxPass; $b.certificateContainerPassword = $pfxPass
New-Object2 'mtls' 'custodio.pedro' $b @($gMovil) @('Activate')

$b = Base 'Credential' 'Database' 'bd-banca-movil-lectura' 'Medio' 'Confidencial' 'CAN' 'custodio.pedro' 'operador.diego' 200
$b.attributes = @{ targetSystem = 'pgmovil01.banco-demo.local'; accountName = 'app_readonly' }; $b.initialValue = New-RandomText 18
New-Object2 'bdmovil' 'custodio.pedro' $b @($gMovil) @('Activate')

$b = Base 'Secret' 'OAuthToken' 'client-secret-notificaciones-push' 'Bajo' 'Confidencial' 'CAN' 'custodio.pedro' 'operador.sofia' -10
$b.initialValue = New-RandomText 28
New-Object2 'push' 'custodio.pedro' $b @($gMovil) @('Activate')

$b = Base 'ServiceAccount' 'ManagedIdentity' 'mi-servicio-notificaciones' 'Medio' 'Confidencial' 'CAN' 'custodio.pedro' 'operador.diego' 365
$b.attributes = @{ directorySource = 'Local'; accountIdentifier = 'mi-notificaciones' }; $b.initialValue = New-RandomText 20
New-Object2 'notificaciones' 'custodio.pedro' $b @($gMovil) @('Activate', 'Suspend')

$b = Base 'Certificate' 'Vpn' 'vpn-sucursales' 'Alto' 'Restringida' 'CAN' 'custodio.pedro' 'operador.sofia' 0
$b.Remove('noExpirationJustified'); $b.certificateFileBase64 = New-Pfx 'vpn.banco-demo.local' 60 $null
New-Object2 'vpn' 'custodio.pedro' $b @($gMovil) @('Activate')

$b = Base 'Secret' 'ApiKey' 'api-key-legado-sms' 'Bajo' 'Confidencial' 'CAN' 'custodio.pedro' 'operador.sofia' 30
$b.description = 'Proveedor SMS retirado'; $b.initialValue = New-RandomText 20
New-Object2 'sms' 'custodio.pedro' $b @($gMovil) @('Activate', 'Deactivate')

# --- Tesorería (operador.tomas: registra como propietario, IMP-46/IMP-58)
$b = Base 'Credential' 'Infrastructure' 'bloomberg-terminal-mesa' 'Alto' 'Confidencial' 'TES' 'operador.tomas' 'operador.tomas' 75
$b.attributes = @{ targetSystem = 'bbg-terminal-01'; accountName = 'mesa.dinero' }; $b.initialValue = New-RandomText 16
New-Object2 'bloomberg' 'operador.tomas' $b $null @('Activate')

# ---------------------------------------------------------------- Solicitudes, aprobaciones y revelados
Write-Host 'Solicitudes de acceso...'
function Request($user, $objKey, $action, $justification, $minutes) {
    Api POST '/access-requests' @{ objectIds = @($objects[$objKey]); action = $action; justification = $justification; requestedDurationMinutes = $minutes } $sessions[$user]
}
# Revelar, descargar y aprobar exigen re-autenticación en los últimos 15 minutos (IMP-29).
function Reauth($user) { Api POST '/auth/reauthenticate' @{ password = $demoPassword } $sessions[$user] | Out-Null }
# Crítico aprobado por Seguridad y revelado
$r = Request 'operador.carla' 'switch' 'Reveal' 'Rotación trimestral de la API Key del switch transaccional' 120
Reauth 'seguridad.ana'
Api POST "/access-requests/$($r.id)/approvals" @{ decision = 'Approved'; comment = 'Autorizado para la ventana de rotación' } $sessions['seguridad.ana'] | Out-Null
Reauth 'operador.carla'
Api POST "/objects/$($objects['switch'])/reveal" @{} $sessions['operador.carla'] | Out-Null
# Crítico pendiente de Seguridad
Request 'operador.jose' 'firewall' 'Reveal' 'Diagnóstico de reglas del firewall perimetral por incidente de red' 60 | Out-Null
# Confidencial aprobado por el propietario y revelado
$r = Request 'operador.diego' 'pasarela' 'Reveal' 'Configuración del nuevo ambiente de la pasarela de pagos' 240
Reauth 'custodio.pedro'
Api POST "/access-requests/$($r.id)/approvals" @{ decision = 'Approved'; comment = 'Aprobado' } $sessions['custodio.pedro'] | Out-Null
Reauth 'operador.diego'
Api POST "/objects/$($objects['pasarela'])/reveal" @{} $sessions['operador.diego'] | Out-Null
# Confidencial rechazado
$r = Request 'operador.jose' 'sqlcore' 'Reveal' 'Consulta puntual de saldos en la base del core bancario' 60
Reauth 'custodio.maria'
Api POST "/access-requests/$($r.id)/approvals" @{ decision = 'Rejected'; comment = 'Use la cuenta de solo lectura asignada a reportes' } $sessions['custodio.maria'] | Out-Null
# Confidencial pendiente del propietario
Request 'operador.sofia' 'bdmovil' 'Reveal' 'Verificación de la réplica de lectura de banca móvil' 90 | Out-Null
# Restringido pendiente de un par del grupo (descarga de llave privada)
Request 'operador.jose' 'portal' 'DownloadPrivateKey' 'Instalación del certificado renovado en el balanceador del portal' 60 | Out-Null
# Cancelada por el solicitante
$r = Request 'operador.diego' 'mtls' 'DownloadPrivateKey' 'Prueba de conexión mTLS desde el ambiente de QA' 30
Api POST "/access-requests/$($r.id)/cancel" $null $sessions['operador.diego'] | Out-Null

# ---------------------------------------------------------------- Volumen adicional
$bulkObjects = 0; $bulkRequests = 0; $bulkErrors = New-Object System.Collections.Generic.List[string]
if ($ObjectsPerArea -gt 0) {
    Write-Host "Volumen adicional ($ObjectsPerArea objetos y $RequestsPerArea solicitudes por área)..."
    $rnd = New-Object System.Random $RandomSeed
    function Pick($items) { return $items[$rnd.Next($items.Count)] }
    function Chance([int] $percent) { return $rnd.Next(100) -lt $percent }
    $lastReauth = @{}
    function EnsureReauth($user) {
        # Evita re-autenticar en cada llamada (límite de intentos por minuto): basta con una cada 10 minutos.
        if (-not $lastReauth.ContainsKey($user) -or ((Get-Date) - $lastReauth[$user]).TotalMinutes -gt 10) {
            Reauth $user; $lastReauth[$user] = Get-Date
        }
    }
    function Plainify([string] $s) {
        $n = $s.Normalize([Text.NormalizationForm]::FormD)
        return (-join ($n.ToCharArray() | Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne 'NonSpacingMark' })).ToLowerInvariant()
    }

    foreach ($a in @(@('RIE', 'Riesgos'), @('OPE', 'Operaciones'), @('CUM', 'Cumplimiento'))) {
        if (-not $areas.ContainsKey($a[0])) { $areas[$a[0]] = (Api POST '/areas' @{ code = $a[0]; name = $a[1] } $admin).id }
    }
    $firstNames = @('Andrea', 'Bruno', 'Camila', 'Daniel', 'Elena', 'Fernando', 'Gabriela', 'Hugo', 'Isabel', 'Javier', 'Karina', 'Lorena',
        'Mateo', 'Natalia', 'Óscar', 'Paula', 'Ricardo', 'Silvia', 'Tatiana', 'Ulises', 'Valeria', 'Wilson', 'Ximena', 'Yolanda', 'Zoe')
    $lastNames = @('Aguirre', 'Bravo', 'Castro', 'Delgado', 'Espinoza', 'Flores', 'Guerrero', 'Hidalgo', 'Iturralde', 'Jaramillo',
        'León', 'Mendoza', 'Núñez', 'Ordóñez', 'Pazmiño', 'Quiroga', 'Robles', 'Sánchez', 'Torres', 'Úbeda', 'Valencia', 'Zambrano')
    $taken = @{}; foreach ($k in $users.Keys) { $taken[$k] = $true }
    function New-DemoUser([string] $role, [string] $area) {
        $first = Pick $firstNames; $last = Pick $lastNames
        $base = Plainify "$first.$last"; $name = $base; $i = 2
        while ($taken.ContainsKey($name)) { $name = "$base$i"; $i++ }
        $taken[$name] = $true
        $created = Api POST '/users' @{ userName = $name; displayName = "$first $last"; email = "$name@banco-demo.local"; areaId = $areas[$area]; roles = @($role) } $admin
        $users[$name] = $created.userId
        $s = Api POST '/auth/login' @{ userName = $name; password = $created.temporaryPassword }
        Api POST '/auth/change-password' @{ currentPassword = $created.temporaryPassword; newPassword = $demoPassword } $s.sessionToken | Out-Null
        $sessions[$name] = (Api POST '/auth/login' @{ userName = $name; password = $demoPassword }).sessionToken
        $script:userDefs += , @($name, "$first $last", $role, $area)
        Start-Sleep -Milliseconds 400
        return $name
    }

    $securityUsers = @('seguridad.ana', 'seguridad.raul', (New-DemoUser 'Seguridad' 'CUM'))
    New-DemoUser 'Auditor' 'CUM' | Out-Null

    $systems = @('core', 'pagos', 'swift', 'cajeros', 'tarjetas', 'crm', 'nomina', 'contabilidad', 'riesgos', 'antifraude', 'portal',
        'movil', 'api-gateway', 'bus-servicios', 'datalake', 'correo', 'backup', 'firma-digital', 'kyc', 'credito', 'inversiones', 'cobranzas')
    $envs = @(@('Producción', 'prd'), @('Producción', 'prd'), @('Producción', 'prd'), @('ContingenciaDR', 'dr'), @('PreproducciónUAT', 'uat'),
        @('QA', 'qa'), @('Desarrollo', 'dev'))
    $catalog = @{
        Certificate = @('Digital', 'SslTls', 'Vpn', 'Api', 'Swift', 'Signing'); CryptographicKey = @('Symmetric', 'Asymmetric')
        Secret = @('ApplicationSecret', 'ApiKey', 'OAuthToken'); Credential = @('TechnicalPassword', 'Database', 'Infrastructure', 'NetworkDevice')
        ServiceAccount = @('ServicePrincipal', 'ManagedIdentity', 'DirectoryAccount', 'DatabaseAccount')
    }
    $typeWeights = @('Certificate', 'Certificate', 'Secret', 'Secret', 'Secret', 'Credential', 'Credential', 'Credential', 'ServiceAccount', 'CryptographicKey')
    $justifications = @('Rotación programada de credenciales del servicio', 'Diagnóstico de un error de conexión reportado por el monitoreo',
        'Configuración del ambiente de contingencia', 'Renovación del certificado en el balanceador de carga',
        'Migración del servicio a la nueva plataforma', 'Validación de integración con el proveedor externo',
        'Recuperación del servicio tras una ventana de mantenimiento', 'Auditoría técnica solicitada por Riesgo Operativo')
    $reqObjects = New-Object System.Collections.Generic.List[object]

    foreach ($area in @('TI', 'CAN', 'TES', 'INF', 'RIE', 'OPE', 'CUM')) {
        $custodian = New-DemoUser 'Custodio' $area
        $operators = @(1..5 | ForEach-Object { New-DemoUser 'Custodio' $area })
        $areaGroups = @()
        foreach ($g in 1..2) {
            $members = @($operators | Sort-Object { $rnd.Next() } | Select-Object -First 3)
            $label = @('Operación', 'Soporte', 'Integraciones', 'Plataforma', 'Seguridad Aplicativa') | Get-Random -SetSeed ($RandomSeed + $g + $area.GetHashCode())
            $gid = New-Group "$label $area-$g" "Grupo de demostración del área $area" $area $custodian $members
            $areaGroups += , @{ Id = $gid; Members = @($custodian) + $members }
        }

        for ($n = 1; $n -le $ObjectsPerArea; $n++) {
            try {
                $type = Pick $typeWeights; $subtype = Pick $catalog[$type]; $env = Pick $envs; $system = Pick $systems
                $criticality = if ($subtype -eq 'Swift') { 'Crítico' } else { Pick @('Crítico', 'Alto', 'Alto', 'Medio', 'Medio', 'Medio', 'Bajo', 'Bajo') }
                $metadataOnly = ($type -in @('Secret', 'Credential', 'ServiceAccount')) -and (Chance 10)
                $sensitivity = if ($type -in @('Certificate', 'CryptographicKey')) { 'Restringida' } elseif ($metadataOnly) { Pick @('Interna', 'Confidencial') } elseif (Chance 30) { 'Restringida' } else { 'Confidencial' }
                $roll = $rnd.Next(100)
                $days = if ($roll -lt 10) { - $rnd.Next(1, 60) } elseif ($roll -lt 35) { $rnd.Next(1, 31) } elseif ($roll -lt 60) { $rnd.Next(31, 121) } else { $rnd.Next(121, 700) }
                $group = Pick $areaGroups
                $technical = Pick @($group.Members | Where-Object { $_ -ne $custodian })
                $prefix = switch ($type) { 'Certificate' { 'cert' } 'CryptographicKey' { 'key' } 'Secret' { 'sec' } 'Credential' { 'cred' } default { 'svc' } }
                $b = @{ type = $type; subtype = $subtype; name = "$prefix-$system-$($env[1])-$($area.ToLowerInvariant())-$n"; criticality = $criticality;
                    sensitivity = $sensitivity; environment = $env[0]; areaId = $areas[$area]; functionalOwnerId = $users[$custodian];
                    technicalOwnerId = $users[$technical]; custodyMode = if ($metadataOnly) { 'MetadataOnly' } else { 'Internal' };
                    description = "Objeto de demostración del sistema $system ($($env[1]))" }
                $kind = $null
                switch ($type) {
                    'Certificate' {
                        if (Chance 80) { $b.certificateFileBase64 = New-Pfx "$system-$($env[1]).banco-demo.local" $days $pfxPass; $b.certificateContainerPassword = $pfxPass; $kind = 'DownloadPrivateKey' }
                        else { $b.certificateFileBase64 = New-Pfx "$system-$($env[1]).banco-demo.local" $days $null }
                    }
                    'CryptographicKey' {
                        $km = New-Object byte[] 32; $rng.GetBytes($km); $b.keyMaterialBase64 = [Convert]::ToBase64String($km); $kind = 'DownloadKeyMaterial'
                        $b.attributes = @{ algorithm = if ($subtype -eq 'Symmetric') { 'AES' } else { 'RSA' }; keyLength = if ($subtype -eq 'Symmetric') { '256' } else { '3072' } }
                    }
                    default {
                        if (-not $metadataOnly) { $b.initialValue = 'demo_' + (New-RandomText 18); $kind = 'Reveal' }
                        if ($type -eq 'Credential') { $b.attributes = @{ targetSystem = "$system-$($env[1])-01.banco-demo.local"; accountName = "svc_$($system.Replace('-', '_'))" } }
                        if ($type -eq 'ServiceAccount') { $b.attributes = @{ directorySource = Pick @('Local', 'Database'); accountIdentifier = "sa-$system-$($env[1])" } }
                    }
                }
                if ($type -ne 'Certificate') {
                    if (Chance 4) { $b.noExpirationJustified = $true } else { $b.expirationDate = Iso $days }
                }
                $needsGroup = $criticality -eq 'Crítico' -or $sensitivity -eq 'Restringida' -or (Chance 80)
                $stateRoll = $rnd.Next(100)
                $states = if ($stateRoll -lt 75) { @('Activate') } elseif ($stateRoll -lt 83) { @('Activate', 'Suspend') } elseif ($stateRoll -lt 88) { @('Activate', 'Deactivate') } else { @() }
                New-Object2 "bulk-$area-$n" $custodian $b $(if ($needsGroup) { @($group.Id) } else { $null }) $states
                $bulkObjects++
                if ($kind -and $needsGroup -and $states.Count -eq 1) {
                    $reqObjects.Add(@{ Id = $objects["bulk-$area-$n"]; Kind = $kind; Critical = $criticality -eq 'Crítico'; Restricted = $sensitivity -eq 'Restringida';
                        Group = $group; Custodian = $custodian; Technical = $technical; Area = $area })
                }
            }
            catch { $bulkErrors.Add("objeto $area-$n : $($_.Exception.Message)") }
        }

        # Solicitudes con desenlaces variados sobre los objetos activos del área.
        $candidates = @($reqObjects | Where-Object { $_.Area -eq $area })
        $requested = @{}
        for ($n = 1; $n -le $RequestsPerArea -and $candidates.Count -gt 0; $n++) {
            try {
                $o = Pick $candidates
                $requester = Pick @($o.Group.Members | Where-Object { $_ -ne $custodian })
                $dedupe = "$requester|$($o.Id)|$($o.Kind)"
                if ($requested.ContainsKey($dedupe)) { continue }
                $max = if ($o.Critical -or $o.Restricted) { 240 } else { 480 }
                $minutes = Pick @(30, 60, 120, 240, 480 | Where-Object { $_ -le $max })
                $r = Api POST '/access-requests' @{ objectIds = @($o.Id); action = $o.Kind; justification = (Pick $justifications); requestedDurationMinutes = $minutes } $sessions[$requester]
                $bulkRequests++
                $approver = if ($o.Critical) { Pick $securityUsers } elseif ($o.Restricted) { Pick @($o.Group.Members | Where-Object { $_ -ne $requester }) } else { $o.Custodian }
                $roll = $rnd.Next(100)
                if ($roll -lt 45) {
                    $requested[$dedupe] = $true
                    EnsureReauth $approver
                    Api POST "/access-requests/$($r.id)/approvals" @{ decision = 'Approved'; comment = 'Aprobado según procedimiento' } $sessions[$approver] | Out-Null
                    if ($o.Kind -eq 'Reveal' -and (Chance 70)) { EnsureReauth $requester; Api POST "/objects/$($o.Id)/reveal" @{} $sessions[$requester] | Out-Null }
                }
                elseif ($roll -lt 60) {
                    EnsureReauth $approver
                    Api POST "/access-requests/$($r.id)/approvals" @{ decision = 'Rejected'; comment = (Pick @('Justificación insuficiente', 'Use el ambiente de pruebas', 'Solicite en la ventana de cambios aprobada')) } $sessions[$approver] | Out-Null
                }
                elseif ($roll -lt 90) { $requested[$dedupe] = $true }
                else { Api POST "/access-requests/$($r.id)/cancel" $null $sessions[$requester] | Out-Null }
            }
            catch { $bulkErrors.Add("solicitud $area-$n : $($_.Exception.Message)") }
        }
        Write-Host "  Área $area lista: $bulkObjects objetos y $bulkRequests solicitudes acumuladas."
    }
}

# ---------------------------------------------------------------- Política de expiración
Write-Host 'Política de expiración...'
Api POST '/expiration-policies' @{ name = 'Certificados críticos'; appliesToType = 'Certificate'; appliesToCriticality = 'Crítico';
    thresholdDays = @(180, 120, 90, 60, 45, 30, 15, 7, 3, 1); isActive = $true } $sessions['seguridad.ana'] | Out-Null

# ---------------------------------------------------------------- Resultado
$lines = @(
    "PlatformVault · usuarios de demostración ($(Get-Date -Format 'yyyy-MM-dd HH:mm'))",
    "Contraseña común: $demoPassword",
    "Administrador ($AdminUserName): $adminNote",
    ''
) + ($userDefs | ForEach-Object { "{0,-18} {1,-10} área {2}" -f $_[0], $_[2], $_[3] })
New-Item -ItemType Directory -Force (Split-Path $OutputFile) | Out-Null
Set-Content -Path $OutputFile -Value $lines -Encoding UTF8
Write-Host ''
Write-Host "Datos cargados: $($users.Count) usuarios, $($objects.Count) objetos, $(7 + $bulkRequests) solicitudes."
if ($bulkErrors.Count -gt 0) {
    Write-Host "Elementos no cargados: $($bulkErrors.Count) (las reglas de negocio los rechazaron). Primeros:"
    $bulkErrors | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" }
}
Write-Host "Credenciales de demostración guardadas en $OutputFile"
