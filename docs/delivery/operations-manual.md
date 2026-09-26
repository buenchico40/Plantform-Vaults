# PlatformVault · Manual de operación (Fase 1, iteración 1A)

Destinatarios: equipo de operación de TI, mesa de soporte de segundo nivel y Seguridad de la Información.

## 1. Tareas programadas

Se ejecutan dentro de PlatformVault.Api. Cada ejecución toma un bloqueo (`sp_getapplock`), por lo que con varias instancias solo una trabaja, y queda registrada en `app.JobRun` (consulta: `GET /api/v1/jobs/runs`).

| Trabajo | Frecuencia | Qué hace | Si falla |
|---|---|---|---|
| `ExpirationMonitor` | Cada hora | Genera alertas por umbral (una por objeto y umbral) y alertas diarias de objetos expirados; avisa a los niveles N1 a N3 según criticidad. | Revise el log; las alertas se generan en la siguiente ejecución (son idempotentes). |
| `AlertEscalation` | Cada 30 min | Escala alertas no reconocidas: Alta y Crítica 24 h, Media 72 h, Baja 7 días, hasta N4 (Seguridad). | Idem. |
| `AccessWindow` | Cada 30 s | Activa accesos programados, revoca los vencidos y expira solicitudes sin respuesta (72 h). | **Prioridad alta**: si no corre, los accesos no se revocan a tiempo. La API también rechaza usos fuera de ventana, pero revise de inmediato. |
| `AuditChainVerification` | Cada 6 h | Verifica la cadena de hashes de auditoría. Una ruptura envía una alerta crítica a Seguridad. | Ver §4. |
| `NotificationDispatch` | Cada minuto | Envía la cola de correo con hasta 5 reintentos. Con SMTP apagado marca los mensajes `Suppressed`. | Revise la conectividad SMTP; los mensajes quedan en cola. |

## 2. Salud y registros

- `GET /health/live`: el proceso responde. `GET /health/ready`: base de datos y KEK disponibles (hace un cifrado de prueba).
- Logs de la API en `logs/platformvault-api-AAAAMMDD.log` (90 días). Cada línea lleva el `CorrelationId`, que también devuelve la cabecera `X-Correlation-Id` y aparece en los errores que ve el usuario («referencia …»). Los logs nunca contienen valores sensibles, contraseñas ni tokens.
- Toda acción de negocio queda en `audit.AuditEvent`. Consúltela desde **Auditoría** (Auditor y Seguridad).

## 3. Gestión de usuarios (Administrador)

- **Alta**: Usuarios → Crear usuario. El sistema muestra una contraseña temporal una sola vez; entréguela por un canal seguro. El usuario debe cambiarla al iniciar sesión.
- **Roles**: un usuario puede combinar roles operativos (Administrador, Custodio, Operador). Auditor y Seguridad son exclusivos y no pueden asignarse a quien pertenece a grupos o es propietario de objetos. Nadie se asigna roles a sí mismo. Cambiar roles cierra las sesiones del usuario.
- **Baja**: desmarque «Activo». Se cierran sus sesiones, se revocan sus accesos temporales y se cancelan sus solicitudes pendientes. Los usuarios no se eliminan.
- **Bloqueo**: 5 intentos fallidos bloquean la cuenta 30 minutos. «Desbloquear» la libera antes.
- **Límite de intentos por equipo**: además, la API admite como máximo `Security:LoginAttemptsPerMinutePerClient` (20 por defecto) intentos de inicio de sesión, re-autenticación o cambio de contraseña por minuto desde la IP de cada navegador; el exceso responde «Demasiados intentos. Espere un minuto».
- **Contraseña olvidada**: «Restablecer contraseña» genera una temporal y cierra sus sesiones.
- Política de contraseñas: 15 caracteres o más, mayúsculas, minúsculas, dígitos y símbolos, caducidad de 90 días, sin repetir las últimas 4. Riesgo aceptado temporal RA-01: sin MFA hasta la Fase 2.

## 4. Incidencias de seguridad

| Situación | Acción |
|---|---|
| Alerta «cadena de auditoría rota» | No modifique la base. Seguridad ejecuta **Auditoría → Verificar integridad**, anota la secuencia rota y abre la investigación forense. Conserve respaldos de la base y del log. |
| Sospecha de uso indebido de un acceso | Seguridad o el propietario revocan el acceso en **Solicitudes y accesos**; suspender el objeto revoca todos sus accesos a la vez. |
| API Key del canal Web comprometida | Genere una nueva (manual de instalación §7), registre ambos clientes, actualice la Web y retire la antigua. Revise en la auditoría los eventos `Api.ClientRejected`. |
| Pérdida o caducidad de la KEK | Sin la KEK los valores son irrecuperables. Restaure el certificado desde el respaldo con custodia dual, con la misma huella. La rotación con re-envoltura llega en la iteración 1B; hasta entonces un certificado anterior puede declararse en `KeyProtection:PreviousThumbprints` para seguir descifrando. |
| Respuesta `424 AUDIT_UNAVAILABLE` | La operación se rechazó porque no pudo auditarse (fail-closed). Revise la base de datos y el log con el `CorrelationId`. |

## 5. Respaldo y recuperación

- Base de datos: respaldo completo diario y de log según la política del banco. Con TDE, respalde también el certificado TDE de `master`.
- KEK: respaldo PFX generado en la instalación, fuera del servidor y con custodia dual. Pruebe la restauración al menos una vez al año.
- Claves de Data Protection de la Web (`DataProtection:KeysPath`): si se pierden, los usuarios deben volver a iniciar sesión; no hay pérdida de datos.
- La auditoría se conserva 10 años y no puede purgarse desde la aplicación.

## 6. Mantenimiento de la base de datos

- Los cambios de esquema se aplican con `database/deploy.ps1`, que es idempotente. Nunca edite tablas a mano: la cuenta técnica solo ejecuta procedimientos y la auditoría es de solo inserción.
- Tras cada despliegue revise las cuatro validaciones `OK` que imprime el script.
