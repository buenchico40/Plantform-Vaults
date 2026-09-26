# PlatformVault · prueba de extremo a extremo (IMP-34). Requiere API en http://127.0.0.1:5080 y Web en https://localhost:5443.
# La API Key se lee de %USERPROFILE%\PlatformVaultDev\web-api-key.txt (nunca del repositorio).
$ErrorActionPreference = 'Stop'
$base = 'https://localhost:5443'
$pwd = (Get-Content (Join-Path $env:TEMP 'pv-e2e-password.txt')).Trim()  # generada por Invoke-ApiE2E.ps1
$key = (Get-Content "$env:USERPROFILE\PlatformVaultDev\web-api-key.txt").Trim()
$script:fails = 0
function Check($name, $cond) { if ($cond) { Write-Host "OK   $name" } else { Write-Host "FAIL $name" -ForegroundColor Red; $script:fails++ } }
function Req($session, $method, $path, $body = $null, $headers = @{}, $contentType = 'application/x-www-form-urlencoded') {
    $p = @{ Method = $method; Uri = "$base$path"; WebSession = $session; UseBasicParsing = $true; MaximumRedirection = 5; Headers = $headers }
    if ($null -ne $body) { $p.Body = $body; $p.ContentType = $contentType }
    try { $r = Invoke-WebRequest @p; return [pscustomobject]@{ Status = [int]$r.StatusCode; Content = $r.Content; Headers = $r.Headers } }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response; if (-not $resp) { throw }
        $text = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd()
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Content = $text; Headers = $resp.Headers }
    }
}
function Token($html) { ([regex]'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Match($html).Groups[1].Value }
function Meta($html) { ([regex]'<meta name="pv-af" content="([^"]+)"').Match($html).Groups[1].Value }
function Login($user) {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Req $s GET '/Account/Login'
    $t = Token $page.Content
    $r = Req $s POST '/Account/Login' "UserName=$user&Password=$([uri]::EscapeDataString($pwd))&__RequestVerificationToken=$([uri]::EscapeDataString($t))"
    return @($s, $r)
}

$anon = Req (New-Object Microsoft.PowerShell.Commands.WebRequestSession) GET '/Objects'
Check 'sin sesión redirige al login' ($anon.Content -match 'Iniciar sesión')

$s, $hm = Login 'custodio1'
Check 'login del custodio y tablero' ($hm.Status -eq 200 -and $hm.Content -match 'Tablero operativo')
Check 'CSP con nonce' ("$($hm.Headers['Content-Security-Policy'])" -match "script-src 'self' 'nonce-")
Check 'X-Frame-Options DENY' ("$($hm.Headers['X-Frame-Options'])" -eq 'DENY')
$cookies = $s.Cookies.GetCookies([uri]$base)
$auth = $cookies | Where-Object Name -eq '__Host-pv'
Check 'cookie de sesión HttpOnly y Secure' ($auth -and $auth.HttpOnly -and $auth.Secure)
Check 'la cookie no contiene el token en claro' ($auth.Value.Length -gt 100 -and $auth.Value -notmatch '^[A-Za-z0-9_-]{43}$')
Check 'el HTML no contiene la API Key' (-not $hm.Content.Contains($key))

$list = Req $s GET '/Objects'
Check 'inventario muestra OBJ-000001' ($list.Content -match 'OBJ-000001')
Check 'el listado no contiene valores' ($list.Content -notmatch 'valor-super-secreto')
$noCsrf = Req $s POST '/Account/Logout' ''
Check 'POST sin token CSRF rechazado (400)' ($noCsrf.Status -eq 400)
$audit = Req $s GET '/Audit'
Check 'el custodio ve la auditoría (rol con permiso)' ($audit.Status -eq 200)
$users = Req $s GET '/Users'
Check 'el custodio no administra usuarios (mensaje de permiso)' ($users.Content -notmatch 'Crear usuario')

# Operador: revelado del objeto crítico con acceso aprobado (vía Web, con re-autenticación)
$o, $oh = Login 'operador1'
$ol = Req $o GET '/Objects?Text=swift'
$id = ([regex]'"(?:[Ii]d)":"([0-9a-f-]{36})"').Match($ol.Content).Groups[1].Value
$detail = Req $o GET "/Objects/Details/$id"
Check 'detalle muestra el panel de acceso vigente' ($detail.Content -match 'Acceso temporal vigente')
$af = Meta $detail.Content
$bad = Req $o POST "/Objects/Reveal/$id" '{"password":"incorrecta-123456"}' @{ 'RequestVerificationToken' = $af; 'X-Requested-With' = 'XMLHttpRequest' } 'application/json'
Check 'revelar con contraseña incorrecta rechazado (401)' ($bad.Status -eq 401)
$ok = Req $o POST "/Objects/Reveal/$id" (@{ password = $pwd } | ConvertTo-Json) @{ 'RequestVerificationToken' = $af; 'X-Requested-With' = 'XMLHttpRequest' } 'application/json'
Check 'revelado vía Web con re-autenticación' ($ok.Status -eq 200 -and ($ok.Content | ConvertFrom-Json).value -eq 'valor-super-secreto-123')
Check 'respuesta del revelado no se guarda en caché' ("$($ok.Headers['Cache-Control'])" -match 'no-store')
[void]$o.Headers.Remove('RequestVerificationToken'); $noAf = Req $o POST "/Objects/Reveal/$id" (@{ password = $pwd } | ConvertTo-Json) @{ 'X-Requested-With' = 'XMLHttpRequest' } 'application/json'
Check 'revelado sin token CSRF rechazado (400)' ($noAf.Status -eq 400)

# Administrador: usuarios
$a, $ah = Login 'admin'
$au = Req $a GET '/Users'
Check 'el administrador ve la gestión de usuarios' ($au.Content -match 'Crear usuario')
$aa = Req $a GET '/Audit'
Check 'el administrador no ve la auditoría de negocio' ($aa.Content -notmatch 'Bitácora de auditoría')

$lo = Req $s GET '/'
$t = Meta $lo.Content
$out = Req $s POST '/Account/Logout' "__RequestVerificationToken=$([uri]::EscapeDataString($t))"
Check 'cierre de sesión' ($out.Content -match 'Iniciar sesión')
Write-Host "Fallos: $script:fails"
