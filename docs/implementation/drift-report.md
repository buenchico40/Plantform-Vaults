# Drift Report — Implementación de la Fase 1 de PlatformVault

| Atributo | Valor |
|---|---|
| Documento | drift-report.md |
| Fecha | 2026-09-25 |
| Fuente de autoridad | Prompt maestro de implementación, §3 «Orden de autoridad de los artefactos» |
| Artefactos revisados | `docs/specs/functional/01..05`, `docs/architecture/domain-model.md`, `c4-containers.md`, `ADR-001-layered-cqrs-architecture.md`, `docs/api/platform-vault-v1.yaml` |
| Estado | Decisiones cerradas el 2026-09-25, salvo DR-21 (resto del prompt) |

## Cómo leer este documento

Cada hallazgo indica la contradicción, los artefactos afectados, el impacto, la corrección propuesta y la acción:

- **Implementar:** la interpretación es inequívoca porque la decisión técnica obligatoria (autoridad 1) prevalece. Se implementa según el prompt y el artefacto aprobado se corrige de forma explícita, no silenciosa.
- **Requiere decisión:** afecta a cifrado, recuperación de claves, contratos de API o controles de acceso, o su interpretación no es inequívoca. No se implementa hasta que se decida (§3.6 del prompt).

Ningún artefacto aprobado se ha modificado todavía. Las correcciones propuestas se aplicarán en un cambio posterior y trazable, una vez cerradas las decisiones.

## Resumen

| Severidad | Cantidad | Implementar | Requiere decisión |
|---|---|---|---|
| Alta | 9 | 3 (DR-01, DR-06, DR-07) | 5 decididos (DR-02, DR-04, DR-05, DR-09, DR-10) · 1 abierto (DR-21) |
| Media | 7 | 6 | 1 decidido (DR-11) |
| Baja | 5 | 5 | 0 |
| **Total** | **21** | **14** | **6 decididos · 1 abierto** |

---

## Hallazgos

### DR-01 · Identidad: se retiran Microsoft Entra ID y Active Directory — **Alta · Implementar**

| | |
|---|---|
| Contradicción | El prompt exige usuarios administrados dentro de PlatformVault, con usuario y contraseña, y prohíbe Entra ID, AD y LDAP. Los artefactos aprobados hacen de Entra ID la única fuente de identidad. |
| Artefactos afectados | 01 R-03, S-01, S-07, DEC-06, DEC-33; 02 US-025, US-026, US-062; 03 RN-030, RN-031, RN-032, RN-069 (N3 = manager en Entra ID), RN-123, §2.6 (`DirectorySource`), §2.7; 04 R-03, RNF-SEG-01, RNF-SEG-06, RNF-SEG-15; c4 §1, §2, §6; OpenAPI `securitySchemes` |
| Impacto | US-025 se reescribe para autenticación local. US-026 (validación de cuentas de servicio en AD) queda sin objeto. El «propietario inválido» de RN-031 pasa a depender de la baja del usuario en la plataforma, no en Entra ID. El manager que usa RN-069 debe ser un atributo local del usuario. |
| Corrección propuesta | Implementar ASP.NET Core Identity con stores personalizados sobre procedimientos almacenados. Añadir `ManagerUserId` al usuario local. Retirar US-026 y DEC-06. `DirectorySource` de Service Account se conserva como dato descriptivo (EntraID, AD, Local, Database), sin integración. |
| Acción | Implementar. La decisión técnica 1 prevalece. |

### DR-02 · MFA obligatorio sin proveedor de identidad externo — **Alta · Decidido**

| | |
|---|---|
| Contradicción | RNF-SEG-01 exige MFA para todo acceso y una autenticación reciente (≤ 15 min) para las acciones sensibles. PCI DSS 8.4 aplica porque la plataforma está en el alcance (DEC-29). El prompt especifica usuario y contraseña, pero no menciona MFA. |
| Artefactos afectados | 04 RNF-SEG-01, RNF-SEG-15, §6.2 (PCI DSS 8.3/8.4); 02 US-016 (MFA reciente para revelar), US-025; 03 RN-032 |
| Impacto | Sin MFA, la plataforma que custodia credenciales del entorno de tarjetas queda protegida por un único factor, lo que previsiblemente genera un hallazgo del QSA. Afecta directamente a controles de acceso. |
| Corrección propuesta | Segundo factor TOTP (RFC 6238) con el proveedor de autenticador integrado en ASP.NET Core Identity (sin criptografía propia). Obligatorio para todos los usuarios, con re-autenticación por TOTP en las acciones sensibles (step-up ≤ 15 min). |
| Acción | **Decidido (2026-09-25): un solo factor, usuario y contraseña. Riesgo aceptado** frente a RNF-SEG-01 y PCI DSS 8.4, registrado como RA-01 en assumptions-and-decisions.md. Controles compensatorios: contraseña de ≥ 15 caracteres, bloqueo de 30 min tras 5 intentos, rotación cada 90 días (PCI DSS 8.3.9), historial de 4 contraseñas y re-autenticación con contraseña (≤ 15 min) para revelar, descargar y aprobar. |

### DR-03 · Persistencia con Dapper y procedimientos almacenados, sin EF Core — **Media · Implementar**

| | |
|---|---|
| Contradicción | La arquitectura menciona Entity Framework Core, `DbContext`, proyecciones `AsNoTracking` y un Unit of Work sobre EF. El prompt exige Dapper y solo procedimientos almacenados. |
| Artefactos afectados | c4 §2.2 (tecnología de la API), §3.2 (Repositories, Unit of Work); ADR-001 §Aclaración sobre CQRS (punto 1), §Justificación (punto 8) |
| Impacto | Ninguno funcional. El Unit of Work se implementa sobre `SqlConnection` + `SqlTransaction`. Las consultas del lado Query son procedimientos dedicados. |
| Corrección propuesta | Implementar con Dapper. Actualizar c4 y ADR-001 para sustituir EF Core por Dapper + procedimientos almacenados. |
| Acción | Implementar. |

### DR-04 · Encrypted Vault como nodo separado frente a SQL Server con una única cuenta técnica — **Alta · Decidido**

| | |
|---|---|
| Contradicción | ASSUMPTION-ARCH-01 separa físicamente el contenido cifrado (Encrypted Vault: base dedicada, red propia, cuenta de servicio exclusiva) de los metadatos. El prompt exige guardar el contenido cifrado en SQL Server y usar una única cuenta técnica SQL. |
| Artefactos afectados | domain-model §17 ASSUMPTION-ARCH-01, RISK-ARCH-02; c4 §2.1 (Zona 4), §2.2 (contenedor Encrypted Vault), §4, §5, RISK-C4-01, RISK-C4-05; ADR-001 R4 |
| Impacto | Con una sola cuenta SQL desaparece la separación de credenciales entre metadatos y contenido cifrado. Se pierde parte de la defensa en profundidad, aunque el contenido sigue cifrado con la KEK. |
| Corrección propuesta | **Opción A (recomendada):** misma base de datos, esquema dedicado `vault`, accesible solo desde procedimientos del esquema `vault`, con permisos por esquema. Una transacción ACID única elimina RISK-C4-01. **Opción B:** base de datos dedicada en la misma instancia, con la misma cuenta, lo que exige coordinar dos conexiones y reintroduce RISK-C4-01. |
| Acción | **Decidido (2026-09-25): opción A.** Esquema `vault` en la misma base de datos, accesible solo desde sus procedimientos almacenados. |

### DR-05 · Custodia de la KEK — **Alta · Decidido**

| | |
|---|---|
| Contradicción | DEC-35 establece la KEK como certificado RSA-3072 no exportable en el almacén de certificados de Windows. El prompt pide un «servicio de cifrado» y «protección de claves» sin detallar el mecanismo. La sección del prompt que probablemente lo detalla no llegó (ver DR-21). |
| Artefactos afectados | 01 DEC-35; 04 RNF-SEG-03, RNF-SEG-04, RNF-DIS-04; c4 Local Key Protector, RISK-C4-02 |
| Impacto | Define cómo se recupera el contenido cifrado ante un desastre. |
| Corrección propuesta | Mantener DEC-35: cifrado de sobre con DEK AES-256-GCM (`System.Security.Cryptography.AesGcm`) y KEK en certificado de máquina, envolviendo la DEK con RSA-OAEP-SHA256. En entornos de desarrollo, certificado autofirmado generado por script. Todo con primitivas de .NET, sin criptografía propia. |
| Acción | **Decidido: se mantiene DEC-35** (artefacto aprobado; no es una decisión nueva). KEK en certificado de máquina. |

### DR-06 · Contrato de autenticación de la API — **Alta · Implementar**

| | |
|---|---|
| Contradicción | El OpenAPI define `entraIdAuth` (OAuth 2.0 Authorization Code) y `applicationCredentials`. El prompt exige API Key + IP/CIDR entre Web y API, y un token opaco de sesión (`X-User-Session`) para el usuario final. |
| Artefactos afectados | OpenAPI `security`, `components.securitySchemes`; c4 §6 (flujo de autenticación), §10 (tabla de comunicaciones) |
| Impacto | Cambia la seguridad de las 76 operaciones del contrato. |
| Corrección propuesta | Sustituir `entraIdAuth` por dos esquemas `apiKey`: `X-API-Key` (cliente Web) y `X-User-Session` (sesión del usuario), ambos requeridos. Documentar la restricción por IP/CIDR en la descripción del contrato. El esquema F2 `applicationCredentials` queda fuera de la Fase 1. |
| Acción | Implementar. La decisión técnica 1 lo impone de forma explícita; el cambio de contrato se registra aquí y en el propio OpenAPI. |

### DR-07 · Faltan en el contrato las operaciones de autenticación y administración de usuarios — **Alta · Implementar**

| | |
|---|---|
| Contradicción | Al pasar a usuarios locales hacen falta login, logout, renovación y cierre de sesión, cambio y restablecimiento de contraseña, alta, baja y bloqueo de usuarios, y enrolamiento del segundo factor. El OpenAPI no tiene ninguna, porque delegaba en Entra ID. |
| Artefactos afectados | OpenAPI (tag Identity) |
| Impacto | Sin ellas no se puede operar la plataforma. |
| Corrección propuesta | Añadir `/auth/login`, `/auth/logout`, `/auth/session`, `/auth/password`, `/users` (POST/PATCH, bloqueo y desbloqueo, restablecimiento de contraseña) y `/auth/mfa/*` (según DR-02). |
| Acción | Implementar, como consecuencia directa de DR-01. El diseño del segundo factor depende de DR-02. |

### DR-08 · Tipos de objeto: ApiKey y Token — **Media · Implementar**

| | |
|---|---|
| Contradicción | §4.2 del prompt enumera ApiKey y Token como tipos, pero exige implementar solo los tipos del modelo aprobado y no crear agregados independientes cuando exista una jerarquía común. En el modelo aprobado, API Key y OAuth Token son **subtipos de Secret**. |
| Artefactos afectados | 03 §2.4; domain-model §10 (`ObjectType`); OpenAPI `ObjectType` |
| Corrección propuesta | 5 tipos (Certificate, CryptographicKey, Secret, Credential, ServiceAccount); API Key y OAuth Token como subtipos de Secret, filtrables por subtipo. |
| Acción | Implementar. La propia regla del prompt resuelve la ambigüedad. |

### DR-09 · Cuentas locales de emergencia (US-062, DEC-33) — **Alta · Decidido**

| | |
|---|---|
| Contradicción | US-062 existe para acceder cuando Entra ID no responde. Si todas las cuentas son locales, ese escenario desaparece y la historia pierde su motivo. |
| Artefactos afectados | 01 DEC-33, R-03; 02 US-062; 03 RN-123; 04 RNF-SEG-15; OpenAPI `/emergency-accounts*` |
| Impacto | Afecta a controles de acceso (activación conjunta de Administrador y Seguridad, ventana de 8 h). |
| Corrección propuesta | Retirar US-062 y RN-123 como se hizo con DEC-35, y trasladar la política de contraseñas de RNF-SEG-15 a todas las cuentas locales. |
| Acción | **Decidido (2026-09-25): se retiran US-062 y RN-123.** Su política de contraseñas se aplica a todas las cuentas locales. |

### DR-10 · Alcance de la Fase 1: el prompt enumera menos historias que las aprobadas — **Alta · Decidido**

| | |
|---|---|
| Contradicción | La especificación aprobada tiene 59 historias vigentes de Fase 1. La sección 4 del prompt enumera un subconjunto y no menciona, entre otras: llave dividida y custodios (US-056 a US-058), eliminación definitiva (US-055), notificaciones de actividad de grupo (US-053), aviso a Seguridad (US-054), revisión diaria de seguridad (US-059), controles de cumplimiento (US-052), paquetes de evidencia (US-044), hallazgos (US-050), carga CSV (US-012), grupos personales y aprobación por par o por Seguridad (RN-106, RN-108, RN-122). |
| Impacto | Varias de esas historias son **controles de acceso o de cifrado** (llave dividida, aprobación de Seguridad en objetos críticos, grupos personales). Omitirlas debilitaría controles aprobados. |
| Corrección propuesta | Implementar la sección 4 del prompt **más** los controles de acceso y cifrado aprobados que la sección 4 no nombra, porque forman parte de «Aprobación», «Segregación de funciones» y «Autorización por objeto». Registrar el resto como backlog con su historia de origen. |
| Acción | **Decidido (2026-09-25):** sección 4 del prompt más los controles de acceso y cifrado aprobados que no nombra (aprobación por Seguridad o par según criticidad, grupos personales, llave dividida y custodios, segregación de funciones). El resto queda como backlog trazado en assumptions-and-decisions.md §5. |

### DR-11 · Notificaciones por correo y Teams sin Entra ID — **Media · Decidido**

| | |
|---|---|
| Contradicción | DEC-07 prevé correo y Teams mediante Microsoft Graph, que requiere un registro de aplicación en Entra ID, prohibido por el prompt. |
| Artefactos afectados | 01 DEC-07; 02 US-047, US-053, US-054; c4 Notification Adapter |
| Corrección propuesta | Correo por SMTP autenticado sobre TLS; Teams mediante *incoming webhook* o flujo de Workflows (sin Entra ID). Ambos configurables y deshabilitados por defecto. |
| Acción | **Decidido como IMP-16:** SMTP ahora; Teams por *incoming webhook*, opcional y deshabilitado por defecto. Graph queda descartado porque exige Entra ID. |

### DR-12 · Escalamiento N3 basado en el manager de Entra ID — **Media · Implementar**

| | |
|---|---|
| Contradicción | RN-069 define N3 como «Custodio del área y responsable jerárquico (manager en Entra ID)». |
| Corrección propuesta | Atributo local `ManagerUserId` en el usuario (ver DR-01). |
| Acción | Implementar. |

### DR-13 · Aprovisionamiento just-in-time del usuario — **Media · Implementar**

| | |
|---|---|
| Contradicción | 03 §2.7 aprovisiona al usuario en su primer inicio de sesión a partir de los claims de Entra ID. |
| Corrección propuesta | Alta explícita de usuarios por el Administrador, con la cuatro ojos que RN-038 ya exige para roles privilegiados. |
| Acción | Implementar. |

### DR-14 · Token de sesión opaco frente a tokens OIDC — **Media · Implementar**

| | |
|---|---|
| Contradicción | RNF-SEG-06 habla de tokens de acceso de ≤ 60 min emitidos por Entra ID. |
| Corrección propuesta | Token opaco aleatorio (256 bits, `RandomNumberGenerator`), del que solo se guarda el hash SHA-256. Inactividad de 15 min, duración máxima de 8 h y revocación inmediata al bloquear o dar de baja al usuario. Se conservan los límites de RNF-SEG-06. |
| Acción | Implementar. |

### DR-15 · Workers en segundo plano y despliegue sin contenedores — **Baja · Implementar**

| | |
|---|---|
| Contradicción | Ninguna real: c4 ya ubica los *workers* como `IHostedService` en el proceso de la API. El prompt exige Windows sin contenedores. |
| Corrección propuesta | Api y Web alojadas en IIS o como servicio de Windows. *Workers* dentro de la API, con `sp_getapplock` para evitar ejecuciones duplicadas. |
| Acción | Implementar. |

### DR-16 · TDE en entornos de desarrollo — **Baja · Implementar**

| | |
|---|---|
| Contradicción | RNF-SEG-03 (b) exige TDE. LocalDB, usado en desarrollo y pruebas, no lo soporta. |
| Corrección propuesta | TDE obligatorio en QA, UAT y Producción mediante el script `08-Security`; en desarrollo se documenta la excepción. El contenido sensible sigue cifrado a nivel de aplicación en todos los entornos. |
| Acción | Implementar. |

### DR-17 · Inmutabilidad de la auditoría con Ledger — **Baja · Implementar**

| | |
|---|---|
| Contradicción | Ninguna. RNF-AUD-02 admite tablas Ledger *append-only* o un mecanismo equivalente. |
| Corrección propuesta | Tabla `audit.AuditEvent` como `LEDGER = ON (APPEND_ONLY = ON)` más el encadenamiento de hash de RN-076 en el procedimiento de inserción. La cuenta técnica solo tiene `EXECUTE` sobre procedimientos, nunca `UPDATE`/`DELETE` sobre tablas. |
| Acción | Implementar. |

### DR-18 · Una única cuenta técnica SQL y permisos — **Media · Implementar**

| | |
|---|---|
| Contradicción | RNF-AUD-05 separa lógicamente el almacén de auditoría de los administradores de la plataforma. |
| Corrección propuesta | La cuenta técnica solo recibe `EXECUTE` por esquema (`app`, `vault`, `audit`, `identity`, `report`) y ningún permiso directo sobre tablas. El acceso directo a tablas queda reservado a los DBA, y su actividad la registra SQL Server Audit. |
| Acción | Implementar. |

### DR-19 · Front-End ASP.NET Core MVC con DevExtreme — **Baja · Implementar**

| | |
|---|---|
| Contradicción | Ninguna. c4 ya fija ASP.NET Core MVC con DevExpress. Versión disponible: DevExpress/DevExtreme 26.1.4, con Dashboard y Reporting incluidos en la licencia local. |
| Acción | Implementar con `DevExtreme.AspNet.Core` 26.1.4 desde el feed local. Ninguna clave ni credencial del feed se versiona. |

### DR-20 · Cuentas de servicio de Entra ID en el inventario — **Baja · Implementar**

| | |
|---|---|
| Contradicción | Ninguna real. Registrar como objeto una cuenta cuyo `DirectorySource` es Entra ID o AD es un dato de inventario, no una integración. |
| Acción | Implementar sin integración. |

### DR-21 · El prompt de implementación llegó incompleto — **Alta · Requiere decisión**

| | |
|---|---|
| Contradicción | El texto recibido termina en la sección 9 («Arquitectura de autenticación»), en medio de un bloque de código. Las secciones posteriores (probablemente cifrado, base de datos, pruebas y entregables de `/docs/delivery`) no llegaron. |
| Impacto | No se conocen requisitos que pueden afectar a cifrado, convenciones de base de datos, estrategia de pruebas y criterios de entrega. |
| Acción | Requiere el resto del prompt. |
