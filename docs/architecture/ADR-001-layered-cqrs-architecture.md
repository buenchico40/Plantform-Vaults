# ADR-001: Arquitectura en capas con CQRS, desplegada como monolito modular

| Atributo | Valor |
|---|---|
| Título | Arquitectura en capas con CQRS para la Plataforma de Gobierno de Credenciales, Certificados y Secretos (PGCCS) |
| Estado | **Aceptada** |
| Fecha | 2026-09-24 |
| Autor | Software Architect (.NET, DDD, C4, CQRS) |
| Documentos relacionados | [domain-model.md](domain-model.md), [c4-containers.md](c4-containers.md), [docs/specs/functional/](../specs/functional/) |

---

## Contexto

La PGCCS debe gobernar el ciclo de vida completo de cinco tipos de objetos sensibles (certificados, credenciales, secretos, claves criptográficas, cuentas de servicio) para una institución financiera, con:

- **78 casos de escritura con reglas de negocio densas**: segregación de funciones, cuatro ojos, aprobación por par o por Seguridad según criticidad, llave dividida, eliminación lógica vs. definitiva, grupos personales, cuentas de emergencia (RN-036, RN-052, RN-096, RN-104 a RN-118, RN-122 a RN-124).
- **Consultas muy distintas de las escrituras**: dashboards operativos y ejecutivos, búsquedas multicriterio sobre 200 000 objetos con p95 ≤ 3 s (RNF-REN-02), reportes regulatorios, índice de riesgo — todas de solo lectura, sin las invariantes de aprobación (US-009, US-010, US-041 a US-045).
- **Auditoría inmutable y fail-closed** que debe participar de la misma transacción que cualquier cambio de dominio (RN-079).
- **Restricciones tecnológicas fijas**: .NET 10, SQL Server on-premise, sin microservicios impuestos, un Front-End ASP.NET Core MVC que nunca toca datos directamente (R-07).
- **Volumen moderado** (50 000 objetos en el diseño inicial, 200 000 a 5 años; 20–60 millones de eventos de auditoría al año) — no es un sistema de escala de miles de instancias.

## Problema

¿Qué estilo arquitectónico da la mejor relación entre **protección de las invariantes de negocio**, **capacidad de construir consultas especializadas para dashboards y reportes**, **auditabilidad completa**, **testabilidad** y **complejidad operativa aceptable para un equipo bancario de tamaño mediano**, sin sobre-diseñar para una escala que la especificación no exige?

## Fuerzas de decisión

| Fuerza | Peso | Comentario |
|---|---|---|
| Separación de responsabilidades | Alto | Registro (comando) y consulta/dashboard (query) tienen formas de acceso a datos radicalmente distintas |
| Protección del dominio | Alto | Reglas como RN-104, RN-106, RN-122 no pueden vivir en la capa de datos ni en el controlador; deben vivir en un modelo de dominio explícito |
| Complejidad de autorizaciones | Alto | RBAC + ámbito + grupos + SoD + llave dividida es demasiado para un CRUD anémico |
| Flujos de aprobación | Alto | Multinivel, con reglas distintas según criticidad (par de grupo vs. Seguridad) — exige máquinas de estado explícitas |
| Auditoría y trazabilidad | Alto | Debe ser fail-closed y transaccional con el cambio de dominio (RN-079); una arquitectura con consistencia eventual lo pondría en riesgo |
| Consultas especializadas para dashboards | Alto | Índice de riesgo, controles de cumplimiento diarios, búsquedas multicriterio — necesitan proyecciones propias, no el modelo de escritura |
| Gestión de vencimientos y alertas | Medio | Procesos batch/worker con su propio ritmo, independientes del tráfico interactivo |
| Testabilidad | Alto | El dominio debe probarse sin base de datos ni HTTP (RNF-MAN-02, ≥ 80 % de cobertura en reglas de negocio) |
| Mantenibilidad | Alto | Equipo bancario, rotación de desarrolladores, vida útil de años |
| Escalabilidad | Medio | 200 000 objetos y 500 usuarios concurrentes (RNF-REN-04) — no requiere escala de microservicios |
| Complejidad operativa | Alto | Un solo equipo de operaciones on-premise (DEC-02); minimizar el número de piezas desplegables |
| Consistencia transaccional | Alto | Activar un objeto con llave dividida exige verificar dos custodios en la misma operación (RN-117); no se puede resolver con consistencia eventual |
| Curva de aprendizaje | Medio | Debe ser abordable por un equipo .NET convencional, sin exigir experiencia previa en sistemas distribuidos |
| Evolución futura | Medio | F2 añade recuperación de secretos por API a alto volumen (300 req/s) y SIEM — la arquitectura debe poder crecer sin reescritura |

## Alternativas evaluadas

### 1. Arquitectura en capas con CQRS (elegida)

Separación explícita en cuatro capas (Presentación → Aplicación → Dominio → Infraestructura), con el lado de **Comandos** modelado por agregados DDD (ver [domain-model.md](domain-model.md)) que protegen las invariantes, y el lado de **Consultas** modelado como proyecciones de solo lectura optimizadas para cada pantalla/reporte, dentro de **una única API .NET 10** y **una única base de datos lógica SQL Server** (más el Encrypted Vault dedicado para el contenido cifrado, ver [c4-containers.md §2.2](c4-containers.md#22-contenedores)).

### 2. Monolito anémico orientado a CRUD

Entidades como simples bolsas de propiedades (`ManagedObject { get; set; }` en todos sus campos), con la lógica de negocio dispersa en servicios de aplicación o, peor, en la capa de controladores. Los repositorios exponen `IQueryable` genérico tanto para leer como para escribir.

### 3. Clean Architecture (Onion/Hexagonal)

Variante de la opción 1 con dependencias estrictamente centrípetas (el dominio no conoce ni siquiera los nombres de las capas externas) y puertos/adaptadores explícitos para cada integración (Entra ID, AD, almacén de certificados local, Encrypted Vault, notificaciones).

### 4. Microservicios

Descomposición en servicios independientes por bounded context (Identidad, Gobierno de Objetos, Custodia, Solicitudes, Vencimientos, Auditoría…), cada uno con su propia base de datos, comunicados por API o mensajería, con consistencia eventual entre ellos.

## Decisión

Se adopta la **opción 1: arquitectura en capas con CQRS**, desplegada para la primera versión como **un monolito modular**: una única API .NET 10, un único proceso de despliegue (más los *workers* como `IHostedService` dentro del mismo proceso, ver [c4-containers.md §3.2](c4-containers.md#32-descripción-de-componentes)), y una única base de datos SQL Server transaccional (con el Encrypted Vault como base dedicada exclusivamente al contenido cifrado, por el requisito explícito de separación física del encargo de arquitectura).

La separación en capas y el CQRS son una decisión de **diseño del código**, no de topología de despliegue: no implican bases de datos separadas para leer y escribir, ni microservicios, ni un bus de mensajería. Ver §*Aclaración sobre CQRS* más abajo.

### Evaluación comparativa

| Criterio | 1. Capas + CQRS | 2. CRUD anémico | 3. Clean Architecture | 4. Microservicios |
|---|---|---|---|---|
| Separación de responsabilidades | ✅ Alta (comando ≠ consulta, dominio ≠ infraestructura) | ❌ Baja, todo mezclado | ✅ Alta (más estricta aún) | ✅ Alta, pero a nivel de servicio |
| Protección del dominio | ✅ Agregados con invariantes explícitas | ❌ Ninguna invariante protegida por el modelo | ✅ Igual que la opción 1 | ✅ Igual, pero por servicio |
| Complejidad de autorizaciones (RBAC+ámbito+grupo+SoD+llave dividida) | ✅ Domain Services dedicados | ❌ Lógica dispersa y difícil de probar | ✅ Igual que la opción 1 | ⚠️ Debe replicarse o centralizarse entre servicios |
| Flujos de aprobación multinivel | ✅ `AccessRequest` como máquina de estado | ❌ Difícil de expresar sin duplicar lógica | ✅ Igual que la opción 1 | ⚠️ Cruza servicios (Identidad + Solicitudes) |
| Auditoría fail-closed transaccional (RN-079) | ✅ Misma transacción SQL Server | ⚠️ Posible, pero sin garantía estructural | ✅ Igual que la opción 1 | ❌ Requiere consistencia eventual o Sagas — viola RN-079 |
| Consultas especializadas para dashboards | ✅ Modelos de lectura propios (mismo motor de datos) | ⚠️ Consultas ad hoc sobre el modelo de escritura, lentas y frágiles | ✅ Igual que la opción 1 | ✅ Con un servicio de reportería propio, más piezas |
| Gestión de vencimientos/alertas | ✅ Workers desacoplados vía CQRS de comandos internos | ⚠️ Jobs acoplados a controladores | ✅ Igual que la opción 1 | ✅ Con un servicio propio, más piezas |
| Testabilidad | ✅ Dominio sin BD ni HTTP | ❌ Difícil sin mocks de infraestructura | ✅ La mejor de las cuatro | ✅ Por servicio, pero pruebas de integración más caras |
| Mantenibilidad | ✅ Alta para un equipo mediano | ⚠️ Se degrada rápido al crecer reglas | ✅ Alta, algo más ceremoniosa | ⚠️ Alta complejidad de coordinación entre equipos |
| Escalabilidad | ✅ Suficiente para 200 000 objetos / 500 usuarios | ✅ Suficiente también, pero frágil | ✅ Igual que la opción 1 | ✅ Mayor, mal necesaria a esta escala |
| Complejidad operativa | ✅ Un despliegue, una base de datos | ✅ La más simple de operar | ⚠️ Ligeramente mayor (más proyectos/capas) | ❌ Alta: N despliegues, N pipelines, orquestación |
| Consistencia transaccional (RN-117) | ✅ Nativa, una sola transacción SQL Server | ✅ Nativa también | ✅ Igual que la opción 1 | ❌ Exige Sagas o consistencia eventual |
| Curva de aprendizaje | ✅ Moderada, patrón .NET conocido | ✅ La más baja | ⚠️ Algo mayor por la disciplina de dependencias | ❌ Alta (mensajería, resiliencia distribuida, DevOps) |
| Evolución futura (F2: 300 req/s, SIEM) | ✅ Se puede extraer un servicio de recuperación de secretos sin rediseñar el dominio | ❌ Reescritura probable | ✅ Igual que la opción 1 | ✅ Ya está descompuesto, pero pagado desde el día uno |

**Resultado:** la opción 1 iguala o se acerca a Clean Architecture en las fuerzas de mayor peso (protección del dominio, testabilidad, auditoría), con menor complejidad operativa y curva de aprendizaje, y evita el sobre-diseño de microservicios que la escala del proyecto (50 000–200 000 objetos, un banco, un equipo) no justifica. Clean Architecture queda registrada como **variante de evolución** (§Consecuencias) más que como alternativa descartada: la opción elegida ya sigue su regla de dependencias hacia el dominio; lo que se pospone es la ceremonia adicional de puertos/adaptadores explícitos por cada integración, que puede introducirse gradualmente si el número de integraciones crece.

## Aclaración sobre CQRS

Esta decisión usa CQRS como **patrón de separación de intención dentro del código de aplicación**, no como arquitectura distribuida. Explícitamente, **CQRS en esta plataforma NO implica**:

1. **Bases de datos separadas para comandos y consultas.** Ambas leen y escriben en la misma base SQL Server transaccional (más el Encrypted Vault dedicado al contenido cifrado). Las consultas usan vistas, proyecciones EF `AsNoTracking` o tablas de resumen materializadas dentro de esa misma base, para evitar que las búsquedas y dashboards (RNF-REN-02, US-041 a US-045) compitan por bloqueos con las transacciones de escritura, sin duplicar la infraestructura de datos.
2. **Consistencia eventual.** Una consulta ejecutada inmediatamente después de un comando exitoso ve el resultado de ese comando: mismo motor de base de datos, misma transacción confirmada.
3. **Microservicios.** Comandos y consultas viven en el mismo proceso .NET, en el mismo despliegue.
4. **Un bus de eventos externo.** Los eventos de dominio (ver [domain-model.md §12](domain-model.md#12-eventos-de-dominio)) se despachan en memoria, dentro de la misma transacción y el mismo proceso; no hay Kafka, Service Bus ni RabbitMQ en esta versión.

Para la primera versión se evalúa explícitamente, y se adopta, un **despliegue modular con una única API y una única base de datos SQL Server** (Encrypted Vault aparte, por mandato del encargo de arquitectura), manteniendo la separación lógica de carpetas/proyectos entre `Application.Commands`, `Application.Queries`, `Domain` e `Infrastructure`.

## Justificación

1. **Separación de responsabilidades:** el 60 % de las historias de usuario son de consulta o reportería (dashboards, búsquedas, auditoría, exportaciones); forzarlas a pasar por el modelo de agregados de escritura habría degradado tanto el rendimiento (RNF-REN-02) como la legibilidad del dominio.
2. **Protección del dominio:** reglas como "los objetos críticos los aprueba Seguridad, nunca un par" (RN-122) o "un objeto con llave dividida no se activa sin custodio aceptado por lado" (RN-117) son invariantes de agregado por definición; un CRUD anémico las dejaría como validaciones dispersas y frágiles en controladores.
3. **Complejidad de autorizaciones:** el modelo RBAC + ámbito + grupo + SoD + llave dividida (03 §4) se implementa como un único `AuthorizationService` de dominio, invocado igual desde comandos y desde consultas, evitando duplicación.
4. **Flujos de aprobación:** `AccessRequest` como agregado con máquina de estado explícita (Pendiente → EnAprobación → Aprobada/Rechazada) es directamente el diagrama de [03-domain-glossary.md §3.2](../specs/functional/03-domain-glossary.md); un CRUD anémico requeriría reconstruir ese estado a partir de flags dispersos.
5. **Auditoría y trazabilidad:** al mantener comandos y consultas en la misma base de datos y el mismo proceso, el `AuditEvent` se escribe en la misma transacción que el cambio de dominio, satisfaciendo RN-079 (fail-closed) sin necesidad de Sagas ni compensación.
6. **Consultas especializadas para dashboards:** el lado de Queries puede evolucionar sus propias proyecciones (por ejemplo, una tabla de resumen para el índice de riesgo de US-042) sin tocar los agregados de escritura ni arriesgar sus invariantes.
7. **Gestión de vencimientos y alertas:** los *workers* de background son, en términos de CQRS, generadores de comandos internos (`RecalculateExpirationCommand`, `EscalateAlertCommand`); se benefician de la misma separación sin necesitar infraestructura de mensajería.
8. **Testabilidad:** los agregados y Domain Services se prueban unitariamente sin base de datos ni HTTP; los Query Handlers se prueban de forma más simple, sobre datos de prueba en memoria o SQLite in-memory.
9. **Mantenibilidad:** la estructura de carpetas por capa y por *feature* (Commands/Queries) es predecible para cualquier desarrollador .NET que se incorpore al equipo.
10. **Escalabilidad:** suficiente para los 500 usuarios concurrentes y 200 000 objetos del horizonte a 5 años (RNF-REN-04) sin necesitar la complejidad de microservicios.
11. **Complejidad operativa:** un solo pipeline de CI/CD, un solo esquema de base de datos que versionar, coherente con el despliegue on-premise decidido en DEC-02.
12. **Consistencia transaccional:** al no fragmentar los datos en servicios independientes, las invariantes que cruzan agregados dentro de una misma operación (RN-117) se resuelven con una transacción de base de datos local, no con Sagas.
13. **Curva de aprendizaje:** el equipo no necesita experiencia previa en sistemas distribuidos, mensajería o consistencia eventual — riesgo real dado que RN-079 exige justamente lo contrario de la consistencia eventual.
14. **Evolución futura:** si en F2 la recuperación de secretos por aplicaciones alcanza 300 req/s (RNF-REN-03/RNF-REN-08), el módulo de recuperación puede extraerse a un servicio propio (o a instancias dedicadas del mismo binario) sin rediseñar el dominio, porque ya está aislado como *bounded context* dentro del monolito modular (ver [domain-model.md §4](domain-model.md#4-bounded-contexts)).

## Consecuencias positivas

- El dominio queda expresado en código de forma directa a partir de las 124 reglas de negocio del glosario, con invariantes verificables por prueba unitaria.
- Los dashboards y reportes (CU-06) pueden evolucionar de forma independiente sin arriesgar la integridad de los agregados de escritura.
- Un único pipeline de despliegue y una única base de datos simplifican las operaciones de un equipo bancario de tamaño mediano.
- La ruta de evolución hacia Clean Architecture o hacia la extracción de un servicio de recuperación de secretos en F2 no exige reescritura, solo mover límites de proyecto.
- Todas las operaciones sensibles pueden ser fail-closed de forma estructural, no por disciplina de código.

## Consecuencias negativas

- Mayor número de artefactos de código (Commands, Queries, Handlers, DTOs) que un CRUD directo, con más ceremonia inicial.
- El equipo debe mantener la disciplina de no filtrar el modelo de escritura hacia el lado de Queries (y viceversa), lo que exige revisión de código constante.
- Una única base de datos SQL Server es un límite de escalado vertical: si el volumen de auditoría (20–60 M eventos/año) creciera muy por encima del horizonte previsto, se necesitaría partición de tablas o un almacén de auditoría separado — no urgente hoy (ver RNF-REN-07).
- El Encrypted Vault como base física separada de la transaccional (exigencia del encargo de arquitectura) añade una segunda base de datos que operar, aunque sin las cargas de un microservicio completo.

## Riesgos

| ID | Riesgo | Probabilidad | Impacto |
|---|---|---|---|
| R1 | El equipo, por familiaridad con CRUD, termine anemizando los agregados pese al diseño (setters públicos, lógica en controladores) | Media | Alto |
| R2 | Los *workers* en el mismo proceso que la API compitan por recursos si el volumen de auditoría crece rápido | Baja–Media | Medio |
| R3 | La necesidad futura (F2) de 300 req/s de recuperación de secretos exija más capacidad de la que un monolito modular puede dar sin extracción de servicio | Media | Medio |
| R4 | La separación física del Encrypted Vault respecto de SQL Server introduzca una operación de "escritura en dos bases" que no sea verdaderamente atómica (ver `RISK-C4-01` en [c4-containers.md §11](c4-containers.md#11-riesgos-arquitectónicos-y-mitigaciones)) | Media | Alto |

## Mitigaciones

- **R1:** revisión de arquitectura obligatoria en cada pull request que toque un agregado; pruebas unitarias de invariantes como parte de la suite crítica de CI (RNF-MAN-02); *linters* que prohíban setters públicos en entidades de dominio.
- **R2:** monitoreo de los *workers* (RNF-MAN-05); umbral definido para extraerlos a un `Worker Service` .NET independiente sin cambiar el modelo de dominio, si la telemetría lo justifica.
- **R3:** el módulo de recuperación de secretos por API ya se diseña como *bounded context* aislado (ver [domain-model.md §4](domain-model.md#4-bounded-contexts)) precisamente para poder extraerse sin fricción cuando F2 lo requiera.
- **R4:** patrón de escritura "Vault primero, metadatos después" con reconciliación de arranque para blobs huérfanos (ver `RISK-C4-01`); se documenta como decisión técnica a validar en el diseño detallado de la capa de infraestructura.

## Criterios de revisión

Esta decisión se revisa si ocurre cualquiera de los siguientes:

1. El tráfico de recuperación de secretos por API (F2) supera de forma sostenida el 70 % de la capacidad probada en pruebas de carga (RNF-REN-03/04).
2. El volumen de eventos de auditoría supera el horizonte de 60 M/año proyectado en 04 §1, degradando `RNF-REN-07` (consulta de auditoría p95 ≤ 5 s).
3. Se incorpora un segundo canal de consumo (por ejemplo, integración móvil, hoy fuera de alcance) que requiera un modelo de autorización o de datos incompatible con el monolito modular.
4. Auditoría Interna o el QSA de PCI DSS (RNF-CUM-04) objetan la segmentación de datos entre SQL Server y el Encrypted Vault tal como está descrita.

## Referencias

- [docs/specs/functional/01-vision-document.md](../specs/functional/01-vision-document.md)
- [docs/specs/functional/02-user-stories.md](../specs/functional/02-user-stories.md)
- [docs/specs/functional/03-domain-glossary.md](../specs/functional/03-domain-glossary.md)
- [docs/specs/functional/04-non-functional-requirements.md](../specs/functional/04-non-functional-requirements.md)
- [docs/specs/functional/05-traceability-matrix.md](../specs/functional/05-traceability-matrix.md)
- [domain-model.md](domain-model.md)
- [c4-containers.md](c4-containers.md)
- Evans, E. — *Domain-Driven Design*
- Fowler, M. — *CQRS* (martinfowler.com/bliki/CQRS.html): "CQRS is a significant mental leap… it should be used only for parts of the domain rather than the whole system."
