# Supuestos y decisiones de implementación — PlatformVault Fase 1

| Atributo | Valor |
|---|---|
| Documento | assumptions-and-decisions.md |
| Fecha | 2026-09-25 |
| Relacionado | [drift-report.md](drift-report.md) |

Las decisiones de implementación se identifican como **IMP-NN**. Cada una indica su origen (decisión técnica del prompt, artefacto aprobado o hallazgo del drift report) y su estado.

## 1. Entorno verificado

| Elemento | Versión encontrada | Uso |
|---|---|---|
| .NET SDK | 10.0.401 | Compilación de todos los proyectos |
| DevExpress / DevExtreme | 26.1.4 (feed local `DevExpress 26.1 Local`) | `DevExtreme.AspNet.Core` en PlatformVault.Web; Dashboard y Reporting disponibles en la licencia |
| SQL Server LocalDB | SQL Server 2025 (`MSSQLLocalDB`) | Desarrollo y pruebas de integración |
| sqlcmd | ODBC 17 | Despliegue de scripts de `/database` |
| git | Disponible | Control de versiones |

## 2. Decisiones tomadas (interpretación inequívoca)

| ID | Decisión | Origen | Estado |
|---|---|---|---|
| IMP-01 | Solución `PlatformVault.sln` en formato clásico (`--format sln`), porque .NET 10 genera `.slnx` por defecto y el prompt pide `.sln`. | Prompt §6 | Aplicada |
| IMP-02 | Dependencias entre capas: Domain ← Application ← Infrastructure; Api → Application + Infrastructure; **Web → solo Api por HTTP** (sin referencia a ningún otro proyecto). Una prueba de arquitectura lo verifica. | Prompt §7 | Aplicada |
| IMP-03 | Persistencia **solo con Dapper** y procedimientos almacenados, en todas las fases. Sin EF Core, sin SQL dinámico y sin SQL embebido en C#. Cada repositorio llama solo a `CommandType.StoredProcedure`. | Prompt §2, DR-03, aclaración 2026-09-25 | Aplicada en el diseño |
| IMP-04 | Identidad local con ASP.NET Core Identity y *custom stores* sobre procedimientos almacenados. Hash de contraseñas con `PasswordHasher<T>` de Identity (PBKDF2-HMAC-SHA512, iteraciones por defecto de .NET 10). Sin criptografía propia. | Prompt §2, DR-01 | Aplicada en el diseño (un solo factor, IMP-17) |
| IMP-05 | Web → API: cabecera `X-API-Key` y lista de IP/CIDR permitidas, validadas en un middleware antes de cualquier *controller*. La API Key se guarda en la configuración del servidor Web (nunca en el navegador) y en la API solo su hash. | Prompt §2, §9, DR-06 | Aplicada en el diseño |
| IMP-06 | Sesión de usuario: token opaco de 256 bits emitido por la API en el login y enviado por Web en `X-User-Session`. La API guarda solo el hash SHA-256. Inactividad de 15 min, máximo de 8 h y revocación inmediata. | Prompt §9, RNF-SEG-06, DR-14 | Aplicada en el diseño |
| IMP-07 | Cookie de la Web: `HttpOnly`, `Secure`, `SameSite=Strict`, cifrada con ASP.NET Core Data Protection. Contiene la referencia a la sesión de la API, nunca la API Key. | Prompt §8, RNF-SEG-06 | Aplicada en el diseño |
| IMP-08 | Autorización en dos niveles dentro de Application: funcional (rol + permiso) y por objeto (ámbito, grupo, propiedad, RN-010). Cuando un objeto está fuera de ámbito se responde 404, nunca 403, y el intento se audita como posible IDOR/BOLA. | Prompt §7.2, RN-010 | Aplicada en el diseño |
| IMP-09 | Protección contra *mass assignment*: DTOs de entrada explícitos por caso de uso, con `JsonSerializerOptions.UnmappedMemberHandling = Disallow`. Un campo no permitido produce 400 y se audita. | Prompt §2, §4.6 | Aplicada en el diseño |
| IMP-10 | 5 tipos de objeto; API Key y OAuth Token como subtipos de Secret. | DR-08 | Aplicada |
| IMP-11 | Auditoría en tabla Ledger *append-only* con hash encadenado; escritura en la misma transacción que el cambio (fail-closed, RN-079). | RNF-AUD-02, DR-17 | Aplicada en el diseño |
| IMP-12 | Una única cuenta técnica SQL con `EXECUTE` por esquema y sin permisos sobre tablas. | Prompt §2, DR-18 | Aplicada en el diseño |
| IMP-13 | *Workers* (vencimientos, escalamiento, expiración de accesos temporales, verificación de la cadena de auditoría) como `IHostedService` en la API, con `sp_getapplock`. | c4 §3.2, DR-15 | Aplicada en el diseño |
| IMP-14 | Despliegue en IIS o como servicio de Windows, sin contenedores. | Prompt §2 | Aplicada en el diseño |
| IMP-15 | `ManagerUserId` local para el escalamiento N3. | DR-12 | Aplicada en el diseño |

## 3. Decisiones cerradas con el usuario (2026-09-25)

| ID | Decisión | Hallazgo | Efecto en la implementación |
|---|---|---|---|
| IMP-16 | Notificaciones por **SMTP** autenticado sobre TLS; **Teams por *incoming webhook***, opcional y deshabilitado por defecto. Graph descartado porque exige Entra ID. | DR-11 | Notification Adapter con dos canales configurables |
| IMP-17 | **Fase 1: un solo factor, usuario y contraseña. MFA en la Fase 2.** Riesgo aceptado temporal RA-01 (§3.1). | DR-02 | Política de contraseñas reforzada y re-autenticación con contraseña para acciones sensibles |
| IMP-18 | **Fase 1: contenido cifrado en el esquema `vault`** de la misma base de datos, accesible solo desde procedimientos `vault.*`; metadatos y valor cifrado en una sola transacción. **Fase 2: Encrypted Vault** como nodo separado. | DR-04 | En la Fase 1 desaparece RISK-C4-01 (escritura en dos bases); reaparece en la Fase 2 |
| IMP-19 | **KEK: se mantiene DEC-35** — certificado RSA-3072 no exportable en el almacén de la máquina; DEK AES-256-GCM por objeto, versión y componente, envuelta con RSA-OAEP-SHA256. | DR-05 | Servicio de cifrado con primitivas de .NET, sin algoritmos propios |
| IMP-20 | **Las cuentas de emergencia pasan a la Fase 2** (US-062, RN-123), junto con Entra ID. En la Fase 1 su política de contraseñas se aplica a todas las cuentas locales. | DR-09 | Sin módulo de modo de emergencia en la Fase 1 |
| IMP-21 | **Alcance:** sección 4 del prompt más los controles de acceso y cifrado aprobados que no nombra. Resto a backlog (§5). | DR-10 | Ver §5 |
| IMP-22 | **Identidad por fases:** Fase 1, usuarios locales con ASP.NET Core Identity; Fase 2, Microsoft Entra ID (SSO, MFA, acceso condicional) y Active Directory. | DR-01 | La capa de identidad se diseña detrás de interfaces para poder añadir Entra ID en la Fase 2 sin tocar Application |

### 3.1 Riesgo aceptado temporal RA-01 — Autenticación de un solo factor en la Fase 1

| Campo | Valor |
|---|---|
| Descripción | En la Fase 1 los usuarios se autentican solo con usuario y contraseña, sin cumplir RNF-SEG-01 (MFA obligatorio). El MFA llega en la Fase 2 con Entra ID. |
| Marco afectado | PCI DSS v4.0 8.4 (la plataforma está en el alcance por DEC-29, como sistema que afecta la seguridad del CDE); ISO 27001 A.8.5. |
| Probabilidad de hallazgo en auditoría | Alta. Conviene informar a Cumplimiento antes de la próxima evaluación con el QSA (DEC-34). |
| Controles compensatorios implementados | (1) Contraseña de 15 caracteres o más, con mayúsculas, minúsculas, dígitos y símbolos. (2) Bloqueo de 30 min tras 5 intentos fallidos. (3) Rotación obligatoria cada 90 días (PCI DSS 8.3.9, exigido precisamente cuando la contraseña es el único factor). (4) Historial de las 4 últimas contraseñas. (5) Re-autenticación con contraseña, con vigencia de 15 min, para revelar, descargar y aprobar (sustituye al *step-up* MFA de RNF-SEG-01). (6) Acceso a la API solo desde el servidor Web (API Key + IP/CIDR), nunca directo desde el navegador. (7) Autenticación exitosa y fallida auditada, con alerta a Seguridad ante intentos fallidos repetidos (RN-119). |
| Decidido por | Usuario, 2026-09-25 |
| Vigencia | Hasta la puesta en producción de la Fase 2 |
| Revisión | Antes de pasar a producción y en cada evaluación anual PCI DSS mientras siga vigente |

### 3.2 Resto del prompt (PEND-07)

**Resuelto con supuestos (2026-09-25).** El texto del prompt termina en la sección 9. El usuario aceptó trabajar con las recomendaciones de §6, que cubren lo que no llegó. Si aparece el resto del prompt, se compara con §6 y se ajusta.

## 4. Supuestos

| ID | Supuesto |
|---|---|
| SUP-01 | En desarrollo, la base de datos se despliega en LocalDB con los scripts de `/database`. QA, UAT y Producción usan SQL Server 2022 o superior con TDE. |
| SUP-02 | El certificado KEK de desarrollo es autofirmado y lo genera un script. Nunca se versiona. |
| SUP-03 | La licencia de DevExpress la instala el equipo en cada máquina (feed local). El repositorio no contiene claves ni credenciales del feed. |
| SUP-04 | Los artefactos aprobados de `/docs/specs`, `/docs/architecture` y `/docs/api` se corregirán según el drift report en un cambio explícito y posterior, una vez cerradas las decisiones pendientes. |

## 5. Alcance de la Fase 1 y backlog (IMP-21)

**Se implementa** la sección 4 del prompt: inventario, tipos, propiedad, accesos, vencimientos y alertas, auditoría, dashboards y reportes. **Además**, se implementan estos controles aprobados que la sección 4 no nombra, porque forman parte de «Aprobación», «Segregación de funciones» y «Autorización por objeto»:

| Control aprobado | Historias / reglas |
|---|---|
| Aprobación por Seguridad (titular o suplente) en objetos Críticos | US-033, RN-122 |
| Aprobación por par del grupo en objetos Restringidos no críticos | US-033, RN-106 |
| Grupos de acceso locales, responsables y grupos personales | US-027, US-028, RN-033 a RN-035, RN-103, RN-104, RN-108 |
| Llave dividida, componentes y custodios designados | US-056 a US-058, RN-111 a RN-118 |
| Segregación de funciones entre roles | US-030, RN-036 |
| Marca de objeto crítico y rebaja de criticidad con Seguridad | US-011, RN-107 |

**Backlog** (historias aprobadas y vigentes que no entran en esta entrega; se implementarán en una iteración posterior de la Fase 1):

| Historia | Motivo |
|---|---|
| US-012 Carga inicial CSV | No figura en la sección 4 del prompt |
| US-044 Reportes regulatorios y paquetes de evidencia | No figura en la sección 4 del prompt |
| US-047 Notificaciones por Teams (el correo sí se implementa) | Canal opcional (IMP-16) |
| US-050 Gestión de hallazgos | No figura en la sección 4 del prompt |
| US-052 Controles de cumplimiento | No figura en la sección 4 del prompt |
| US-053 Notificaciones de actividad de grupo | No figura en la sección 4 del prompt |
| US-054 Aviso a Seguridad | No figura en la sección 4 del prompt |
| US-055 Eliminación definitiva | El prompt pide desactivación lógica |
| US-059 Revisión diaria automatizada de seguridad | No figura en la sección 4; la consulta de auditoría sí se implementa |

**Fase 2** (aclaración del 2026-09-25): US-025 (SSO, MFA y acceso condicional con Entra ID), US-026 (validación de cuentas de servicio en Active Directory) y US-062 (cuentas de emergencia). También el Encrypted Vault como nodo separado (IMP-18).

**Retiradas** (no se implementarán): US-018, US-060 y US-061 (DEC-35).

## 6. Supuestos por ausencia del resto del prompt (aceptados el 2026-09-25)

Criterio general: simplificar la infraestructura y la operación, no los controles de seguridad.

### 6.1 Entrega en dos iteraciones

| ID | Decisión |
|---|---|
| IMP-23 | **Iteración 1A (núcleo):** usuarios, login y roles; inventario de los 5 tipos con cifrado; propietarios; grupos; solicitudes con aprobación de Seguridad (objetos críticos), de un par (objetos restringidos) o del propietario (objetos confidenciales); acceso temporal con revocación automática; auditoría; vencimientos y alertas por correo; dashboard básico. |
| IMP-24 | **Iteración 1B (endurecimiento):** llave dividida y custodios (US-056 a US-058), grupos personales (RN-108), reportes y exportaciones, rotación de la KEK con re-envoltura, cuatro ojos en la asignación de roles privilegiados (RN-038) y flujos de aprobación configurables de varios niveles (US-032). |

### 6.2 Seguridad de acceso

| ID | Decisión |
|---|---|
| IMP-25 | Contraseña de 15 caracteres o más con mayúsculas, minúsculas, dígitos y símbolos; bloqueo de 30 min tras 5 intentos; caducidad a los 90 días; historial de 4. Implementado con las opciones de ASP.NET Core Identity. |
| IMP-26 | Sesión: token aleatorio de 256 bits; la base guarda solo su hash SHA-256; 15 min de inactividad y 8 h como máximo. |
| IMP-27 | API Key entre Web y API: una clave en la configuración de la Web, protegida con DPAPI en los servidores; la API guarda solo su hash SHA-256 y compara en tiempo constante. Rotación manual cada 6 meses. |
| IMP-28 | Lista de IP/CIDR permitidas en `appsettings` de la API; en desarrollo, solo `127.0.0.1` y `::1`. |
| IMP-29 | Revelar, descargar y aprobar exigen haber reintroducido la contraseña en los últimos 15 min. |

### 6.3 Cifrado

| ID | Decisión |
|---|---|
| IMP-30 | AES-256-GCM (`System.Security.Cryptography.AesGcm`) con una DEK aleatoria por objeto y versión, envuelta con RSA-OAEP-SHA256 con el certificado KEK (DEC-35). El identificador del objeto y la versión se usan como datos asociados, para que un texto cifrado no pueda trasladarse a otro objeto. |
| IMP-31 | Un script de PowerShell genera el certificado de desarrollo y exporta el respaldo cifrado. **No se pasa a producción sin el respaldo del certificado verificado.** El diseño admite varios certificados (actual y anteriores) para la rotación de la iteración 1B. |

### 6.4 Base de datos

| ID | Decisión |
|---|---|
| IMP-32 | Una base `PlatformVault` con los esquemas `identity`, `app`, `vault`, `audit` y `report`. Scripts numerados e idempotentes (`IF NOT EXISTS` / `CREATE OR ALTER`), un archivo por objeto y un script maestro con sqlcmd. Sin herramienta de migraciones. |
| IMP-33 | Auditoría en tabla Ledger de solo inserción con hash SHA-256 encadenado. La cuenta técnica solo ejecuta procedimientos. |

### 6.5 Pruebas

| ID | Decisión |
|---|---|
| IMP-34 | Unitarias de dominio para todas las invariantes críticas; integración contra LocalDB (no Testcontainers, porque Docker está prohibido); pruebas de seguridad de la API (IDOR/BOLA, *mass assignment*, API Key, IP, sesión vencida, autoaprobación); prueba canario de no exposición; prueba de arquitectura de dependencias de la Web. |

### 6.6 Operación y entregables

| ID | Decisión |
|---|---|
| IMP-35 | API y Web como dos sitios de IIS en el mismo servidor Windows. *Workers* dentro de la API con `sp_getapplock`. Logs con Serilog a archivo, con Correlation ID y redacción de secretos. Health checks de base de datos y certificado. Correo por SMTP, apagado por defecto. |
| IMP-36 | `/docs/delivery`: manual de instalación, manual de operación y matriz de pruebas. |
| IMP-37 | Sin MediatR: desde la versión 13 exige licencia comercial. Se usan interfaces propias de *command* y *query handler* resueltas por inyección de dependencias. |
| IMP-38 | Los scripts y estilos de DevExtreme se copian al compilar desde la instalación local de DevExpress a `wwwroot/lib/devextreme` (excluido de git). No se versiona ningún archivo del proveedor. |

## 7. Decisiones tomadas durante la implementación de la iteración 1A (2026-09-25)

Estas decisiones surgieron al construir la iteración 1A. Las que afectaban al contrato de la API o a controles de acceso se revisaron con el usuario el 2026-09-25 (§7.4) y el contrato `docs/api/platform-vault-v1.yaml` se actualizó en consecuencia.

### 7.1 Contrato de la API (`platform-vault-v1.yaml`)

| ID | Decisión | Estado |
|---|---|---|
| IMP-39 | Autenticación de la Fase 1 con endpoints nuevos: `POST /auth/login`, `/auth/logout`, `/auth/reauthenticate`, `/auth/change-password`. Cabeceras `X-API-Key` (canal Web → API) y `X-User-Session` (token opaco). El esquema `entraIdAuth` del contrato queda para la Fase 2. | Aprobada (2026-09-25) |
| IMP-40 | `PATCH /objects/{id}` acepta además `expirationDate`, `noExpirationJustified` y `attributes`. Los bloques por tipo del contrato (`credential`, `certificate`, `secret`, `cryptographicKey`, `serviceAccount`) se representan en 1A como un diccionario plano `attributes` con las mismas claves (`targetSystem`, `accountName`, `directorySource`, `accountIdentifier`…). | Aprobada (2026-09-25) |
| IMP-41 | Campos del contrato no soportados en 1A que la API **rechaza con 400** (no se ignoran en silencio): `tags`, `applicationIds`, `splitKey`; en `CreateAccessRequest`, `jit`, `preferredApproverId` y `ticketReference`, y `objectIds` debe tener exactamente un elemento; en `UpdateGroupRequest`, `responsibles` y `notificationSettings` (los responsables se gestionan con `POST /groups/{id}/members`). `UpdateGroupRequest` acepta `name`, `description` y `status`. | Aprobada (2026-09-25) |
| IMP-42 | **Alineado con el contrato**: `POST /objects/{id}/download?part=PublicCertificate\|PrivateKeyPfx\|KeyMaterial&temporaryAccessId=`. El certificado público (DER, guardado por versión en `app.ObjectCertificate`) solo requiere Consultar y se audita. El cuerpo opcional `{ downloadPassword }` protege el PKCS#12 entregado (mínimo 12 caracteres). | Aprobada y modificada (2026-09-25) |
| IMP-43 | `POST /users/{id}/roles` recibe solo `role`; el `scope` del contrato se deriva del área del usuario en la Fase 1. | Aprobada (2026-09-25) |
| IMP-44 | Endpoints adicionales: `GET /users/lookup`, `POST /users`, `PUT /users/{id}`, `POST /users/{id}/reset-password`, `POST /users/{id}/unlock`, `GET/POST /areas`, `GET /catalog/subtypes`, `GET /objects/{id}/ownership-history`, `PUT /expiration-policies/{id}`, `GET /jobs/runs`. Concurrencia optimista con `ETag`/`If-Match` en todas las modificaciones de objetos. | Aprobada (2026-09-25) |
| IMP-45 | Acciones de acceso con los valores del contrato `AccessActionType`: `Reveal` (texto), `DownloadPrivateKey` (PKCS#12) y `DownloadKeyMaterial` (material de clave). `ModifyValue` y `Delete` no requieren solicitud en 1A. | Aplicada |

### 7.2 Controles de acceso

| ID | Decisión | Estado |
|---|---|---|
| IMP-46 | **Custodio u Operador** registran objetos, solo en su área (decisión del usuario; amplía la matriz §4.2, que no daba Crear al Operador). El Propietario, rol derivado, no registra objetos en la Fase 1. | Aprobada y modificada (2026-09-25) |
| IMP-58 | Consecuencia de IMP-46: el Operador no tiene visibilidad por área (RN-010), así que al registrar un objeto **debe figurar como propietario funcional o técnico**; si no, la API responde 422. Evita ampliar el modelo de visibilidad. | Aplicada (propuesta; el usuario puede pedir otra opción) |
| IMP-47 | Seguridad no solicita accesos en 1A: no puede ser miembro ni propietario (RN-103) y la nota ⁵ (solo incidentes, con cuatro ojos) pasa a 1B. | Aprobada (2026-09-25) |
| IMP-48 | Grupos personales (RN-108) en 1B: en 1A un grupo de un solo miembro no exime de aprobación (los Confidenciales los aprueba el propietario). | Aprobada (2026-09-25) |
| IMP-49 | El inicio de sesión cuenta como re-autenticación (IMP-29). La Web pide siempre la contraseña antes de revelar, descargar o aprobar. | Aprobada (2026-09-25) |
| IMP-50 | La marca «Sin vencimiento» la ponen Custodio o Propietario; la aprobación de Seguridad de RN-063 pasa a 1B (en objetos Críticos se avisa a Seguridad al instante). | Aprobada (2026-09-25) |
| IMP-51 | Aviso a Seguridad (RN-110): inmediato para objetos Críticos; el resumen diario de los no críticos pasa a 1B. | Aplicada |
| IMP-52 | Las alertas de severidad Crítica escalan con el plazo de Alta (24 h); RN-070 no fija un plazo propio para Crítica. | Aplicada |

### 7.3 Operación y seguridad técnica

| ID | Decisión |
|---|---|
| IMP-53 | Primer Administrador con `PlatformVault.Api --bootstrap-admin <usuario> "<nombre>"`: solo funciona si no hay ningún Administrador activo, se audita y muestra una contraseña temporal una sola vez. Las contraseñas temporales las genera el sistema con un generador criptográfico. |
| IMP-54 | PKCS#12 (RN-015): se abre con su contraseña solo en memoria, se re-exporta sin contraseña y se cifra con la KEK. |
| IMP-55 | Web: CSP con *nonce* por petición (DevExtreme `AddCspNonce`); estilos en línea permitidos porque DevExtreme los requiere. Cookie `__Host-pv` (HttpOnly, Secure, SameSite=Strict) cifrada con Data Protection y claves protegidas con DPAPI. CSRF global. |
| IMP-56 | Notificaciones en cola (`app.Notification`) dentro de la transacción; con SMTP apagado se marcan `Suppressed`. |
| IMP-57 | Riesgo menor conocido: si ASP.NET Core Identity recalcula el hash de una contraseña por cambio de algoritmo, la fecha de cambio se actualiza y reinicia el plazo de 90 días de esa contraseña. |
| IMP-59 | Parámetros de los listados alineados con el contrato: `q` (en lugar de `text`), `status`, `role`, `subtype`, `groupId`, `expiresFrom`/`expiresTo`, `orphanOwner`, `objectId` (alertas) y `correlationId` (auditoría). Los filtros aún no disponibles (`applicationId`, `insecureConfiguration`, `noUsage`, `splitKey`, `includeDeleted`, `isPersonal`) responden 400. Las modificaciones de objetos devuelven `ObjectWriteResult` y las reglas de negocio responden 422 con `code`. |

| IMP-60 | **Defecto corregido en la verificación final:** el límite de intentos de `/auth/*` se aplicaba por IP de origen y, como todas las peticiones llegan desde el servidor Web, 20 inicios de sesión por minuto de cualquier usuario bloqueaban a todos. Ahora la Web envía la IP del navegador en `X-Client-Ip`; la API solo la acepta tras validar la API Key y la red, y la usa para el límite por usuario final y para la auditoría (antes registraba la IP del servidor Web). Límite configurable `Security:LoginAttemptsPerMinutePerClient` (20 por defecto). |
| IMP-61 | **Unificación de roles (2026-09-26):** el rol Operador se elimina y sus usuarios pasan a Custodio (migración idempotente en `07-SeedData/700-Roles.sql`). El Custodio ya no ve por área: ve los objetos de los que es propietario, los que registró (`ManagedObject.CreatedBy`) y los asignados a un grupo activo del que es miembro (`app.ufn_VisibleObjects`, `ObjectAuthorizer.HasScopedAccess`). Sobre esos objetos conserva todos los permisos del Custodio. Sigue registrando objetos solo en su área y ya no necesita figurar como propietario. El nivel N3 de escalamiento avisa al jefe del propietario funcional y a los Responsables de los grupos del objeto (antes, custodios del área). Sustituye a IMP-46. |

### 7.4 Revisión con el usuario (2026-09-25)

| Decisión | Resultado |
|---|---|
| IMP-39, IMP-40/41, IMP-43/44 | Aprobadas y documentadas en el contrato. Los campos no soportados se marcan con `x-not-supported-in`. |
| IMP-42 | Se alinea con el contrato (`?part=`) y se añade la descarga del certificado público. |
| IMP-46 | Cambio: el Operador también registra objetos en su área (con IMP-58). |
| IMP-47, IMP-48, IMP-49/50 | Aprobadas sin cambios. |
| IMP-61 | Cambio: Custodio y Operador se unifican en Custodio con visibilidad por grupo y por objetos registrados (sustituye a IMP-46). |
