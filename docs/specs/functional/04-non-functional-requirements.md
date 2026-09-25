# 04 — Requisitos No Funcionales

**Plataforma de Gobierno de Credenciales, Certificados y Secretos (PGCCS)**

| Atributo | Valor |
|---|---|
| Documento | 04-non-functional-requirements.md |
| Versión | 1.0 |
| Fecha | 2026-09-24 |
| Convención | **RNF-{CATEGORÍA}-NN**. Cada requisito tiene métrica verificable y método de verificación. |

---

## 1. Contexto técnico y restricciones de base

| ID | Restricción | Origen |
|---|---|---|
| R-01 | Backend y API desarrollados en **.NET 10**. | Visión §10 |
| R-02 | Base de datos **SQL Server** 2022+ on-premise. | Visión §10, DEC-02 |
| R-03 | Autenticación con **Microsoft Entra ID**; cuentas locales solo de emergencia (DEC-33). | Visión §10 |
| R-04 | Toda comunicación sobre **HTTPS**. | Visión §10 |
| R-05 | Información sensible **cifrada** en reposo. | Visión §10 |
| R-06 | Todo acceso a información sensible **auditado**. | Visión §10 |
| R-07 | El Front-End **no accede directamente** a la base de datos. | Visión §10 |
| R-08 | Visibilidad de objetos restringible **por grupos**. | Visión §10 |
| R-09 | Despliegue **on-premise** en los centros de datos del banco, con conectividad saliente controlada a Entra ID y Microsoft 365. Sin dependencia de servicios de nube para la custodia criptográfica (DEC-35). | DEC-02 |

**Supuestos de dimensionamiento** (se usan para las métricas y se revisan tras la carga inicial):

| Parámetro | Valor de diseño | Horizonte 5 años |
|---|---|---|
| Objetos administrados | 50 000 | 200 000 |
| Usuarios nominales | 1 000 | 3 000 |
| Usuarios concurrentes (UI) | 200 | 500 |
| Aplicaciones consumidoras por API | 300 | 1 000 |
| Recuperaciones por API de aplicaciones (pico) | No aplica en F1 (DEC-03) | 300 req/s (F2) |
| Eventos de auditoría | 20 M / año | 60 M / año |

---

## 2. Seguridad

| ID | Requisito | Métrica / criterio | Verificación |
|---|---|---|---|
| RNF-SEG-01 | **MFA obligatorio** para todo acceso de usuarios. Las acciones sensibles (revelar, descargar llaves, aprobar, cambiar políticas o roles) exigen una autenticación MFA reciente (≤ 15 min) mediante *step-up* / authentication context de Entra ID. | 100 % de sesiones con claim `amr` = `mfa`. Tokens sin MFA → 401/403. | Pruebas de integración; revisión de la configuración de acceso condicional. |
| RNF-SEG-02 | **TLS 1.3** en todas las comunicaciones externas: navegador ↔ Front-End, Front-End ↔ API, aplicaciones ↔ API. TLS 1.2 solo se admite para dependencias internas que no soporten 1.3 (SQL Server, SMTP, LDAPS) y únicamente con suites AEAD (ECDHE + AES-GCM). TLS ≤ 1.1, SSL y HTTP sin cifrar están deshabilitados. HSTS con `max-age` ≥ 1 año. | Escaneo TLS (p. ej., testssl.sh) sin hallazgos de severidad media o superior. | Escaneo en QA y en producción. |
| RNF-SEG-03 | **Cifrado AES-256** en reposo: (a) Sensitive Payloads con cifrado de sobre: DEK AES-256-GCM única por objeto y versión, envuelta con una KEK local (certificado RSA-3072+ no exportable en el almacén de certificados de la máquina, DEC-35); (b) Transparent Data Encryption en SQL Server; (c) respaldos cifrados con AES-256; (d) en objetos con llave dividida, cada componente del valor con su propia DEK (RN-113). | 0 payloads en claro en la BD o en respaldos. | Inspección de BD y respaldos; revisión de código. |
| RNF-SEG-04 | **Gestión segura de llaves**: la KEK es un certificado RSA no exportable (3072 bits o superior) instalado en el almacén de certificados de la máquina (Windows Certificate Store), protegido por DPAPI/TPM cuando el hardware lo soporta; la clave privada nunca sale del servidor ni se expone en texto plano; rotación de la KEK al menos anual y bajo demanda ante compromiso, con re-envoltura de las DEK sin indisponibilidad; solo la cuenta de servicio de la API tiene permiso de lectura de la clave privada; respaldo cifrado y fuera de línea del certificado, custodiado por Seguridad, como parte del procedimiento de recuperación ante desastres (RNF-DIS-04); separación de funciones entre quien administra el servidor/certificado y quien opera la plataforma; registro de todas las operaciones con la llave (DEC-35). | Evidencia de rotación anual; revisión del respaldo cifrado del certificado. | Auditoría de configuración del almacén de certificados. |
| RNF-SEG-05 | **Protección de datos en memoria**: los payloads se mantienen en memoria el mínimo tiempo posible, no se almacenan en caché distribuida ni en disco temporal, y los buffers se limpian tras su uso cuando la plataforma lo permita. | Revisión de código. | Revisión de código seguro. |
| RNF-SEG-06 | **Gestión de sesión**: expiración por inactividad a los 15 min; duración máxima de 8 h; tokens de acceso de ≤ 60 min; cookies `Secure`, `HttpOnly`, `SameSite=Strict`; revocación de sesión ante la deshabilitación del usuario (≤ 15 min). | Pruebas funcionales. | Pruebas de seguridad. |
| RNF-SEG-07 | **Seguridad de aplicación**: cumplimiento de OWASP ASVS 4.0 nivel 3 para los módulos de secretos y nivel 2 para el resto; mitigación del OWASP Top 10; cabeceras de seguridad (CSP estricta, X-Content-Type-Options, frame-ancestors 'none'); protección anti-CSRF; validación de entradas en el servidor. | 0 vulnerabilidades críticas o altas abiertas en el pase a producción. | SAST, DAST, SCA y pentest externo antes de la puesta en producción y anualmente. |
| RNF-SEG-08 | **Seguridad de la API**: OAuth 2.0 / OIDC con Entra ID; aplicaciones autenticadas con certificado (la recuperación de secretos por aplicaciones es F2, DEC-03) (no se admiten *client secrets* para aplicaciones de producción); autorización por endpoint y ámbito; limitación de tasa por cliente; tamaño máximo de petición; errores sin detalle interno (RFC 7807). | Pruebas de API; revisión de OpenAPI. | Pruebas automatizadas y pentest. |
| RNF-SEG-09 | **No exposición de secretos** en logs, trazas, errores, reportes, exportaciones, notificaciones, auditoría, telemetría (Application Insights/OpenTelemetry) ni SIEM. Redacción automática en el framework de logging y pruebas canario (US-051). | 0 ocurrencias del valor canario. | Pruebas automatizadas en CI. |
| RNF-SEG-10 | **Seguridad de la cadena de suministro**: dependencias con SCA; SBOM generado por versión; artefactos firmados; pipeline con detección de secretos; imágenes base mínimas y parcheadas. | SBOM por release; 0 dependencias con CVE crítica sin excepción. | Pipeline CI/CD. |
| RNF-SEG-11 | **Endurecimiento de infraestructura**: la BD no es accesible públicamente (endpoint privado o red interna); API detrás de WAF; segmentación de red entre Front-End, API y BD; cuentas de servicio de la plataforma con mínimo privilegio. | Revisión de arquitectura. | Revisión de configuración y escaneo de vulnerabilidades. |
| RNF-SEG-12 | **Sincronización horaria**: todos los componentes sincronizados por NTP con una desviación de ≤ 1 s. Las marcas de tiempo se almacenan en UTC. | Desviación ≤ 1 s. | Monitoreo. |
| RNF-SEG-13 | **Gestión de vulnerabilidades** (PCI DSS 11.3 y 11.4): escaneos internos de vulnerabilidades al menos trimestrales y tras cada cambio significativo, con corrección de las altas y críticas y nuevo escaneo de verificación; pruebas de penetración internas anuales, incluidas las de segmentación. | Informes trimestrales sin vulnerabilidades altas o críticas abiertas. | Escaneos y pentest. |
| RNF-SEG-14 | **Detección de cambios en archivos críticos** (PCI DSS 11.5.2): monitoreo de integridad de binarios, configuración y scripts de la plataforma, con comparación al menos semanal y alerta ante cambios no autorizados. | Alertas de cambios no autorizados atendidas. | Revisión de la herramienta de monitoreo. |
| RNF-SEG-15 | **Cuentas locales de emergencia** (PCI DSS 8.2.2, 8.3 y 8.4): nominales y no compartidas; MFA local (FIDO2 o TOTP); contraseña de 15 caracteres o más, guardada con hash resistente (Argon2id, o PBKDF2 con 600 000 iteraciones o más); bloqueo de 30 min tras 5 intentos fallidos; cambio de contraseña tras cada uso y como máximo cada 90 días; revisión trimestral de las cuentas (RN-123, DEC-33). | 100 % de cuentas conformes en la revisión trimestral. | Revisión trimestral. |

---

## 3. Rendimiento

| ID | Requisito | Métrica | Verificación |
|---|---|---|---|
| RNF-REN-01 | **Tiempo de respuesta de la interfaz**: navegación y consulta de detalle. | p95 ≤ 2 s; p99 ≤ 4 s, con la carga de diseño. | Pruebas de carga. |
| RNF-REN-02 | **Búsqueda multicriterio** sobre el inventario completo (200 000 objetos). | p95 ≤ 3 s para la primera página de resultados. | Pruebas de carga con volumen de horizonte. |
| RNF-REN-03 | *(F2, DEC-03)* **Recuperación de secretos por API** (aplicaciones), incluidas la autorización, el descifrado y el registro de auditoría y uso. | p95 ≤ 300 ms; p99 ≤ 800 ms a 100 req/s. | Pruebas de carga. |
| RNF-REN-04 | **Capacidad concurrente**. | 500 usuarios concurrentes en UI + 300 req/s de API sin degradar el RNF-REN-01 ni el RNF-REN-03 en más de un 20 %. | Pruebas de estrés. |
| RNF-REN-05 | **Procesos batch**: monitoreo de vencimientos, escalamiento, análisis de uso y evaluación de controles. | Ejecución completa sobre 200 000 objetos en ≤ 10 min. | Pruebas de volumen. |
| RNF-REN-06 | **Reportes y exportaciones**. | ≤ 30 s en síncrono hasta 50 000 filas; por encima, asíncrono con notificación en ≤ 15 min. | Pruebas de volumen. |
| RNF-REN-07 | **Consulta de auditoría** con filtros sobre 12 meses en línea. | p95 ≤ 5 s. | Pruebas de volumen. |
| RNF-REN-08 | **Escalabilidad**: API y Front-End sin estado, escalables horizontalmente; los procesos batch se ejecutan en workers independientes, con bloqueo distribuido para evitar ejecuciones duplicadas. | Escalar de 2 a N instancias sin cambios de código; throughput lineal ± 20 %. | Prueba de escalado. |

---

## 4. Disponibilidad y continuidad

| ID | Requisito | Métrica | Verificación |
|---|---|---|---|
| RNF-DIS-01 | *(F2, DEC-03)* **Disponibilidad de la API de recuperación de secretos** (dependencia de aplicaciones en ejecución). | ≥ 99,95 % mensual. | Monitoreo sintético. |
| RNF-DIS-02 | **Disponibilidad de la interfaz y de la API de gestión**. | ≥ 99,9 % mensual, excluyendo ventanas de mantenimiento planificadas y notificadas. | Monitoreo sintético. |
| RNF-DIS-03 | **Alta disponibilidad**: mínimo 2 instancias de cada componente en hosts físicos distintos detrás de un balanceador; BD con SQL Server Always On Availability Groups (réplica síncrona en el centro de datos principal); sin punto único de falla. | Prueba de conmutación sin pérdida de servicio > 60 s. | Prueba de failover semestral. |
| RNF-DIS-04 | **Recuperación ante desastres**: centro de datos alterno del banco con réplica asíncrona de Always On y servidores de aplicación preparados; el certificado que actúa como KEK se replica de forma segura (exportación cifrada, canal fuera de banda) al sitio alterno como parte del procedimiento de DR; runbook documentado. | Simulacro de DR anual exitoso. | Simulacro de DR. |
| RNF-DIS-05 | **RTO** (Recovery Time Objective). | ≤ 2 h para el servicio completo. Al habilitar la recuperación por aplicaciones (F2) se revisará a ≤ 1 h. | Simulacro de DR. |
| RNF-DIS-06 | **RPO** (Recovery Point Objective). | ≤ 15 min para datos de inventario; ≤ 5 min para la bitácora de auditoría. | Simulacro de DR. |
| RNF-DIS-07 | **Respaldos**: completos diarios, diferenciales o de log cada 5–15 min; cifrados; copia inmutable fuera del sitio principal; retención de 35 días en línea y 1 año en archivo; restauración probada trimestralmente. | Restauración trimestral exitosa. | Prueba de restauración. |
| RNF-DIS-08 | **Degradación controlada**: si fallan el correo, Teams o el SIEM, la plataforma sigue operando con colas persistentes. Si falla el almacén de certificados local (KEK) o el almacén de auditoría, las operaciones sensibles se bloquean (fail-closed); la consulta de metadatos sigue disponible. | Pruebas de caos. | Pruebas de resiliencia. |
| RNF-DIS-09 | *Retirado (DEC-35, DEC-27).* Existía únicamente para la pérdida de Azure Key Vault; al no depender de ningún servicio de nube, el riesgo equivalente (pérdida del certificado local) queda cubierto por RNF-DIS-04 y RNF-DIS-07. | — | — |

---

## 5. Auditoría

| ID | Requisito | Métrica | Verificación |
|---|---|---|---|
| RNF-AUD-01 | **Retención**: eventos de auditoría conservados 10 años (DEC-28), configurable. Consultables en línea 12 meses y en archivo con recuperación en ≤ 48 h. Cumple PCI DSS 10.5.1 (≥ 12 meses, 3 meses disponibles de inmediato). | Política de retención configurada. | Revisión de configuración. |
| RNF-AUD-02 | **Integridad**: bitácora *append-only* (SQL Server Ledger tables *append-only* o mecanismo equivalente), encadenamiento hash SHA-256 (RN-076), copia diaria a almacenamiento WORM (almacenamiento on-premise con retención bloqueada tipo WORM, o Azure Blob inmutable si se autoriza) y verificación automática diaria. | 0 rupturas no detectadas; verificación diaria ejecutada. | Prueba de alteración controlada. |
| RNF-AUD-03 | **No repudio**: cada evento se asocia a una identidad autenticada con MFA (objectId de Entra ID), con marca de tiempo UTC sincronizada, IP, identificador de correlación y hash encadenado. Las aprobaciones registran el claim de autenticación reciente. Los paquetes de evidencia se firman con un certificado de la plataforma. | 100 % de eventos con identidad y marca de tiempo. | Revisión de muestra de eventos. |
| RNF-AUD-04 | **Completitud**: toda acción de la lista de US-036 genera un evento. Operaciones sensibles en modo fail-closed (RN-079). | Cobertura del 100 % en las pruebas automatizadas de auditoría. | Pruebas automatizadas. |
| RNF-AUD-05 | **Separación de la auditoría**: el almacén de auditoría está separado lógicamente de los datos operativos. Los administradores de la plataforma no tienen permisos de escritura sobre él. Los DBA con acceso privilegiado quedan registrados por la auditoría nativa de SQL Server, que Seguridad revisa periódicamente en F1 y se envía al SIEM a partir de F2 (DEC-08). | Revisión de permisos. | Auditoría de configuración. |
| RNF-AUD-06 | **Envío al SIEM** (F2, DEC-08): latencia ≤ 1 min y sin pérdida de eventos (cola persistente). | Latencia p95 ≤ 60 s. | Monitoreo. |
| RNF-AUD-07 | **Revisión diaria automatizada** (PCI DSS 10.4.1 y 10.4.1.1): reporte diario generado automáticamente, reglas de detección con alerta inmediata y revisión documentada por Seguridad el siguiente día hábil (RN-119, DEC-30). | 100 % de reportes revisados en ≤ 1 día hábil. | Control CC-10. |

---

## 6. Cumplimiento

La plataforma debe **facilitar y evidenciar** el cumplimiento de los marcos siguientes. La tabla relaciona cada control con las capacidades que lo soportan.

### 6.1 ISO/IEC 27001:2022 (Anexo A)

| Control | Descripción | Soporte en la plataforma |
|---|---|---|
| A.5.3 | Segregación de funciones | Matriz SoD, RN-036, US-030 |
| A.5.9 | Inventario de información y activos | Inventario centralizado, US-001 a US-005 |
| A.5.15 | Control de acceso | RBAC, US-029, denegación por defecto |
| A.5.16 | Gestión de identidades | Entra ID, US-025 |
| A.5.17 | Información de autenticación | Custodia cifrada de credenciales, US-015 |
| A.5.18 | Derechos de acceso | Solicitudes, aprobaciones, revocación, US-031 a US-035 |
| A.5.33 | Protección de registros | Bitácora inmutable, US-037 |
| A.8.2 | Derechos de acceso privilegiado | JIT, cuatro ojos, US-034 |
| A.8.5 | Autenticación segura | MFA, RNF-SEG-01 |
| A.8.15 | Registro de eventos (logging) | US-036, RNF-AUD-* |
| A.8.16 | Actividades de monitoreo | Alertas, SIEM, US-048 |
| A.8.24 | Uso de criptografía | Gestión de claves y certificados, RNF-SEG-03/04 |

### 6.2 PCI DSS v4.0

| Requisito | Descripción | Soporte en la plataforma |
|---|---|---|
| 3.6 / 3.7 | Gestión de claves criptográficas (generación, distribución, almacenamiento, criptoperíodo, retiro, conocimiento dividido/control dual) | US-005, RN-025, RN-026 (cuatro ojos = control dual), RN-113 (conocimiento dividido), RNF-SEG-04 |
| 4.2.1 | Criptografía fuerte en transmisión | RNF-SEG-02 |
| 4.2.1.1 | Inventario de certificados y claves de confianza | US-001, US-043 |
| 7.2 | Acceso según necesidad de conocer, mínimo privilegio | RBAC, ámbitos, RN-010 |
| 7.2.4 | Revisión periódica de cuentas de usuario y privilegios | Reportes de accesos, US-043/044 |
| 8.3 / 8.4 | Autenticación fuerte y MFA | RNF-SEG-01 |
| 8.6.1–8.6.3 | Gestión de cuentas de sistema y aplicación; contraseñas no embebidas en scripts o código; rotación | US-004, US-003, recuperación por API (US-046, F2) |
| 10.2 / 10.3 | Registro de eventos y protección de logs | US-036, US-037 |
| 10.5.1 | Retención de ≥ 12 meses | RNF-AUD-01 |
| 10.4.1 / 10.4.1.1 | Revisión diaria y automatizada de logs | RN-119, US-059, RNF-AUD-07 |
| 11.3 / 11.4 | Escaneos de vulnerabilidades y pruebas de penetración | RNF-SEG-07, RNF-SEG-13 |
| 11.5.2 | Detección de cambios en archivos críticos | RNF-SEG-14 |
| 12.5.2 | Confirmación anual del alcance de PCI DSS y evaluación anual con el QSA | RNF-CUM-04 |
| 8.2.2 / 8.3 / 8.4 | Cuentas de emergencia nominales, contraseñas robustas y MFA | RN-123, RNF-SEG-15 |
| 12.3.3 | Inventario y revisión anual de suites y protocolos criptográficos | US-020, US-050 (configuración insegura) |

### 6.3 DORA — Reglamento (UE) 2022/2554 (marco de referencia, no obligatorio, DEC-10)

| Artículo | Descripción | Soporte en la plataforma |
|---|---|---|
| Art. 8 | Identificación de activos TIC e inventario | Inventario centralizado, propietarios, relaciones |
| Art. 9 | Protección y prevención: control de acceso, autenticación fuerte, políticas criptográficas y gestión de claves | RBAC, MFA, cifrado, políticas |
| Art. 10 | Detección de actividades anómalas | Alertas, SIEM, hallazgos |
| Art. 11 / 12 | Respuesta, recuperación y respaldo | RNF-DIS-* |
| Art. 17 | Gestión de incidentes TIC | Suspensión de objetos, revocación, auditoría |
| RTS gestión de riesgo TIC (art. 6 y 7: criptografía y gestión del ciclo de vida de claves y certificados) | Registro, gestión del ciclo de vida y renovación de certificados | US-001, US-020 a US-024 |

### 6.4 Regulación bancaria y otros marcos

| Marco | Soporte en la plataforma |
|---|---|
| **Regulador bancario local** (retención de 10 años confirmada por Cumplimiento, DEC-28): requisitos de seguridad de la información, gestión de riesgo operacional/tecnológico, retención de registros | Retención configurable, reportes regulatorios parametrizables (US-044), evidencias firmadas |
| **SWIFT Customer Security Programme (CSCF)**: controles sobre credenciales, certificados y claves del entorno SWIFT, MFA, logging | Subtipo SWIFT con criticidad Crítico por defecto (RN-019), cuatro ojos, auditoría |
| **Basilea / riesgo operacional** | Índice de riesgo, dashboard ejecutivo (US-042) |
| **Protección de datos personales** (normativa local aplicable) | Minimización: solo datos de identidad corporativa; no se almacenan datos personales de clientes |

### 6.5 Requisitos de cumplimiento

| ID | Requisito | Verificación |
|---|---|---|
| RNF-CUM-01 | La plataforma genera las evidencias de los controles de §6.1–6.4 mediante reportes y paquetes de evidencia (US-044) sin intervención manual sobre los datos. | Revisión con Auditoría Interna. |
| RNF-CUM-02 | Los parámetros regulatorios (retención, umbrales, políticas por defecto) son configurables sin cambio de código. | Prueba funcional. |
| RNF-CUM-03 | **Alcance PCI DSS** (DEC-29): la plataforma está **dentro del alcance** como sistema que puede afectar la seguridad del CDE, porque custodia credenciales de sistemas del CDE y claves que protegen datos de tarjeta. **No forma parte del CDE**, porque no almacena, procesa ni transmite datos de tarjeta; no se permite registrarlos (RN-124). Le aplican los requisitos de PCI DSS v4.0, salvo los de protección de datos de tarjeta almacenados (3.2 a 3.5). | Validación del alcance con el QSA. |
| RNF-CUM-04 | **Confirmación anual del alcance** (PCI DSS 12.5.2, DEC-34): cada año, y además tras cambios significativos, Cumplimiento confirma el alcance de la plataforma y el inventario de sus componentes. La evaluación anual de PCI DSS con el QSA incluye explícitamente la plataforma y valida su clasificación como sistema que afecta la seguridad del CDE. Los hallazgos del QSA se registran y se les da seguimiento hasta su cierre. | Evidencia anual de la confirmación y del informe del QSA. |

---

## 7. Mantenibilidad, calidad y operación

| ID | Requisito | Métrica | Verificación |
|---|---|---|---|
| RNF-MAN-01 | Arquitectura por capas o limpia, con dominio independiente de la infraestructura. Separación Front-End / API / workers. | Revisión de arquitectura. | Revisión técnica. |
| RNF-MAN-02 | **Pruebas automatizadas críticas**: unitarias en el dominio (≥ 80 % de cobertura en reglas de negocio), de integración de la API, de autorización por rol y ámbito, pruebas canario de no exposición y ejecución de los escenarios Gherkin marcados como críticos. | Suite crítica en verde en CI; bloqueo del despliegue si falla. | Pipeline CI. |
| RNF-MAN-03 | **API documentada con OpenAPI 3.x**, versionada y publicada. Contratos verificados en CI. | Especificación válida sin errores. | Linter OpenAPI en CI. |
| RNF-MAN-04 | **Despliegue automatizado** (CI/CD) a Desarrollo, QA/Pruebas, UAT y Producción, con infraestructura como código y aprobación manual para Producción. | Despliegue exitoso en el ambiente de pruebas. | Pipeline CD. |
| RNF-MAN-05 | **Observabilidad**: logs estructurados, métricas y trazas distribuidas (OpenTelemetry), health checks y alertas técnicas (errores, latencia, fallos de jobs, colas). Sin datos sensibles (RNF-SEG-09). | Dashboard técnico operativo. | Revisión operativa. |
| RNF-MAN-06 | **Configuración externalizada**: sin secretos en archivos de configuración ni en código. La propia plataforma protege sus cadenas de conexión y credenciales técnicas con el mismo mecanismo local (DPAPI / certificado de máquina), nunca en texto plano en disco (DEC-35). | 0 secretos en el repositorio. | Escaneo de secretos. |

---

## 8. Usabilidad, accesibilidad e internacionalización

| ID | Requisito | Métrica |
|---|---|---|
| RNF-USA-01 | Interfaz web responsive para navegadores corporativos (Edge y Chrome, últimas 2 versiones). | Pruebas de compatibilidad. |
| RNF-USA-02 | Accesibilidad WCAG 2.1 nivel AA. | Auditoría de accesibilidad. |
| RNF-USA-03 | Idioma español. Textos externalizados para futura internacionalización. Fechas mostradas en la zona horaria local y almacenadas en UTC. | Revisión. |
| RNF-USA-04 | Registrar un objeto típico no requiere más de 3 minutos para un usuario capacitado. | Prueba de usabilidad. |

---

## 9. Resumen de reglas de seguridad documentadas

Consolidado para la validación 7 («todas las reglas de seguridad están documentadas»):

| Área | Reglas de negocio | RNF |
|---|---|---|
| Autenticación | RN-030, RN-031, RN-032, RN-123 | RNF-SEG-01, RNF-SEG-06, RNF-SEG-15 |
| Autorización / RBAC | RN-036 a RN-043, RN-010 | RNF-SEG-08 |
| Segregación de funciones / cuatro ojos / grupos | RN-033 a RN-036, RN-038, RN-052, RN-054, RN-095, RN-096, RN-103 a RN-110 | — |
| Aprobaciones | RN-044 a RN-055 | — |
| Acceso temporal / JIT | RN-056 a RN-060, RN-009 | — |
| Cifrado y gestión de claves (KEK local, DEC-35) | RN-083, RN-015, RN-017, RN-026 | RNF-SEG-02, 03, 04, 05 |
| Conocimiento dividido (llave dividida) | RN-111 a RN-118 | RNF-SEG-03 |
| Supervisión sin SIEM (revisión diaria) | RN-119 | RNF-AUD-07 |
| Fuera del CDE (sin datos de tarjeta) | RN-124 | RNF-CUM-03 |
| Aprobación de objetos críticos por Seguridad | RN-122 | — |
| Contingencia de llaves | Retirado (DEC-35, DEC-27) | — |
| No exposición | RN-067, RN-074, RN-077, RN-084, RN-085, RN-086, RN-100, RN-101 | RNF-SEG-09 |
| Auditoría | RN-075 a RN-079 | RNF-AUD-01 a 06 |
| Configuración insegura | RN-020, RN-029 | RNF-SEG-07, 10, 11 |
| Continuidad | — | RNF-DIS-01 a 08 |
