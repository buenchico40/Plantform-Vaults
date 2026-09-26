# PlatformVault · Matriz de pruebas (Fase 1, iteración 1A)

Destinatarios: QA, Seguridad de la Información y auditoría interna.

Resultado de la última ejecución (2026-09-25, LocalDB SQL Server 2025, .NET 10.0.401): **122 pruebas automatizadas, 0 fallos**; prueba de extremo a extremo de la API **44/44**; prueba de humo de la Web **20/20**.

## 1. Cómo ejecutar

| Suite | Comando | Requisitos |
|---|---|---|
| Unitarias y de arquitectura | `dotnet test PlatformVault.sln` | Ninguno para Domain, Application, Api y Web. |
| Integración | Incluida en `dotnet test` (`PlatformVault.Infrastructure.Tests`) | LocalDB o `PV_TEST_SERVER`. Despliega la base `PlatformVaultIntegrationTests` con `database/deploy.ps1`; `PV_TEST_SKIP_DEPLOY=1` reutiliza una ya desplegada. |
| Extremo a extremo de la API | `tests/e2e/Invoke-ApiE2E.ps1 -AdminTempPassword <temporal>` | Base recién desplegada, primer Administrador creado con `--bootstrap-admin` y API en `http://127.0.0.1:5080`. |
| Humo de la Web | `tests/e2e/Invoke-WebSmoke.ps1` | Tras la prueba anterior; Web en `https://localhost:5443`. |
| Validación de la base | Al final de `database/deploy.ps1` | — |

## 2. Controles de seguridad obligatorios

| Control | Evidencia automatizada |
|---|---|
| Solo la API conecta con SQL Server; la Web no depende de Infrastructure, Dapper ni SqlClient | `ArchitectureTests.Web_project_does_not_reference_data_or_security_layers` |
| Acceso a SQL solo por procedimientos; sin SQL embebido ni dinámico; sin EF Core | `ArchitectureTests.Csharp_contains_no_embedded_or_dynamic_sql_IMP03`, `Every_database_call_targets_a_stored_procedure`, `No_entity_framework_anywhere` |
| La cuenta técnica no lee ni escribe tablas | `database/10-Validation/1002-PermissionProbe.sql` |
| Web → API con API Key e IP/CIDR | `ApiClientTests` (6 casos); E2E «sin API Key responde 401» |
| Token de sesión opaco, hash en base, inactividad 15 min, revocación | `SessionTokenTests`, `RepositoryIntegrationTests.Session_expires_after_idle_timeout_and_revocation_IMP26`; E2E «sesión revocada tras cerrar sesión» |
| Autorización funcional y por objeto; IDOR/BOLA responde 404 | `ObjectAuthorizationTests` (9); E2E «usuario fuera de ámbito recibe 404», «búsqueda fuera de ámbito vacía»; `Database_visibility_filter_blocks_out_of_scope_users_RN010` |
| Mass assignment | E2E «mass assignment rechazado (400)» (y auditoría `Api.MassAssignmentRejected`) |
| Contenido cifrado; solo texto cifrado en el esquema vault | `EnvelopeEncryptionTests` (5), `Vault_stores_only_ciphertext_IMP18`; E2E «el detalle nunca contiene el valor», «la auditoría no contiene el valor» |
| Contraseñas con hash no reversible y política | `Temporary_passwords_meet_policy_IMP25`; E2E «contraseña débil rechazada», «login con contraseña temporal exige cambio» |
| Sin algoritmos criptográficos propios | Revisión de código: solo `AesGcm`, `RSA` OAEP-SHA256, `SHA256`, `RandomNumberGenerator`, PBKDF2 de Identity |
| Front-End: sin secretos en HTML/JS, sin tokens en el navegador, CSRF, CSP | `WebSecurityTests` (6), `Browser_code_never_stores_secrets_or_tokens`; humo Web «el HTML no contiene la API Key», «cookie HttpOnly y Secure», «POST sin token CSRF rechazado», «CSP con nonce» |
| Auditoría de solo inserción, encadenada y fail-closed | `1001-LedgerAppendOnly.sql`, `Audit_chain_is_verified_and_append_only_RN075_RN076`, `Audit_is_rolled_back_with_the_business_change_RN079`; E2E «cadena de auditoría íntegra» |
| Términos prohibidos no usados en el código | `ArchitectureTests.Forbidden_domain_terms_are_not_used` |

## 3. Reglas de negocio

| Regla | Pruebas |
|---|---|
| RN-002, RN-004, RN-013, RN-019, RN-124 (catálogo, sensibilidad, atributos, SWIFT, datos de tarjeta) | `ObjectRulesTests` (8 casos + 7 del detector); E2E «PAN en metadatos rechazado» |
| RN-005, RN-018 (duplicados) | `Duplicate_names_are_detected_RN005`; E2E «nombre duplicado (409)» |
| RN-007, RN-009 (ciclo de vida y revocación) | `Lifecycle_transitions_RN007`; E2E «suspender revoca accesos», «tras suspender no se revela» |
| RN-016 (expiración del certificado) | `Certificate_expiration_cannot_be_edited_RN016`, `CertificateInspectorTests` (4) |
| RN-036 a RN-038, RN-103 (segregación de funciones) | `SegregationOfDutiesTests` (6); E2E «SoD», «autoasignación», «Seguridad no puede ser miembro» |
| RN-045, RN-047 a RN-051, RN-054 (solicitudes y aprobación) | `AccessRulesTests` (10); E2E solicitud, aprobador por propietario, «autoaprobación rechazada», «duración mayor a la máxima» |
| RN-104 (grupo de dos miembros) | `Critical_or_restricted_needs_group_with_two_members_RN104`; E2E «crítico sin grupo de 2 miembros no se activa» |
| RN-107, RN-122 (críticos aprobados por Seguridad) | `Only_security_downgrades_a_critical_object_RN107`, `Only_security_approves_critical_access_RN122`; E2E «un par no aprueba un crítico», «Seguridad aprueba el crítico» |
| RN-061 a RN-071 (vencimientos, alertas y escalamiento) | `ExpirationRulesTests` (12) |
| RN-092 (versionado y concurrencia) | `Optimistic_concurrency_and_versioning_RN092`; E2E «concurrencia optimista (409)», «modificación sin If-Match» |
| IMP-29 (re-autenticación 15 min) | `Reauthentication_window_is_15_minutes_IMP29`; humo Web «revelar con contraseña incorrecta rechazado» |

## 4. Pendiente de automatizar

- Pruebas de carga y de rendimiento (RNF de rendimiento).
- Prueba de restauración del respaldo de la KEK en un servidor limpio (manual, anual).
- Pruebas de los trabajos de expiración y escalamiento con reloj simulado a lo largo de varios días (hoy se prueban las reglas de dominio y la ejecución real en el entorno local).
- Pruebas en navegador (interfaz con DevExtreme). La prueba de humo cubre el flujo HTTP de la Web, no el renderizado de los componentes.
