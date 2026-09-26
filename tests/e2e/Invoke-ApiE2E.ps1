# PlatformVault · prueba de extremo a extremo (IMP-34). Requiere API en http://127.0.0.1:5080 y Web en https://localhost:5443.
# La API Key se lee de %USERPROFILE%\PlatformVaultDev\web-api-key.txt (nunca del repositorio).
param([string]$AdminTempPassword)
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5080/api/v1'
$key = (Get-Content "$env:USERPROFILE\PlatformVaultDev\web-api-key.txt").Trim()
$script:fails = 0
function Check($name, $cond) { if ($cond) { Write-Host "OK   $name" } else { Write-Host "FAIL $name" -ForegroundColor Red; $script:fails++ } }

function Call($method, $path, $body = $null, $token = $null, $headers = @{}, [switch]$raw) {
    $h = @{ 'X-API-Key' = $key }
    if ($token) { $h['X-User-Session'] = $token }
    foreach ($k in $headers.Keys) { $h[$k] = $headers[$k] }
    $params = @{ Method = $method; Uri = "$base$path"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10)); $params.ContentType = 'application/json; charset=utf-8' }
    try {
        $r = Invoke-WebRequest @params
        $content = if ($r.Content) { if ($r.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($r.Content) } else { $r.Content } } else { '' }
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $(if ($content -and $content.TrimStart().StartsWith('{') -or $content.TrimStart().StartsWith('[')) { $content | ConvertFrom-Json } else { $content }); Headers = $r.Headers }
    } catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if (-not $resp) { throw }
        $reader = New-Object IO.StreamReader($resp.GetResponseStream()); $text = $reader.ReadToEnd()
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $(if ($text) { try { $text | ConvertFrom-Json } catch { $text } }); Headers = $resp.Headers }
    }
}

# Contraseña de los usuarios de prueba: aleatoria en cada ejecución (nunca versionada). La prueba de la Web la lee de %TEMP%.
$rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $bytes = New-Object byte[] 18; $rng.GetBytes($bytes)
$strong = 'Pv#' + [Convert]::ToBase64String($bytes).Replace('+', 'x').Replace('/', 'y') + '9a!'
Set-Content -Path (Join-Path $env:TEMP 'pv-e2e-password.txt') -Value $strong -Encoding ascii
function Login($user, $pwd) { (Call POST '/auth/login' @{ userName = $user; password = $pwd }).Body }
function Activate($user, $temp) {
    $s = Login $user $temp
    $c = Call POST '/auth/change-password' @{ currentPassword = $temp; newPassword = $strong } $s.sessionToken
    if ($c.Status -ne 204) { throw "change-password $user -> $($c.Status) $($c.Body | ConvertTo-Json -Compress)" }
    (Login $user $strong).sessionToken
}

# --- Canal Web -> API
$noKey = try { Invoke-WebRequest -Uri "$base/objects" -UseBasicParsing; 0 } catch { [int]$_.Exception.Response.StatusCode }
Check 'sin API Key responde 401' ($noKey -eq 401)
$badSession = Call GET '/objects' $null 'token-falso'
Check 'sesión inválida responde 401' ($badSession.Status -eq 401)

# --- Administrador
$first = Login 'admin' $AdminTempPassword
Check 'login con contraseña temporal exige cambio' ($first.mustChangePassword -eq $true)
$blocked = Call GET '/objects' $null $first.sessionToken
Check 'con contraseña temporal solo se permite cambiarla (403)' ($blocked.Status -eq 403)
$weak = Call POST '/auth/change-password' @{ currentPassword = $AdminTempPassword; newPassword = 'corta' } $first.sessionToken
Check 'contraseña débil rechazada (400)' ($weak.Status -eq 400)
$admin = Activate 'admin' $AdminTempPassword
$area = (Call GET '/areas' $null $admin).Body | Where-Object code -eq 'TI'

function NewUser($name, $roles) {
    $r = Call POST '/users' @{ userName = $name; displayName = "Usuario $name"; email = "$name@banco.local"; areaId = $area.id; roles = $roles } $admin
    if ($r.Status -ne 201) { throw "crear $name -> $($r.Status) $($r.Body | ConvertTo-Json -Compress)" }
    [pscustomobject]@{ Id = $r.Body.userId; Token = (Activate $name $r.Body.temporaryPassword) }
}
$cust = NewUser 'custodio1' @('Custodio')
$op = NewUser 'operador1' @('Custodio')
$sec = NewUser 'seguridad1' @('Seguridad')
$other = NewUser 'operador2' @('Custodio')
$sod = Call POST '/users' @{ userName = 'mixto1'; displayName = 'Mixto'; roles = @('Seguridad', 'Custodio') } $admin
Check 'SoD: Seguridad + Custodio rechazado (422)' ($sod.Status -eq 422)
$selfRole = Call POST "/users/$((Call GET '/me/effective-permissions' $null $admin).Body.user.id)/roles" @{ role = 'Custodio' } $admin
Check 'autoasignación de roles rechazada (422)' ($selfRole.Status -eq 422)

# --- Grupo
$g = Call POST '/groups' @{ name = 'Pagos'; responsibleUserId = $cust.Id } $admin
Check 'grupo creado' ($g.Status -eq 201)
$secMember = Call POST "/groups/$($g.Body.id)/members" @{ userId = $sec.Id } $admin
Check 'Seguridad no puede ser miembro (422)' ($secMember.Status -eq 422)
Check 'responsable agrega miembro' ((Call POST "/groups/$($g.Body.id)/members" @{ userId = $op.Id } $cust.Token).Status -eq 204)

# --- Objeto confidencial (aprueba el propietario)
$create = @{ type = 'Secret'; subtype = 'ApiKey'; name = 'api-pagos'; criticality = 'Medio'; sensitivity = 'Confidencial'; environment = 'Producción';
    areaId = $area.id; ownerId = $cust.Id; custodyMode = 'Internal'; expirationDate = (Get-Date).ToUniversalTime().AddDays(20).ToString('o');
    initialValue = 'valor-super-secreto-123' }
$o = Call POST '/objects' $create $cust.Token
Check "objeto creado ($($o.Body.code))" ($o.Status -eq 201)
$dup = Call POST '/objects' $create $cust.Token
Check 'nombre duplicado (409)' ($dup.Status -eq 409)
$pan = $create.Clone(); $pan.name = 'tarjeta 4111 1111 1111 1111'
Check 'PAN en metadatos rechazado (422)' ((Call POST '/objects' $pan $cust.Token).Status -eq 422)
$id = $o.Body.id
$detail = Call GET "/objects/$id" $null $cust.Token
Check 'el detalle nunca contiene el valor' (-not (($detail.Body | ConvertTo-Json -Depth 10) -match 'valor-super-secreto'))
$etag = $detail.Body.eTag
$ma = Call PATCH "/objects/$id" @{ name = 'api-pagos'; reason = 'Prueba de campo extra'; lifecycleState = 'Activo' } $cust.Token @{ 'If-Match' = $etag }
Check 'mass assignment rechazado (400)' ($ma.Status -eq 400)
$noEtag = Call PATCH "/objects/$id" @{ name = 'api-pagos-2'; reason = 'Cambio sin versión' } $cust.Token
Check 'modificación sin If-Match rechazada (400)' ($noEtag.Status -eq 400)
$r = Call PUT "/objects/$id/groups" @{ groupIds = @($g.Body.id); reason = 'Asignación al grupo' } $cust.Token @{ 'If-Match' = $etag }
Check 'objeto asignado al grupo' ($r.Status -eq 200)
$stale = Call POST "/objects/$id/state" @{ action = 'Activate'; reason = 'Versión vieja' } $cust.Token @{ 'If-Match' = $etag }
Check 'concurrencia optimista (409)' ($stale.Status -eq 409)
$r = Call POST "/objects/$id/state" @{ action = 'Activate'; reason = 'Puesta en uso' } $cust.Token @{ 'If-Match' = $r.Body.eTag }
Check 'objeto activado' ($r.Status -eq 200)

# --- IDOR / BOLA
Check 'usuario fuera de ámbito recibe 404' ((Call GET "/objects/$id" $null $other.Token).Status -eq 404)
$list = (Call GET '/objects' $null $other.Token).Body
Check 'búsqueda fuera de ámbito vacía' ($list.totalItems -eq 0)
Check 'Seguridad ve metadatos (global)' ((Call GET "/objects/$id" $null $sec.Token).Status -eq 200)

# --- Solicitud y aprobación
$noAccess = Call POST "/objects/$id/reveal" @{} $op.Token
Check 'revelar sin acceso temporal rechazado (401/403)' ($noAccess.Status -in 401, 403)
$req = Call POST '/access-requests' @{ objectIds = @($id); action = 'Reveal'; justification = 'Rotación de credenciales del servicio de pagos'; requestedDurationMinutes = 60 } $op.Token
Check "solicitud creada ($($req.Body.code), aprobador $($req.Body.approverKind))" ($req.Status -eq 201 -and $req.Body.approverKind -eq 'Owner')
$tooLong = Call POST '/access-requests' @{ objectIds = @($id); action = 'Reveal'; justification = 'Rotación de credenciales del servicio de pagos'; requestedDurationMinutes = 600 } $op.Token
Check 'duración mayor a la máxima rechazada (400/422)' ($tooLong.Status -in 400, 422)
$pending = (Call GET '/access-requests?scope=PendingForMe' $null $cust.Token).Body
Check 'pendiente visible para el propietario' ($pending.totalItems -ge 1)
$self = Call POST "/access-requests/$($req.Body.id)/approvals" @{ decision = 'Approved' } $op.Token
Check 'autoaprobación rechazada' ($self.Status -in 403, 404)
$noReauth = Call POST "/access-requests/$($req.Body.id)/approvals" @{ decision = 'Approved' } $cust.Token
Check 'aprobar sin re-autenticación reciente: inicio de sesión cuenta como re-autenticación' ($noReauth.Status -eq 200)
$reveal = Call POST "/objects/$id/reveal" @{} $op.Token
Check 'revelado con acceso aprobado' ($reveal.Status -eq 200 -and $reveal.Body.value -eq 'valor-super-secreto-123')
Check 'respuesta con Cache-Control no-store' ("$($reveal.Headers['Cache-Control'])" -match 'no-store')

# --- Objeto crítico (aprueba Seguridad)
$crit = $create.Clone(); $crit.name = 'swift-token'; $crit.criticality = 'Crítico'; $crit.sensitivity = 'Restringida'; $crit.ownerId = $op.Id
$c = Call POST '/objects' $crit $cust.Token
$cid = $c.Body.id
$act = Call POST "/objects/$cid/state" @{ action = 'Activate'; reason = 'Sin grupo todavía' } $cust.Token @{ 'If-Match' = $c.Body.eTag }
Check 'crítico sin grupo de 2 miembros no se activa (422)' ($act.Status -eq 422)
$r = Call PUT "/objects/$cid/groups" @{ groupIds = @($g.Body.id); reason = 'Asignación al grupo' } $cust.Token @{ 'If-Match' = $c.Body.eTag }
$r = Call POST "/objects/$cid/state" @{ action = 'Activate'; reason = 'Puesta en uso' } $cust.Token @{ 'If-Match' = $r.Body.eTag }
Check 'crítico activado con grupo de 2' ($r.Status -eq 200)
$creq = Call POST '/access-requests' @{ objectIds = @($cid); action = 'Reveal'; justification = 'Atención de conciliación SWIFT del día'; requestedDurationMinutes = 30 } $op.Token
Check 'crítico lo aprueba Seguridad' ($creq.Body.approverKind -eq 'Security')
$peer = Call POST "/access-requests/$($creq.Body.id)/approvals" @{ decision = 'Approved' } $cust.Token
Check 'un par no aprueba un crítico' ($peer.Status -in 403, 404)
$rej = Call POST "/access-requests/$($creq.Body.id)/approvals" @{ decision = 'Rejected' } $sec.Token
Check 'rechazo sin comentario (422)' ($rej.Status -eq 422)
$ok = Call POST "/access-requests/$($creq.Body.id)/approvals" @{ decision = 'Approved'; comment = 'Autorizado' } $sec.Token
Check 'Seguridad aprueba el crítico' ($ok.Status -eq 200 -and $ok.Body.state -eq 'Aprobada')

# --- Suspender revoca accesos
$d = Call GET "/objects/$id" $null $cust.Token
$s = Call POST "/objects/$id/state" @{ action = 'Suspend'; reason = 'Incidente de prueba' } $cust.Token @{ 'If-Match' = $d.Body.eTag }
Check "suspender revoca accesos ($($s.Body.revokedAccesses))" ($s.Body.revokedAccesses -ge 1)
Check 'tras suspender no se revela' ((Call POST "/objects/$id/reveal" @{} $op.Token).Status -eq 403)

# --- Auditoría y tablero
$audit = Call GET '/audit-events?pageSize=200' $null $sec.Token
Check "auditoría consultable por Seguridad ($($audit.Body.totalItems) eventos)" ($audit.Status -eq 200 -and $audit.Body.totalItems -gt 20)
Check 'la auditoría no contiene el valor' (-not (($audit.Body | ConvertTo-Json -Depth 10) -match 'valor-super-secreto'))
Check 'el Administrador no consulta la auditoría (403)' ((Call GET '/audit-events' $null $admin).Status -eq 403)
$chain = Call POST '/audit-events/integrity' $null $sec.Token
Check "cadena de auditoría íntegra ($($chain.Body.eventsVerified))" ($chain.Body.isIntact -eq $true)
$dash = Call GET '/dashboards/operational' $null $cust.Token
Check "tablero del custodio ($($dash.Body.total) objetos)" ($dash.Status -eq 200 -and $dash.Body.total -ge 2)
$logout = Call POST '/auth/logout' $null $op.Token
Check 'sesión revocada tras cerrar sesión' ((Call GET '/objects' $null $op.Token).Status -eq 401)

Write-Host "Fallos: $script:fails"
