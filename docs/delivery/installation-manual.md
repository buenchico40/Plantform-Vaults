# PlatformVault · Manual de instalación (Fase 1, iteración 1A)

Destinatarios: administradores de servidores Windows, DBA y equipo de Seguridad que instalan PlatformVault en QA, UAT o Producción.

## 1. Arquitectura desplegada

| Componente | Tecnología | Ubicación |
|---|---|---|
| PlatformVault.Web | ASP.NET Core MVC (.NET 10) con DevExtreme 26.1 | Sitio IIS 1 (HTTPS). Único punto de acceso de los usuarios. |
| PlatformVault.Api | ASP.NET Core Web API (.NET 10) con trabajos en segundo plano | Sitio IIS 2 (HTTPS). Solo lo invoca la Web. |
| Base de datos | SQL Server 2019 o superior, base `PlatformVault` | Servidor de base de datos. Solo la conecta la API. |
| KEK | Certificado RSA-3072 no exportable | Almacén `LocalMachine\My` del servidor de la API. |

Reglas que la instalación debe respetar: la Web no tiene cadena de conexión ni acceso a la KEK; la API solo acepta llamadas de las IP de la Web con la API Key; la cuenta técnica de SQL solo ejecuta procedimientos almacenados. No se usan contenedores.

## 2. Requisitos previos

- Windows Server 2019 o superior con IIS y el **ASP.NET Core Hosting Bundle 10**.
- SQL Server 2019+ (2022+ para tablas Ledger de solo inserción, que la auditoría requiere). En QA, UAT y Producción, TDE activo (RNF-SEG-03).
- `sqlcmd` (ODBC 17 o superior) en el equipo desde el que se despliega la base.
- Para compilar la Web: la instalación licenciada de **DevExpress 26.1** en la máquina de compilación (la licencia se lee de `%AppData%\DevExpress\DevExpress_License.txt`). Ningún archivo del proveedor se guarda en el repositorio.
- Certificados TLS de servidor para los dos sitios.
- Servidor SMTP autenticado sobre TLS (opcional; apagado por defecto).

## 3. Base de datos

1. Desde `database/`, con una cuenta con permisos de `dbcreator` y `securityadmin`:

   ```powershell
   # Cuenta técnica de Windows (recomendado):
   .\deploy.ps1 -Server SQLPROD01 -AppLoginMode WINDOWS -AppLoginName "DOMINIO\svc-platformvault" -EnableTde
   # Cuenta técnica SQL (solicita la contraseña de forma segura; nunca por parámetro):
   .\deploy.ps1 -Server SQLPROD01 -AppLoginMode SQL -AppLoginName pv_app -EnableTde
   ```

2. El script es idempotente y termina con cuatro validaciones que deben mostrar `OK`: objetos creados, auditoría de solo inserción, permisos (la cuenta técnica no lee tablas) y cadena de auditoría íntegra.
3. TDE necesita antes un certificado de servidor en `master` creado y **respaldado** por el DBA (ver comentario de `08-Security/802-TransparentDataEncryption.sql`).
4. `rollback.ps1` elimina la base. **Solo para desarrollo y pruebas**: destruye la auditoría, que se conserva 10 años.

## 4. Certificado KEK (DEC-35)

Como administrador en el servidor de la API:

```powershell
.\tools\New-PlatformVaultKek.ps1 -StoreLocation LocalMachine -BackupPath E:\custodia\kek-prod.pfx -ValidYears 5
```

- El script crea un certificado RSA-3072, exporta un respaldo PFX cifrado con una contraseña de al menos 20 caracteres y lo reimporta como **no exportable**.
- Guarde el respaldo y su contraseña fuera del servidor, con custodia dual. **No pase a producción sin haber probado la restauración del respaldo**: sin la KEK, los valores custodiados son irrecuperables.
- Conceda a la identidad del grupo de aplicaciones de IIS de la API permiso de lectura sobre la llave privada (`certlm.msc` → certificado → Todas las tareas → Administrar claves privadas).
- Anote la huella (SHA-1) que muestra el script.

## 5. PlatformVault.Api

1. Publique: `dotnet publish src/PlatformVault.Api -c Release -o <carpeta>`.
2. Cree el sitio IIS con HTTPS y un grupo de aplicaciones sin código administrado que se ejecute con la cuenta técnica (si `AppLoginMode = WINDOWS`).
3. Configure los valores **fuera de `appsettings.json`** (variables de entorno del grupo de aplicaciones o archivo `appsettings.Production.json` con ACL restringida):

   | Clave | Valor |
   |---|---|
   | `Database__ConnectionString` | `Server=SQLPROD01;Database=PlatformVault;Integrated Security=true;Encrypt=true` |
   | `KeyProtection__StoreLocation` | `LocalMachine` |
   | `KeyProtection__ActiveThumbprint` | Huella del paso 4 |
   | `ApiClients__Clients__0__Name` | `PlatformVault.Web` |
   | `ApiClients__Clients__0__KeySha256` | SHA-256 en Base64 de la API Key (ver §7) |
   | `ApiClients__Clients__0__AllowedNetworks__0` | IP del servidor Web en CIDR, p. ej. `10.10.5.21/32` |
   | `Smtp__Enabled`, `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__UserName`, `Smtp__Password` | Solo si se activa el correo |

4. Compruebe la salud: `GET https://<api>/health/ready` debe responder `Healthy` (base de datos y KEK).
5. Cree el primer Administrador (una sola vez):

   ```powershell
   .\PlatformVault.Api.exe --bootstrap-admin admin.pv "Administrador de la plataforma"
   ```

   El comando muestra una contraseña temporal una única vez; el Administrador debe cambiarla en su primer inicio de sesión. Si ya existe un Administrador activo, el comando no hace nada.

## 6. PlatformVault.Web

1. Compile en una máquina con DevExpress 26.1 instalado: `dotnet publish src/PlatformVault.Web -c Release -o <carpeta>`. El proyecto copia DevExtreme a `wwwroot/lib/devextreme` al compilar (IMP-38).
2. Cree el sitio IIS con HTTPS (la cookie `__Host-pv` exige HTTPS).
3. Configuración fuera del repositorio:

   | Clave | Valor |
   |---|---|
   | `Api__BaseUrl` | `https://<api>/` |
   | `Api__Key` | API Key en claro (ver §7) |
   | `DataProtection__KeysPath` | Carpeta local con ACL solo para la identidad del sitio. Las claves se protegen con DPAPI. |

## 7. API Key del canal Web → API

1. Genere 32 bytes aleatorios y codifíquelos en Base64 URL (p. ej. con PowerShell y `RandomNumberGenerator`).
2. En la Web, guarde la clave en `Api__Key` (variable de entorno protegida).
3. En la API, guarde **solo** su SHA-256 en Base64 en `ApiClients__Clients__0__KeySha256`.
4. Rótela cada 6 meses (IMP-27). Durante la rotación puede registrar dos clientes en la API (`Clients__0` y `Clients__1`) y retirar el antiguo al terminar.

## 8. Verificación posterior a la instalación

1. Inicie sesión con el Administrador, cambie la contraseña y cree los usuarios de Seguridad, Custodios y Operadores.
2. Revise el historial de trabajos: `GET /api/v1/jobs/runs` (Administrador) debe mostrar ejecuciones `Succeeded` de `ExpirationMonitor`, `AlertEscalation`, `AccessWindow`, `AuditChainVerification` y `NotificationDispatch`.
3. Con un usuario de Seguridad, ejecute **Auditoría → Verificar integridad de la cadena**.
4. En QA, ejecute `tests/e2e/Invoke-ApiE2E.ps1` y `tests/e2e/Invoke-WebSmoke.ps1` sobre una base recién desplegada (ver la matriz de pruebas).

## 9. Entorno de desarrollo local

```powershell
cd database; .\deploy.ps1                                     # LocalDB (localdb)\MSSQLLocalDB
.\tools\New-PlatformVaultKek.ps1 -StoreLocation CurrentUser -BackupPath $env:USERPROFILE\PlatformVaultDev\kek-dev.pfx
dotnet user-secrets set "Database:ConnectionString" "Server=(localdb)\MSSQLLocalDB;Database=PlatformVault;Integrated Security=true;Encrypt=true;TrustServerCertificate=true" --project src/PlatformVault.Api
dotnet user-secrets set "KeyProtection:StoreLocation" "CurrentUser" --project src/PlatformVault.Api
dotnet user-secrets set "KeyProtection:ActiveThumbprint" "<huella>" --project src/PlatformVault.Api
dotnet user-secrets set "ApiClients:Clients:0:KeySha256" "<sha256 base64>" --project src/PlatformVault.Api
dotnet user-secrets set "ApiClients:Clients:0:AllowedNetworks:0" "127.0.0.1/32" --project src/PlatformVault.Api
dotnet user-secrets set "Api:Key" "<api key>" --project src/PlatformVault.Web
dotnet user-secrets set "Api:BaseUrl" "http://127.0.0.1:5080/" --project src/PlatformVault.Web
dotnet run --project src/PlatformVault.Api -- --bootstrap-admin admin "Administrador inicial"
dotnet run --project src/PlatformVault.Api --urls http://127.0.0.1:5080
dotnet run --project src/PlatformVault.Web --urls https://localhost:5443
```

Los secretos de desarrollo viven en `user-secrets` y en `%USERPROFILE%\PlatformVaultDev`, nunca en el repositorio.
