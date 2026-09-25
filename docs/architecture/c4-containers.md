# Arquitectura C4 — Plataforma de Gobierno de Credenciales, Certificados y Secretos (PGCCS)

| Atributo | Valor |
|---|---|
| Documento | c4-containers.md |
| Versión | 1.0 |
| Fecha | 2026-09-24 |
| Rol autor | Software Architect (.NET, DDD, C4, CQRS) |
| Fuente | [docs/specs/functional/](../specs/functional/) |
| Documentos relacionados | [domain-model.md](domain-model.md), [ADR-001-layered-cqrs-architecture.md](ADR-001-layered-cqrs-architecture.md) |

**Decisión de notación:** los diagramas usan `flowchart`/`sequenceDiagram` de Mermaid en lugar de la sintaxis experimental `C4Context`/`C4Container`, para garantizar que rendericen igual en cualquier visor de Markdown compatible con Mermaid estándar. Los niveles C4 (Contexto, Contenedores, Componentes) se mantienen como estructura del documento y como subgrafos etiquetados dentro de cada diagrama.

---

## 1. Nivel 1 — System Context

### 1.1 Actores y sistemas externos

| Actor / Sistema | Tipo | Relación con la plataforma | Referencia |
|---|---|---|---|
| Administrador, Custodio, Propietario Funcional/Técnico, Operador, Auditor, Seguridad | Persona | Usan el Front-End con su identidad de Entra ID (o cuenta de emergencia, US-062) | 01 §4 |
| Custodios designados de componente (grupo) y de Seguridad | Persona (rol operativo, no de sistema) | Ingresan/revelan su parte de una llave dividida | US-057, US-058 |
| Microsoft Entra ID | Sistema externo | SSO, MFA, acceso condicional, identidad de aplicaciones (F2) | RN-030, RNF-SEG-01 |
| Active Directory | Sistema externo | LDAPS de solo lectura, validación de cuentas de servicio | DEC-06 |
| Correo corporativo / Microsoft Teams | Sistema externo | Canal de notificaciones | DEC-07 |
| SIEM corporativo | Sistema externo (F2) | Recepción de eventos de seguridad | DEC-08 |
| Aplicaciones consumidoras | Sistema externo (F2) | Recuperan secretos por API con identidad de aplicación | DEC-03 |
| Auditor externo / QSA | Persona externa | Recibe paquetes de evidencia exportados (US-044) | RNF-CUM-04 |

### 1.2 Diagrama de contexto

```mermaid
flowchart TB
    subgraph Personas["Usuarios corporativos"]
        ADM[Administrador]
        CUS[Custodio]
        PROP[Propietario Funcional/Técnico]
        OPE[Operador]
        AUD[Auditor]
        SEG[Seguridad de la Información]
    end

    PGCCS(("Plataforma de Gobierno de<br/>Credenciales, Certificados y Secretos<br/>(PGCCS)"))

    ENTRA[[Microsoft Entra ID]]
    AD[[Active Directory]]
    MAIL[[Correo corporativo]]
    TEAMS[[Microsoft Teams]]
    SIEM[[SIEM corporativo — F2]]
    APPS[[Aplicaciones consumidoras — F2]]
    QSA[Auditor externo / QSA]

    Personas -->|HTTPS, SSO + MFA| PGCCS
    PGCCS -->|OIDC/OAuth2| ENTRA
    PGCCS -->|LDAPS, solo lectura| AD
    PGCCS -->|SMTP/Graph| MAIL
    PGCCS -->|Graph/webhook| TEAMS
    PGCCS -.->|Syslog/CEF — F2| SIEM
    APPS -.->|REST, identidad de aplicación — F2| PGCCS
    PGCCS -->|Exporta evidencias| QSA
```

---

## 2. Nivel 2 — Containers

### 2.1 Distribución física obligatoria

```mermaid
flowchart TB
    subgraph Z1["Zona 1 — Front-End (DMZ interna)"]
        WEB["Sitio Web<br/>ASP.NET Core MVC + DevExpress"]
    end
    subgraph Z2["Zona 2 — Back-End (red de aplicaciones)"]
        API["API REST PGCCS<br/>.NET 10"]
        KS[("Almacén de certificados local<br/>KEK no exportable — DEC-35")]
    end
    subgraph Z3["Zona 3 — Base de datos (red restringida)"]
        DB[("SQL Server<br/>metadatos, configuración, auditoría")]
    end
    subgraph Z4["Zona 4 — Storage seguro (red restringida, exclusiva)"]
        VAULT[("Encrypted Vault<br/>SQL Server dedicado — payload cifrado")]
    end
    subgraph Z5["Zona 5 — Nube pública (salida controlada, solo identidad y mensajería)"]
        ENTRA[(Microsoft Entra ID)]
        M365[(Microsoft 365 — correo/Teams)]
    end
    subgraph Z6["Zona 6 — Red corporativa interna"]
        AD[(Active Directory)]
    end

    WEB -->|HTTPS REST, único canal| API
    API -->|TDS/TLS| DB
    API -->|TDS/TLS, cuenta exclusiva| VAULT
    API -->|DPAPI-NG / CNG, local al host| KS
    API -->|OIDC/TLS| ENTRA
    API -->|LDAPS| AD
    API -->|SMTP/Graph, TLS| M365

    WEB -.->|prohibido| DB
    WEB -.->|prohibido| VAULT
    WEB -.->|prohibido| KS
```

**Cumplimiento de las validaciones obligatorias 6 y 7:** el Front-End (Zona 1), el Back-End (Zona 2), la Base de datos (Zona 3) y el Storage seguro (Zona 4) son cuatro nodos físicos/lógicos separados, cada uno en su propio segmento de red. El Front-End solo tiene una arista de salida: hacia el Back-End por HTTPS REST. No existe ninguna ruta entre el Front-End y SQL Server, el Encrypted Vault o el almacén de certificados que custodia la KEK (líneas punteadas "prohibido" documentadas explícitamente para la revisión de arquitectura y para las reglas del firewall/NSG). La plataforma no depende de ningún servicio de nube para su custodia criptográfica (DEC-35): la KEK reside en el almacén de certificados de la máquina de cada nodo de la API.

### 2.2 Contenedores

| Contenedor | Responsabilidad | Tecnología | Datos administrados | Dependencias | Protocolo | Límite de confianza | Controles de seguridad | Escalabilidad/Disponibilidad | Clasificación |
|---|---|---|---|---|---|---|---|---|---|
| **Sitio Web** (Front-End) | Presentación, formularios de registro/consulta/aprobación, grillas DevExpress, dashboards | ASP.NET Core MVC (.NET 10), DevExpress | Ninguno persistente propio; solo estado de sesión de presentación | API REST PGCCS, Entra ID (login redirect) | HTTPS/TLS 1.3 | DMZ interna | CSP estricta, anti-CSRF, cookies `Secure/HttpOnly/SameSite=Strict`, sin acceso a BD, Vault ni almacén de llaves (R-07) | Sin estado (stateless) tras Entra ID; N instancias detrás de balanceador (RNF-DIS-03) | Ninguna (no persiste sensibles) |
| **API REST PGCCS** (Back-End) | Casos de uso, reglas de negocio, autorización, orquestación de cifrado, workflows de aprobación, auditoría | .NET 10, ASP.NET Core Web API, Entity Framework Core | Ninguno propio; orquesta SQL Server + Encrypted Vault; usa la KEK del almacén de certificados local | SQL Server, Encrypted Vault, almacén de certificados local (KEK), Entra ID, Active Directory, Notificaciones | HTTPS/TLS 1.3 (interno TLS 1.2 AEAD si aplica) | Red de aplicaciones (interna) | OAuth2/OIDC, RBAC por endpoint, rate limiting, WAF por delante, RFC 7807 en errores (RNF-SEG-08) | Sin estado, N instancias + workers en background separados (RNF-REN-08) | Restringida (orquesta todo) |
| **SQL Server** (Base de datos) | Metadatos de objetos, configuración, usuarios/roles/grupos, solicitudes/aprobaciones, alertas, historial, auditoría append-only | SQL Server 2022+ on-premise, Always On AG | Metadatos, `VaultBlobId` (puntero, nunca el valor), auditoría, configuración | Ninguna saliente | TDS sobre TLS | Red restringida (Zona 3) | TDE, sin endpoint público, cuenta de aplicación de mínimo privilegio, Ledger tables *append-only* para auditoría (RNF-AUD-02) | Always On AG síncrono local + asíncrono a sitio DR (RNF-DIS-03/04) | Confidencial (metadatos); nunca texto plano de secretos (R-05) |
| **Encrypted Vault** (Storage seguro) | Persistencia exclusiva del contenido cifrado de los objetos administrados: `SensitivePayloadRecord`, `KeyComponent`, blobs PFX, versiones históricas cifradas | SQL Server 2022+ dedicado (instancia/base separada de la transaccional), o filegroup aislado con TDE + cifrado a nivel de columna | Ciphertext (AES-256-GCM) por objeto/versión/componente; nunca claves en claro | Ninguna saliente propia; la KEK que envuelve sus DEK es un certificado local en el almacén de la máquina de la API (DEC-35) | TDS sobre TLS, cuenta de servicio exclusiva y distinta de la de SQL Server transaccional | Red restringida y exclusiva (Zona 4), sin conectividad directa desde el Front-End ni desde ningún cliente que no sea la API | TDE + cifrado de sobre, *soft-delete* a nivel de aplicación (no delete físico salvo Purga, RN-109), auditoría de acceso vía la propia API | Always On AG propio, réplica a sitio DR sincronizada con la de SQL Server transaccional | Restringida/Crítica — el activo más sensible del sistema |
| **Almacén de certificados local (KEK)** | Custodia de la Key Encryption Key (KEK) de la plataforma y de las credenciales técnicas propias de la plataforma (cadenas de conexión protegidas con DPAPI) | Windows Certificate Store (almacén de máquina), certificado RSA-3072+ no exportable, protegido por DPAPI-NG/CNG y TPM cuando el hardware lo soporta | KEK (clave privada del certificado), credenciales técnicas cifradas de la plataforma | — | Local al host (API de CNG/DPAPI), sin red | Dentro del host de la API (Zona 2); solo la cuenta de servicio de la API tiene permiso de lectura de la clave privada | Clave no exportable, ACL restringida a la cuenta de servicio, rotación anual con re-envoltura de DEK, respaldo cifrado fuera de línea custodiado por Seguridad (RNF-SEG-04) | El mismo certificado se instala en cada nodo de la API y en el sitio DR mediante el procedimiento de respaldo (RNF-DIS-04) | Crítica (protege la KEK de todo el sistema) |
| **Microsoft Entra ID** | Autenticación SSO/MFA, acceso condicional, identidad de usuarios y de aplicaciones (F2) | Servicio gestionado Microsoft | Identidades, tokens | — | OIDC/OAuth 2.0 sobre TLS | Nube pública | MFA obligatorio, acceso condicional, tokens de corta duración (RNF-SEG-01/06) | Gestionado por Microsoft | N/A (no custodia datos de la plataforma) |
| **Active Directory** | Validación de existencia y estado de cuentas de servicio (US-026) | AD on-premise existente del banco | Cuentas de servicio (solo lectura) | — | LDAPS (636) | Red corporativa interna | Cuenta de solo lectura y mínimo privilegio, TLS obligatorio | Alta disponibilidad propia del AD del banco | N/A (solo lectura) |
| **Servicio de Notificaciones** (módulo dentro de la API) | Envío de correos y mensajes de Teams; colas y reintentos | Módulo .NET dentro de la API + Microsoft Graph / SMTP | Cola de notificaciones pendientes (SQL Server) | Correo corporativo, Teams | SMTP/Graph sobre TLS | Red de aplicaciones | Sin Sensitive Payloads en el cuerpo (RN-067, RN-085), reintentos con backoff (RN-099) | Cola persistente, procesamiento por worker | Interna (metadatos de notificación) |
| **Integraciones externas (F2)** | SIEM y recuperación de secretos por aplicaciones | Módulos .NET dentro de la API | — | SIEM, aplicaciones consumidoras | Syslog/CEF (TLS), REST | Frontera F1/F2 | Deshabilitado en F1 por *feature flag* (DEC-03, DEC-08) | — | — |

**[SUPUESTO ASSUMPTION-ARCH-01]** — repetido aquí por ser una decisión de contenedores: la especificación funcional describe el modo de custodia "Interno" como cifrado local con una KEK en un certificado de la máquina (DEC-01, DEC-35). Este documento **refina** esa decisión, sin contradecirla, separando físicamente el contenido cifrado (Encrypted Vault) de los metadatos transaccionales (SQL Server), tal como lo exige explícitamente el encargo de arquitectura ("Storage seguro" como nodo #4, distinto de la "Base de datos" #3). Ver el detalle en [domain-model.md §17](domain-model.md#17-supuestos-riesgos-y-decisiones-pendientes).

---

## 3. Nivel 3 — Components (Back-End)

### 3.1 Vista de componentes

```mermaid
flowchart TB
    subgraph API["API REST PGCCS (.NET 10)"]
        subgraph Presentation["Capa de presentación"]
            CTRL[API Controllers / Minimal API Endpoints]
        end
        subgraph Application["Capa de aplicación — CQRS"]
            CMD[Application Commands]
            QRY[Application Queries]
            CH[Command Handlers]
            QH[Query Handlers]
        end
        subgraph Domain["Capa de dominio"]
            DS[Domain Services<br/>Authorization, PolicyResolution,<br/>ApproverResolution, SoD, Expiration,<br/>SplitKey, RiskIndex, ComplianceEval]
            AGG[Agregados<br/>ManagedObject, AccessRequest,<br/>TemporaryAccess, Alert, SecurityGroup…]
        end
        subgraph Infra["Capa de infraestructura"]
            REPO[Repositories]
            UOW[Unit of Work]
            ENC[Encryption Service<br/>envelope encryption]
            VLT[Vault Service<br/>persistencia en Encrypted Vault]
            AUD[Audit Service<br/>append-only + hash chain]
            ALSVC[Alert Service]
            EXPSVC[Expiration Service]
            APRSVC[Approval Workflow Service]
            NOT[Notification Adapter]
            IDENT[Entra ID / AD Adapter]
            KP[Local Key Protector<br/>certificado de máquina]
        end
        subgraph Workers["Background Workers (hosted services)"]
            W1[ExpirationMonitorWorker]
            W2[EscalationWorker]
            W3[DailySecurityReviewWorker]
            W5[AuditChainVerificationWorker]
            W6[ComplianceEvaluationWorker]
            W7[NotificationDispatchWorker]
            W8[TemporaryAccessExpiryWorker]
        end
    end

    CTRL --> CMD
    CTRL --> QRY
    CMD --> CH
    QRY --> QH
    CH --> DS
    CH --> AGG
    QH --> REPO
    DS --> AGG
    CH --> UOW
    UOW --> REPO
    UOW --> AUD
    REPO --> ENC
    REPO --> VLT
    VLT --> ENC
    ENC --> KP
    DS --> IDENT
    APRSVC --> NOT
    ALSVC --> NOT
    Workers --> CH
    Workers --> REPO
```

### 3.2 Descripción de componentes

| Componente | Capa | Responsabilidad | Reglas / historias |
|---|---|---|---|
| **API Controllers / Endpoints** | Presentación | Exponen los recursos REST versionados (`/api/v1/...`), documentados con OpenAPI 3.x; traducen HTTP ↔ Commands/Queries; no contienen lógica de negocio | US-046, RNF-MAN-03 |
| **Application Commands** | Aplicación | DTO de intención de cambio (`RegisterCertificateCommand`, `SubmitAccessRequestCommand`, `RevealSecretCommand`…), validados con FluentValidation antes de llegar al handler | Todas las US de escritura |
| **Application Queries** | Aplicación | DTO de consulta (`SearchManagedObjectsQuery`, `GetOperationalDashboardQuery`…), resueltos contra modelos de lectura optimizados (vistas SQL o proyecciones EF `AsNoTracking`) | US-009, US-010, US-041 a US-045 |
| **Command Handlers** | Aplicación | Orquestan: cargan el agregado vía Repository, invocan Domain Services, aplican el cambio, registran el evento de auditoría dentro de la misma transacción (Unit of Work), publican notificaciones | RN-079 (fail-closed) |
| **Query Handlers** | Aplicación | Ejecutan proyecciones de solo lectura respetando el ámbito del usuario (RN-010); nunca devuelven Sensitive Payload (RN-086) | RN-010, RN-043 |
| **Domain Services** | Dominio | Ver [domain-model.md §11](domain-model.md#11-servicios-de-dominio) | — |
| **Authorization Service** | Dominio/Infra (política evaluada en dominio, contexto de usuario provisto por infraestructura) | Calcula permiso efectivo por rol + ámbito + grupo + SoD en cada Command/Query Handler, denegación por defecto | RN-010, RN-036, RN-040 a RN-043 |
| **Vault Service** | Infraestructura | Persiste y recupera el Sensitive Payload cifrado (pieza única o componentes de llave dividida) en el Encrypted Vault. Los objetos en modo Solo metadatos no tienen payload (RN-023) | RN-023, RN-083 |
| **Encryption Service** | Infraestructura | Implementa cifrado de sobre: genera la DEK AES-256-GCM por objeto/versión/componente y la envuelve/desenvuelve con la KEK local a través del Local Key Protector; nunca persiste la KEK ni la DEK en claro | RN-083, RNF-SEG-03/04 |
| **Expiration Service** | Infraestructura (orquesta) + Dominio (`ExpirationCalculationService`) | Recalcula `ExpirationStatus`, aplica la Expiration Policy más específica | RN-061, RN-062, RN-094 |
| **Alert Service** | Infraestructura (orquesta) | Crea/actualiza `Alert`, aplica idempotencia por umbral (RN-064), invoca Notification Adapter | RN-064 a RN-071 |
| **Approval Workflow Service** | Infraestructura (orquesta) + Dominio (`ApproverResolutionService`) | Resuelve el flujo de aprobación aplicable y el pool de aprobadores elegibles; bloquea autoaprobación | RN-047 a RN-055, RN-106, RN-122 |
| **Audit Service** | Infraestructura | Único escritor autorizado de `AuditEvent`; calcula el hash encadenado; expone solo `Append` y `Query`, nunca `Update`/`Delete` | RN-075 a RN-079 |
| **Repositories** | Infraestructura | Un repositorio por agregado (ver §5 de domain-model.md); ocultan EF Core; no exponen `IQueryable` fuera de la capa de infraestructura para las escrituras | RNF-MAN-01 |
| **Unit of Work** | Infraestructura | Envuelve un `DbContext` de EF Core; garantiza que el cambio de dominio y el `AuditEvent` se confirmen en la misma transacción SQL Server (fail-closed, RN-079) | RN-079 |
| **Notification Adapter** | Infraestructura | Envía correo (SMTP/Graph) y Teams; aplica reintentos con backoff; nunca incluye Sensitive Payload | RN-067, RN-085, RN-099 |
| **Entra ID / AD Adapter** | Infraestructura | Valida tokens OIDC, resuelve claims de MFA y acceso condicional; LDAPS de solo lectura para cuentas de servicio | RN-030 a RN-032, DEC-06 |
| **Local Key Protector** | Infraestructura | Único componente con acceso a la clave privada del certificado KEK en el almacén de la máquina (`X509Store` + RSA-OAEP vía CNG); expone solo `Wrap(dek)`/`Unwrap(wrappedDek)`; soporta varias versiones de certificado para la rotación sin indisponibilidad | DEC-35, RNF-SEG-04 |
| **ExpirationMonitorWorker** | Background | Ejecuta periódicamente el recálculo de `ExpirationStatus` y dispara alertas | RNF-REN-05 |
| **EscalationWorker** | Background | Evalúa plazos de reconocimiento y escala | RN-069 a RN-071 |
| **DailySecurityReviewWorker** | Background | Genera el reporte diario y evalúa reglas de detección | RN-119, US-059 |
| **AuditChainVerificationWorker** | Background | Verifica el encadenamiento hash cada 24 h y bajo demanda | RN-076 |
| **ComplianceEvaluationWorker** | Background | Ejecuta diariamente las fórmulas de `ComplianceControl` | RN-102 |
| **NotificationDispatchWorker** | Background | Procesa la cola de notificaciones pendientes | RN-099 |
| **TemporaryAccessExpiryWorker** | Background | Revoca accesos vencidos con desfase ≤ 60 s | RN-057 |

Todos los *workers* corren dentro del mismo proceso de la API como `IHostedService` para la primera versión (ver [ADR-001](ADR-001-layered-cqrs-architecture.md)), con bloqueo distribuido (`sp_getapplock` de SQL Server) para evitar ejecuciones duplicadas al escalar horizontalmente (RNF-REN-08).

---

## 4. Diagrama de despliegue físico

```mermaid
flowchart TB
    subgraph DC1["Centro de Datos Principal (on-premise)"]
        LB1[Balanceador de carga]
        subgraph WebFarm1["Granja Web"]
            W1n1[Sitio Web #1]
            W1n2[Sitio Web #2]
        end
        subgraph ApiFarm1["Granja API"]
            A1n1[API #1 + Workers]
            A1n2[API #2 + Workers]
        end
        SQL1[(SQL Server<br/>nodo primario Always On)]
        VLT1[(Encrypted Vault<br/>nodo primario Always On)]
        KS1[(Almacén de certificados<br/>KEK en cada nodo API)]
    end
    subgraph DC2["Centro de Datos Alterno (DR)"]
        LB2[Balanceador standby]
        WebFarm2[Sitio Web standby]
        ApiFarm2[API standby]
        SQL2[(SQL Server<br/>réplica asíncrona)]
        VLT2[(Encrypted Vault<br/>réplica asíncrona)]
        KS2[(Almacén de certificados<br/>KEK restaurada por DR)]
    end
    subgraph Cloud["Microsoft Entra ID (salida controlada)"]
        ENTRA[(Microsoft Entra ID)]
    end

    LB1 --> W1n1 & W1n2
    W1n1 & W1n2 --> A1n1 & A1n2
    A1n1 & A1n2 --> SQL1
    A1n1 & A1n2 --> VLT1
    A1n1 & A1n2 -->|local| KS1
    A1n1 & A1n2 --> ENTRA
    SQL1 -.->|Always On asíncrono| SQL2
    VLT1 -.->|Always On asíncrono| VLT2
    KS1 -.->|respaldo cifrado fuera de línea, RNF-DIS-04| KS2
```

## 5. Zonas de confianza

```mermaid
flowchart LR
    subgraph TZ0["No confiable"]
        Browser[Navegador del usuario]
    end
    subgraph TZ1["Confianza baja — DMZ interna"]
        WEB[Sitio Web]
    end
    subgraph TZ2["Confianza media — Red de aplicaciones"]
        API[API REST PGCCS]
        KS[(Almacén de certificados local — KEK)]
    end
    subgraph TZ3["Confianza alta — Red restringida de datos"]
        DB[(SQL Server)]
        VAULT[(Encrypted Vault)]
    end
    subgraph TZ4["Externa confiable — Identidad"]
        ENTRA[(Entra ID)]
    end

    Browser -->|TLS 1.3| WEB
    WEB -->|TLS 1.3, JWT| API
    API -->|TLS interno, cuenta de aplicación| DB
    API -->|TLS interno, cuenta exclusiva| VAULT
    API -->|CNG local, ACL de cuenta de servicio| KS
    API -->|OIDC/TLS| ENTRA
```

Cada flecha cruza exactamente un límite de confianza y exige su propio control (autenticación de usuario en TZ0→TZ1, token de aplicación en TZ1→TZ2, credencial de servicio de mínimo privilegio en TZ2→TZ3, validación de token OIDC en TZ2→TZ4). El acceso a la KEK no cruza ningún límite de red: ocurre dentro del host de la API y está restringido por la ACL del certificado a la cuenta de servicio de la plataforma.

## 6. Flujo de autenticación y autorización

```mermaid
sequenceDiagram
    actor U as Usuario
    participant WEB as Sitio Web
    participant ENTRA as Microsoft Entra ID
    participant API as API REST PGCCS
    participant AUTH as Authorization Service
    participant DB as SQL Server

    U->>WEB: Accede a la aplicación
    WEB->>ENTRA: Redirige (Authorization Code + PKCE)
    U->>ENTRA: Autentica + MFA
    ENTRA-->>WEB: Código de autorización
    WEB->>ENTRA: Intercambia por tokens (id_token, access_token)
    WEB->>API: Llama con access_token (Bearer)
    API->>ENTRA: Valida firma/emisor/audiencia/claims MFA
    API->>DB: Resuelve roles y grupos del usuario
    API->>AUTH: ¿Permiso efectivo para (acción, recurso)?
    AUTH-->>API: Permitido / Denegado
    alt Denegado
        API-->>WEB: 403 + AuditEvent(Denied)
    else Permitido
        API-->>WEB: 200 + datos (sin Sensitive Payload salvo endpoint de revelado)
    end
```

## 7. Flujo de consulta de un secreto (revelado)

```mermaid
sequenceDiagram
    actor U as Usuario (custodio designado o beneficiario)
    participant API as API REST PGCCS
    participant AUTH as Authorization Service
    participant TA as TemporaryAccess
    participant VLT as Vault Service
    participant EVLT as Encrypted Vault
    participant KP as Local Key Protector
    participant AUD as Audit Service

    U->>API: POST /objects/{id}/reveal (MFA reciente ≤15min)
    API->>AUTH: ¿Tiene Temporary Access activo o grupo personal aplicable?
    AUTH-->>API: Permitido
    API->>TA: Verifica ventana (StartAt ≤ ahora ≤ EndAt)
    TA-->>API: Vigente
    API->>VLT: Obtener valor (modo Interno)
    VLT->>EVLT: Lee CipherText (y KeyComponent si hay llave dividida)
    VLT->>KP: Unwrap(DEK envuelta)
    KP-->>VLT: DEK en claro (transitoria, en memoria)
    VLT-->>API: Valor descifrado (o solo el componente del actor)
    par En la misma transacción
        API->>AUD: append(SECRET_REVEALED, correlación)
    and
        API->>API: Registra UsageEvent
    end
    alt Falla el registro de auditoría
        API-->>U: Rechaza la operación (fail-closed, RN-079) — no se entrega el valor
    else Auditoría exitosa
        API-->>U: Valor (máx. 30 s en pantalla, RN-084)
    end
```

## 8. Flujo de almacenamiento de un objeto sensible

```mermaid
sequenceDiagram
    actor C as Custodio
    participant API as API REST PGCCS
    participant DOM as ManagedObject (dominio)
    participant ENC as Encryption Service
    participant KP as Local Key Protector
    participant EVLT as Encrypted Vault
    participant DB as SQL Server
    participant AUD as Audit Service

    C->>API: POST /objects (metadatos + valor)
    API->>API: CardDataGuardService.Validate (RN-124, sin PAN)
    API->>DOM: Create(metadatos, propietarios, clasificación)
    DOM-->>API: Invariantes OK (RN-001 a RN-011, RN-087)
    API->>ENC: Cifrar valor (DEK nueva)
    ENC->>KP: Wrap(DEK) con la KEK local
    KP-->>ENC: DEK envuelta
    par Una sola transacción SQL Server
        API->>DB: Persiste metadatos + versión 1 + DEK envuelta (puntero)
        API->>EVLT: Persiste CipherText en el Encrypted Vault
        API->>AUD: append(OBJECT_CREATED)
    end
    alt Cualquier paso falla
        API-->>C: Rollback total, ningún dato queda a medias
    else Éxito
        API-->>C: 201 Created (OBJ-NNNNNN)
    end
```

## 9. Flujo de generación de alertas de vencimiento

```mermaid
sequenceDiagram
    participant W as ExpirationMonitorWorker
    participant EXP as Expiration Service
    participant EP as ExpirationPolicy
    participant DB as SQL Server
    participant ALS as Alert Service
    participant NOT as Notification Adapter
    participant AUD as Audit Service

    loop cada ejecución programada
        W->>DB: Selecciona objetos Activos/Suspendidos
        W->>EXP: Recalcula ExpirationStatus por objeto
        EXP->>EP: Resuelve política más específica (RN-062)
        EP-->>EXP: Umbrales activos
        alt Umbral alcanzado y sin alerta previa (RN-064)
            EXP->>ALS: CrearAlerta(objeto, umbral, severidad)
            ALS->>DB: Persiste Alert
            ALS->>NOT: Notificar a N1 (Propietario Técnico)
            ALS->>AUD: append(ALERT_CREATED)
        end
    end
```

## 10. Tabla de comunicaciones, puertos lógicos y protocolos

| Origen | Destino | Puerto lógico | Protocolo | Autenticación | Cifrado |
|---|---|---|---|---|---|
| Navegador | Sitio Web | 443 | HTTPS | Sesión Entra ID (cookie de sesión de presentación) | TLS 1.3 |
| Sitio Web | API REST PGCCS | 443 | HTTPS/REST | Bearer token OIDC (Entra ID) | TLS 1.3 |
| API REST PGCCS | Microsoft Entra ID | 443 | HTTPS/OIDC | Client credentials / validación de token | TLS 1.3 |
| API REST PGCCS | Active Directory | 636 | LDAPS | Cuenta de servicio de solo lectura | TLS (LDAPS) |
| API REST PGCCS | SQL Server | 1433 | TDS | Cuenta de aplicación con permisos mínimos | TLS 1.2 AEAD (interno) |
| API REST PGCCS | Encrypted Vault | 1433 (instancia dedicada) | TDS | Cuenta de servicio exclusiva, distinta de la anterior | TLS 1.2 AEAD (interno) |
| API REST PGCCS | Almacén de certificados local (KEK) | — (local, sin red) | CNG / DPAPI-NG | ACL del certificado restringida a la cuenta de servicio de la API | Clave privada no exportable |
| API REST PGCCS | Correo corporativo | 443/587 | Microsoft Graph / SMTP autenticado | OAuth2 (Graph) o credenciales SMTP | TLS 1.3/1.2 |
| API REST PGCCS | Microsoft Teams | 443 | Graph / webhook | OAuth2 (Graph) | TLS 1.3 |
| API REST PGCCS | SIEM corporativo (F2) | 6514 (syslog-tls) | Syslog/CEF sobre TLS | Certificado mutuo (a definir en F2) | TLS |
| Aplicaciones consumidoras (F2) | API REST PGCCS | 443 | HTTPS/REST | Certificado de cliente (Service Principal) | TLS 1.3 |

## 11. Riesgos arquitectónicos y mitigaciones

| ID | Riesgo | Impacto | Mitigación |
|---|---|---|---|
| RISK-C4-01 | Un solo `DbContext`/transacción que abarca SQL Server + Encrypted Vault (dos instancias físicamente separadas) no puede ser una transacción distribuida ACID nativa sin `MSDTC`, que añade complejidad operativa. | Alto | Usar un patrón de **commit en dos fases aplicativo simplificado**: escribir primero en el Encrypted Vault (operación idempotente, con `VaultBlobId` determinístico) y confirmar la fila de metadatos en SQL Server solo después; si falla el segundo paso, un *reconciliador* de arranque limpia blobs huérfanos. Documentar como decisión técnica a validar en el diseño detallado (no altera las historias de usuario). |
| RISK-C4-02 | La KEK es un certificado local: si se pierde el almacén de certificados de los nodos de la API (fallo de disco, reinstalación del servidor) sin un respaldo válido, todo el contenido cifrado queda irrecuperable. | Alto | Respaldo cifrado y fuera de línea del certificado, custodiado por Seguridad, incluido en el runbook de DR y probado en el simulacro anual (RNF-DIS-04, RNF-DIS-07); instalación del mismo certificado en todos los nodos y en el sitio alterno; rotación anual con período de convivencia de versiones (RNF-SEG-04). Sustituye al antiguo riesgo de dependencia de Azure Key Vault, retirado por DEC-35. |
| RISK-C4-03 | Los *workers* de background compiten por recursos con la API si corren en el mismo proceso al escalar a muchas instancias. | Medio | Bloqueo distribuido (`sp_getapplock`) ya mitiga duplicidad; si el volumen crece (ver horizonte de 200 000 objetos), extraer los *workers* a un proceso `Worker Service` .NET separado, sin cambiar el modelo de dominio ni las historias — ver [ADR-001 §Evolución futura](ADR-001-layered-cqrs-architecture.md). |
| RISK-C4-04 | DevExpress en el Front-End puede inducir a construir lógica de negocio o consultas directas a datos en el cliente. | Medio | Regla de arquitectura: todo *data source* de un componente DevExpress se alimenta exclusivamente de un endpoint de la API (paginado/filtrado en servidor); prohibido cualquier `SqlDataSource` o acceso directo. |
| RISK-C4-05 | El Encrypted Vault como base SQL Server "dedicada" separada de la transaccional duplica el esfuerzo operativo (parches, backups, HA) respecto de tenerlo todo en una sola base. | Bajo/Medio | Aceptado conscientemente: es exactamente lo que exige el encargo de arquitectura (nodo físico #4 distinto del #3) y refuerza la segmentación de red (RNF-SEG-11). Se documenta en [domain-model.md ASSUMPTION-ARCH-01](domain-model.md#17-supuestos-riesgos-y-decisiones-pendientes). |

## 12. Matriz de trazabilidad (arquitectura)

| Requisito / Historia | Necesidad funcional | Elemento del dominio | Contenedor / Componente responsable | Control de seguridad | Evidencia arquitectónica |
|---|---|---|---|---|---|
| R-07, US-046 | El Front-End no accede a datos directamente | ManagedObject y todos los agregados | Sitio Web → API REST PGCCS (único canal) | Segmentación de red, sin credenciales de BD en el Front-End | §2.1 Distribución física, §5 Zonas de confianza |
| RF-PRT-01, US-015 | Cifrado de la información sensible en reposo | SensitivePayloadRecord | Encrypted Vault + Encryption Service + Local Key Protector | Envelope encryption AES-256-GCM, KEK en certificado local no exportable (DEC-35) | §8 Flujo de almacenamiento |
| RN-042, US-016 | Revelado solo con acceso temporal aprobado | TemporaryAccess, SensitivePayloadRecord | Authorization Service, Vault Service, Audit Service | Fail-closed (RN-079), auditoría por revelado | §7 Flujo de consulta de un secreto |
| RN-075, US-037 | Bitácora inmutable | AuditEvent | Audit Service, SQL Server (Ledger tables) | Append-only, hash encadenado | §3.2 Audit Service |
| RF-VEN-01 a 04, US-020 a US-024 | Vencimientos, alertas, escalamiento | ExpirationPolicy, Alert | ExpirationMonitorWorker, EscalationWorker, Alert Service | Idempotencia por umbral (RN-064) | §9 Flujo de generación de alertas |
| RN-113 a RN-118, US-057, US-058 | Llave dividida y custodios designados | KeyComponent, SensitivePayloadRecord | Encryption Service, Vault Service, Encrypted Vault | Conocimiento dividido, sin valor completo expuesto | domain-model.md §11 SplitKeyService |
| DEC-35, RNF-SEG-04 | Custodia criptográfica local, sin servicios de nube | Sensitive Payload (DEK) | Local Key Protector, almacén de certificados de la máquina | Certificado no exportable, ACL a la cuenta de servicio, respaldo cifrado fuera de línea | §2.1 Distribución física, §4 Despliegue, RISK-C4-02 |
| RNF-MAN-03, US-046 | API documentada con OpenAPI | — | API Controllers / Endpoints | Contrato versionado `/api/v1` | §3.2 |
| RNF-SEG-08 | Autenticación por certificado para aplicaciones (F2) | — | Entra ID Adapter | Sin *client secrets* en producción | §10 Tabla de comunicaciones |
