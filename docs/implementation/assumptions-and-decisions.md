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
| IMP-03 | Persistencia con Dapper y procedimientos almacenados exclusivamente. Sin EF Core, sin SQL dinámico y sin SQL embebido en C#. Cada repositorio llama solo a `CommandType.StoredProcedure`. | Prompt §2, DR-03 | Aplicada en el diseño |
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
| IMP-17 | **Un solo factor: usuario y contraseña.** Riesgo aceptado RA-01 (§3.1). | DR-02 | Política de contraseñas reforzada y re-autenticación con contraseña para acciones sensibles |
| IMP-18 | **Contenido cifrado en el esquema `vault`** de la misma base de datos, accesible solo desde procedimientos `vault.*`. Metadatos y valor cifrado se confirman en una sola transacción. | DR-04 | Desaparece RISK-C4-01 (escritura en dos bases) |
| IMP-19 | **KEK: se mantiene DEC-35** — certificado RSA-3072 no exportable en el almacén de la máquina; DEK AES-256-GCM por objeto, versión y componente, envuelta con RSA-OAEP-SHA256. | DR-05 | Servicio de cifrado con primitivas de .NET, sin algoritmos propios |
| IMP-20 | **Se retiran las cuentas de emergencia** (US-062, RN-123). Su política de contraseñas pasa a todas las cuentas locales. | DR-09 | Sin módulo de modo de emergencia |
| IMP-21 | **Alcance:** sección 4 del prompt más los controles de acceso y cifrado aprobados que no nombra. Resto a backlog (§5). | DR-10 | Ver §5 |

### 3.1 Riesgo aceptado RA-01 — Autenticación de un solo factor

| Campo | Valor |
|---|---|
| Descripción | Los usuarios se autentican solo con usuario y contraseña. No se cumple RNF-SEG-01 (MFA obligatorio). |
| Marco afectado | PCI DSS v4.0 8.4 (la plataforma está en el alcance por DEC-29, como sistema que afecta la seguridad del CDE); ISO 27001 A.8.5. |
| Probabilidad de hallazgo en auditoría | Alta. Conviene informar a Cumplimiento antes de la próxima evaluación con el QSA (DEC-34). |
| Controles compensatorios implementados | (1) Contraseña de 15 caracteres o más, con mayúsculas, minúsculas, dígitos y símbolos. (2) Bloqueo de 30 min tras 5 intentos fallidos. (3) Rotación obligatoria cada 90 días (PCI DSS 8.3.9, exigido precisamente cuando la contraseña es el único factor). (4) Historial de las 4 últimas contraseñas. (5) Re-autenticación con contraseña, con vigencia de 15 min, para revelar, descargar y aprobar (sustituye al *step-up* MFA de RNF-SEG-01). (6) Acceso a la API solo desde el servidor Web (API Key + IP/CIDR), nunca directo desde el navegador. (7) Autenticación exitosa y fallida auditada, con alerta a Seguridad ante intentos fallidos repetidos (RN-119). |
| Decidido por | Usuario, 2026-09-25 |
| Revisión | Antes de pasar a producción y en cada evaluación anual PCI DSS |

### 3.2 Decisión pendiente

| ID | Pregunta | Hallazgo | Bloquea |
|---|---|---|---|
| PEND-07 | Resto del prompt de implementación: el texto recibido termina en la sección 9. | DR-21 | Convenciones de base de datos, estrategia de pruebas y entregables de `/docs/delivery` |

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

**Retiradas** (no se implementarán): US-018, US-060, US-061 (DEC-35), US-026 (DR-01, sin Active Directory) y US-062 (IMP-20).
