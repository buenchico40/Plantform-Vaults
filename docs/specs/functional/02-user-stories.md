# 02 — Historias de Usuario

**Plataforma de Gobierno de Credenciales, Certificados y Secretos (PGCCS)**

| Atributo | Valor |
|---|---|
| Documento | 02-user-stories.md |
| Versión | 1.0 |
| Fecha | 2026-09-24 |
| Total de historias | 62 (US-001 a US-062) |
| Formato de escenarios | Gherkin en español (`# language: es`) |

---

## Convenciones

- **Prioridad (MoSCoW):** Must (imprescindible), Should (importante), Could (deseable).
- **Fase:** F1 = Fase 1 · F2 = fase posterior. Las fases aplican las decisiones DEC-01 a DEC-11 ([01-vision-document.md §9.2](01-vision-document.md#92-registro-de-decisiones)). Los escenarios marcados con la etiqueta `@F2` corresponden a funcionalidad de fase 2 dentro de una historia de F1.
- **Requisitos:** identificadores RF-xxx-NN del catálogo de [05-traceability-matrix.md §2](05-traceability-matrix.md#2-catálogo-de-requisitos-funcionales).
- **Reglas:** identificadores RN-NNN definidos en [03-domain-glossary.md](03-domain-glossary.md).
- Cada bloque `gherkin` es un archivo `.feature` independiente y válido. Palabras clave usadas: `Característica`, `Antecedentes`, `Escenario`, `Esquema del escenario`, `Ejemplos`, `Dado`, `Cuando`, `Entonces`, `Y`, `Pero`.
- **Definition of Done común a todas las historias:** criterios de aceptación verificados; escenarios automatizados cuando son críticos; eventos de auditoría verificados (RN-075); ausencia de Sensitive Payloads en logs verificada (RN-085); endpoints documentados en OpenAPI.

## Índice

| Épica | Historias |
|---|---|
| E01 Inventario de Objetos | US-001 – US-012 |
| E02 Propiedad de los Objetos | US-013 – US-014 |
| E03 Protección de Información | US-015 – US-018 |
| E04 Versionamiento | US-019 |
| E05 Vencimientos | US-020 – US-024 |
| E06 Gestión de Accesos | US-025 – US-028 |
| E07 Roles y Permisos | US-029 – US-030 |
| E08 Solicitudes y Aprobaciones | US-031 – US-033 |
| E09 Acceso Temporal | US-034 – US-035 |
| E10 Auditoría | US-036 – US-038 |
| E11 Trazabilidad de Uso | US-039 – US-040 |
| E12 Dashboard y Monitoreo | US-041 – US-042 |
| E13 Reportería | US-043 – US-045 |
| E14 Integraciones | US-046 – US-048 |
| E15 Descubrimiento Automático | US-049 – US-050 |
| E16 Cumplimiento y Seguridad | US-051 – US-052 |
| E17 Grupos de Acceso | US-053 (y US-027) |
| E18 Eliminación definitiva y supervisión de Seguridad | US-054 – US-055 |
| E19 Llave dividida | US-056 – US-058 |
| E20 Supervisión y emergencia | US-059 – US-062 (US-060 y US-061 retiradas) |

---

# E01 — Inventario de Objetos

## US-001 — Registrar certificado digital

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario Técnico, Administrador de Infraestructura, Administrador de Aplicaciones |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-01, RF-INV-02, RF-INV-07, RF-INV-08, RF-PRO-04 |
| Reglas | RN-001, RN-002, RN-004, RN-005, RN-006, RN-015, RN-016, RN-017, RN-018, RN-019, RN-020, RN-087 |
| Glosario | Managed Object, Certificate, Sensitive Payload, Object Owner |

**Descripción:** Como Custodio, quiero registrar certificados digitales (SSL/TLS, VPN, API, SWIFT, firma u otros) cargando su archivo, para contar con un inventario centralizado con metadatos confiables y fecha de expiración exacta.

**Criterios de aceptación:**
1. El formulario permite seleccionar el subtipo: Digital, SSL/TLS, VPN, API, SWIFT, Firma.
2. Admite archivos PEM, DER, CER/CRT y PFX/P12 (RN-015).
3. Extrae automáticamente Subject, Issuer, serie, SAN, NotBefore, NotAfter, huella SHA-256, algoritmos y tamaño de clave. La fecha de expiración (= NotAfter) no se puede editar (RN-016).
4. Si el archivo contiene clave privada, se custodia cifrada con sensibilidad Restringida (RN-017).
5. Bloquea el registro de un certificado con una huella SHA-256 ya existente en un objeto activo (RN-018).
6. Los certificados SWIFT reciben criticidad Crítico por defecto (RN-019).
7. Marca la configuración insegura según RN-020.
8. No permite guardar sin propietario funcional y técnico (RN-087).
9. Genera el código OBJ-NNNNNN, la versión 1 y un Audit Event de creación.

```gherkin
# language: es
Característica: Registrar certificado digital
  Como Custodio
  Quiero registrar certificados cargando su archivo
  Para mantener un inventario centralizado con metadatos confiables

  Antecedentes:
    Dado que estoy autenticado como "custodio.infra@banco.com" con rol "Custodio" en el área "Infraestructura"
    Y los usuarios "ana.funcional@banco.com" y "luis.tecnico@banco.com" están activos en Entra ID

  Escenario: Registro exitoso de un certificado SSL/TLS con extracción de metadatos
    Cuando registro un objeto de tipo "Certificado" y subtipo "SSL/TLS"
    Y cargo el archivo "portal-banco.pem"
    Y asigno como propietario funcional a "ana.funcional@banco.com"
    Y asigno como propietario técnico a "luis.tecnico@banco.com"
    Y selecciono criticidad "Alto", sensibilidad "Confidencial" y ambiente "Producción"
    Y guardo el objeto
    Entonces el sistema crea el objeto con un código con formato "OBJ-NNNNNN"
    Y la fecha de expiración es igual al campo NotAfter del certificado
    Y el campo fecha de expiración no es editable
    Y se registra la versión 1 del objeto
    Y se registra un evento de auditoría "OBJECT_CREATED" con resultado "Success"

  Escenario: Certificado con clave privada se custodia como restringido
    Cuando registro un certificado de subtipo "API" cargando el archivo "api-cliente.pfx" con su contraseña de contenedor
    Entonces la clave privada se almacena cifrada
    Y la sensibilidad del objeto se establece en "Restringida"
    Y la contraseña del contenedor no se persiste

  Escenario: Rechazo de certificado duplicado
    Dado que existe un objeto activo con la huella SHA-256 del archivo "portal-banco.pem"
    Cuando intento registrar nuevamente el archivo "portal-banco.pem"
    Entonces el sistema rechaza el registro
    Y muestra el código del objeto existente

  Escenario: Certificado SWIFT recibe criticidad crítica por defecto
    Cuando registro un certificado de subtipo "SWIFT"
    Entonces la criticidad propuesta es "Crítico"
    Y la sensibilidad propuesta es "Restringida"

  Esquema del escenario: Detección de configuración insegura
    Cuando registro un certificado con algoritmo de clave "<algoritmo>" de "<tamano>" bits y firma "<firma>"
    Entonces el objeto queda marcado con configuración insegura "<marcado>"

    Ejemplos:
      | algoritmo | tamano | firma  | marcado |
      | RSA       | 1024   | SHA256 | Sí      |
      | RSA       | 2048   | SHA1   | Sí      |
      | RSA       | 3072   | SHA256 | No      |
      | EC        | 256    | SHA384 | No      |
```

---

## US-002 — Registrar secreto, API Key u OAuth Token

| Campo | Valor |
|---|---|
| Actor | Custodio, Administrador de Aplicaciones, Propietario Técnico |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-01, RF-INV-02, RF-PRT-01, RF-PRT-05 |
| Reglas | RN-004, RN-021, RN-022, RN-023, RN-063, RN-083, RN-087 |
| Glosario | Secret, Sensitive Payload |

**Descripción:** Como Administrador de Aplicaciones, quiero registrar secretos de aplicación, API keys y OAuth tokens con su valor cifrado localmente, para eliminar su almacenamiento disperso.

**Criterios de aceptación:**
1. Subtipos disponibles: Secreto de aplicación, API Key, OAuth Token.
2. El modo de custodia (Interno o Solo metadatos) se toma de la configuración del tipo o subtipo definida por Seguridad. El custodio solo puede cambiarlo si esa configuración lo permite (RN-023, DEC-01). No existe el modo Referencia (DEC-35).
3. En modo Interno el valor es obligatorio, no supera 64 KB (RN-022) y se cifra antes de persistirse (RN-083).
4. Los OAuth Tokens exigen emisor, tipo de token y expiración o la marca «Sin vencimiento» aprobada (RN-021, RN-063).
5. La sensibilidad mínima es Confidencial (RN-004).
6. El valor nunca vuelve a mostrarse después de guardar, salvo por revelado autorizado (US-016).

```gherkin
# language: es
Característica: Registrar secretos de aplicación
  Como Administrador de Aplicaciones
  Quiero registrar secretos con su valor protegido
  Para eliminar secretos dispersos en archivos y código

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en la aplicación "Banca en Línea"

  Escenario: Registro de API Key en modo interno
    Cuando registro un objeto de tipo "Secreto" y subtipo "API Key"
    Y selecciono el modo de custodia "Interno"
    Y ingreso el valor secreto
    Y completo los propietarios, la criticidad "Alto" y la fecha de expiración "2027-03-31"
    Y guardo el objeto
    Entonces el valor se almacena cifrado con AES-256-GCM
    Y la pantalla de detalle muestra el valor enmascarado
    Y el evento de auditoría no contiene el valor secreto

  Escenario: Modo de custodia determinado por la configuración del tipo
    Dado que Seguridad configuró el subtipo "OAuth Token" con modo "Interno" sin permitir cambios
    Cuando registro un objeto de subtipo "OAuth Token"
    Entonces el modo de custodia del objeto es "Interno"
    Y la opción de modo "Solo metadatos" no está disponible

  Escenario: Sensibilidad mínima para objetos con valor secreto
    Cuando registro un secreto con sensibilidad "Interna"
    Entonces el sistema rechaza la operación con el mensaje "Un objeto con valor secreto debe ser Confidencial o Restringido"

  Escenario: OAuth Token sin fecha de expiración
    Cuando registro un objeto de subtipo "OAuth Token" sin fecha de expiración
    Y no existe una marca "Sin vencimiento" aprobada por Seguridad
    Entonces el sistema no permite activar el objeto
    Y muestra que la fecha de expiración es obligatoria

  Escenario: Rechazo si el servicio de cifrado no está disponible
    Dado que el servicio de cifrado no está disponible
    Cuando intento guardar un secreto en modo "Interno"
    Entonces el sistema rechaza la operación
    Y ningún dato del valor secreto queda almacenado
```

---

## US-003 — Registrar credencial técnica

| Campo | Valor |
|---|---|
| Actor | Custodio, Administrador de Infraestructura, Propietario Técnico |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-01, RF-INV-02, RF-PRT-01 |
| Reglas | RN-012, RN-013, RN-014, RN-083, RN-087 |
| Glosario | Credential, Sensitive Payload, Expiration Policy |

**Descripción:** Como Administrador de Infraestructura, quiero registrar contraseñas técnicas y credenciales de bases de datos, infraestructura y dispositivos de red, para controlarlas y monitorear su rotación.

**Criterios de aceptación:**
1. Subtipos: Contraseña técnica, Base de datos, Infraestructura, Dispositivo de red.
2. El sistema destino y el nombre de cuenta son obligatorios (RN-013).
3. La contraseña se cifra (RN-012, RN-083).
4. Si no se indica fecha de expiración, se calcula con la política de rotación (RN-014).

```gherkin
# language: es
Característica: Registrar credenciales técnicas
  Como Administrador de Infraestructura
  Quiero registrar credenciales de sistemas y dispositivos
  Para controlarlas y monitorear su rotación

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en el área "Infraestructura"
    Y la política de expiración para "Credencial de base de datos" define rotación máxima de 90 días

  Escenario: Registro de credencial de base de datos con expiración calculada
    Cuando registro una credencial de subtipo "Base de datos"
    Y indico el sistema destino "SQLPRD01-CORE" y la cuenta "svc_core_reader"
    Y indico que la contraseña se cambió el "2026-09-01"
    Y no indico fecha de expiración
    Y guardo el objeto
    Entonces la fecha de expiración calculada es "2026-11-30"

  Esquema del escenario: Campos obligatorios de credenciales
    Cuando registro una credencial de subtipo "<subtipo>" sin el campo "<campo>"
    Entonces el sistema rechaza el registro indicando que "<campo>" es obligatorio

    Ejemplos:
      | subtipo            | campo           |
      | Dispositivo de red | Sistema destino |
      | Infraestructura    | Nombre de cuenta |
      | Contraseña técnica | Nombre de cuenta |
```

---

## US-004 — Registrar cuenta de servicio

| Campo | Valor |
|---|---|
| Actor | Custodio, Administrador de Aplicaciones, Administrador de Infraestructura |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-01, RF-INV-02 |
| Reglas | RN-011, RN-027, RN-028, RN-029 |
| Glosario | Service Account, Application, Object Owner |

**Descripción:** Como Custodio, quiero registrar cuentas de servicio con su origen, aplicaciones consumidoras y objetos vinculados, para saber qué identidades no humanas existen, quién responde por ellas y qué credenciales usan.

**Criterios de aceptación:**
1. Directorio de origen, identificador y al menos una aplicación consumidora obligatorios (RN-027).
2. Permite vincular credenciales, secretos y certificados existentes (RN-011).
3. Una cuenta de servicio no puede asignarse como propietaria de ningún objeto (RN-028).
4. Marca configuración insegura si permite inicio de sesión interactivo sin excepción (RN-029).

```gherkin
# language: es
Característica: Registrar cuentas de servicio
  Como Custodio
  Quiero registrar cuentas de servicio y sus relaciones
  Para conocer y gobernar las identidades no humanas

  Escenario: Registro de Service Principal con credencial vinculada
    Dado que estoy autenticado con rol "Custodio"
    Y existe el secreto "OBJ-000120" de la aplicación "Pagos"
    Cuando registro una cuenta de servicio con origen "EntraID" e identificador "sp-pagos-prod"
    Y asocio la aplicación consumidora "Pagos"
    Y vinculo el objeto "OBJ-000120"
    Y guardo el objeto
    Entonces la cuenta de servicio se registra con el objeto vinculado "OBJ-000120"

  Escenario: Una cuenta de servicio no puede ser propietaria
    Cuando intento asignar la cuenta de servicio "svc_batch" como propietario técnico de un objeto
    Entonces el sistema rechaza la asignación con el mensaje "El propietario debe ser una persona"

  Escenario: Cuenta con inicio de sesión interactivo se marca como insegura
    Cuando registro una cuenta de servicio con inicio de sesión interactivo permitido
    Y no existe una excepción aprobada por Seguridad
    Entonces el objeto queda marcado con configuración insegura
```

---

## US-005 — Registrar clave criptográfica

| Campo | Valor |
|---|---|
| Actor | Custodio, Seguridad de la Información (consulta), Propietario Técnico |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-01, RF-INV-02, RF-PRT-01, RF-PRT-04 |
| Reglas | RN-004, RN-024, RN-025, RN-026, RN-083 |
| Glosario | Cryptographic Key, Sensitive Payload |

**Descripción:** Como Custodio, quiero registrar claves criptográficas y llaves de cifrado con su algoritmo, uso y criptoperíodo, para gobernar su vigencia y proteger su material.

**Criterios de aceptación:**
1. Algoritmo, longitud, uso y fecha de generación obligatorios (RN-024).
2. Criptoperíodo obligatorio; la expiración se calcula a partir de él (RN-025).
3. El material de clave es opcional. Si se carga, se clasifica como Restringido y se cifra.
4. Solo se muestra el KCV. El material nunca se exporta (RN-026).

```gherkin
# language: es
Característica: Registrar claves criptográficas
  Como Custodio
  Quiero registrar claves criptográficas con su criptoperíodo
  Para gobernar su vigencia y proteger su material

  Escenario: Registro de clave AES con cálculo de expiración
    Dado que estoy autenticado con rol "Custodio"
    Cuando registro una clave criptográfica con algoritmo "AES", longitud 256 y uso "Encrypt, Decrypt"
    Y indico fecha de generación "2026-01-01" y criptoperíodo de 365 días
    Y cargo el material de clave
    Y guardo el objeto
    Entonces la fecha de expiración es "2027-01-01"
    Y la sensibilidad del objeto es "Restringida"
    Y el detalle muestra únicamente el KCV de la clave

  Escenario: Criptoperíodo obligatorio
    Cuando registro una clave criptográfica sin criptoperíodo
    Entonces el sistema rechaza el registro indicando que el criptoperíodo es obligatorio
```

---

## US-006 — Editar objeto

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario, Operador (limitado), Seguridad (clasificación) |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-03, RF-VER-01, RF-VER-02, RF-VER-03, RF-CUM-10 |
| Reglas | RN-002, RN-016, RN-019, RN-046, RN-092, RN-093, RN-110, RN-124 |
| Glosario | Managed Object, Object Version, Permission |

**Descripción:** Como Custodio, quiero editar los metadatos y actualizar el valor de un objeto (por ejemplo, tras una renovación manual), para mantener el inventario al día con historial completo.

**Criterios de aceptación:**
1. Solo usuarios con permiso Modificar en el ámbito del objeto pueden editar (§4.2 del glosario).
2. El tipo no es editable (RN-002). La expiración de un certificado solo cambia al cargar un nuevo archivo (RN-016).
3. El motivo del cambio es obligatorio.
4. Cada edición genera una nueva versión con autor, fecha y campos modificados (RN-092).
5. La actualización del Sensitive Payload se registra como «Valor modificado» (RN-093).
6. Al renovar, las alertas abiertas se resuelven automáticamente (RN-066).
7. Cada edición se notifica a Seguridad según RN-110 (US-054).
8. Los campos de metadatos no admiten números de tarjeta (RN-124).

```gherkin
# language: es
Característica: Editar objetos administrados
  Como Custodio
  Quiero editar objetos y registrar renovaciones
  Para mantener el inventario actualizado con historial completo

  Antecedentes:
    Dado que existe el certificado "OBJ-000045" en versión 3 con fecha de expiración "2026-10-15"
    Y existe una alerta abierta de 30 días para "OBJ-000045"

  Escenario: Renovación manual de certificado
    Dado que estoy autenticado como Propietario Técnico de "OBJ-000045"
    Cuando cargo el nuevo archivo de certificado con NotAfter "2027-10-15"
    Y indico el motivo "Renovación anual"
    Y guardo los cambios
    Entonces el objeto queda en versión 4 con fecha de expiración "2027-10-15"
    Y la alerta de 30 días queda en estado "Resuelta"
    Y el historial registra mi usuario, la fecha y los campos modificados

  Escenario: El tipo de objeto no es editable
    Dado que estoy autenticado con rol "Custodio" en el ámbito de "OBJ-000045"
    Cuando intento cambiar el tipo del objeto a "Secreto"
    Entonces el sistema rechaza la operación

  Escenario: Usuario sin permiso de modificación
    Dado que estoy autenticado con rol "Auditor"
    Cuando intento modificar la descripción de "OBJ-000045"
    Entonces el sistema responde con acceso denegado
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Motivo obligatorio
    Dado que estoy autenticado con rol "Custodio" en el ámbito de "OBJ-000045"
    Cuando modifico la descripción sin indicar motivo del cambio
    Entonces el sistema no permite guardar los cambios
```

---

## US-007 — Administrar el estado del objeto (activar, suspender, desactivar, reactivar)

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-04, RF-INV-10, RF-TMP-03 |
| Reglas | RN-007, RN-009, RN-066, RN-087 |
| Glosario | Lifecycle State, Temporary Access |

**Descripción:** Como Custodio, quiero cambiar el estado del ciclo de vida de un objeto, para retirar de uso los objetos innecesarios o comprometidos sin perder su historial.

**Criterios de aceptación:**
1. Solo se permiten las transiciones del diagrama §3.1 del glosario (RN-007).
2. El motivo es obligatorio al suspender o desactivar.
3. Al suspender o desactivar se revocan todos los accesos temporales vigentes y se bloquean el revelado y la descarga (RN-009).
4. Las alertas de vencimiento de objetos desactivados se cierran automáticamente (RN-066).
5. La reactivación revalida los propietarios (RN-087).

```gherkin
# language: es
Característica: Administrar estados del ciclo de vida
  Como Custodio
  Quiero desactivar, suspender y reactivar objetos
  Para retirar de uso objetos innecesarios o comprometidos conservando su historial

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en el ámbito del objeto "OBJ-000200"
    Y el objeto "OBJ-000200" está en estado "Activo"
    Y el usuario "op1@banco.com" tiene un acceso temporal activo sobre "OBJ-000200"

  Escenario: Desactivar un objeto revoca los accesos vigentes
    Cuando desactivo el objeto "OBJ-000200" con el motivo "Aplicación retirada"
    Entonces el objeto queda en estado "Desactivado"
    Y el acceso temporal de "op1@banco.com" queda en estado "Revocado"
    Y el revelado del valor de "OBJ-000200" no está disponible
    Y se registra un evento de auditoría "OBJECT_DEACTIVATED"

  Escenario: Suspender por sospecha de compromiso
    Cuando suspendo el objeto "OBJ-000200" con el motivo "Posible exposición en repositorio"
    Entonces el objeto queda en estado "Suspendido"
    Y se notifica a los propietarios funcional y técnico

  Escenario: Transición no permitida
    Dado que el objeto "OBJ-000300" está en estado "Eliminado"
    Cuando intento reactivar el objeto "OBJ-000300"
    Entonces el sistema rechaza la transición

  Escenario: Reactivación con propietario inválido
    Dado que el objeto "OBJ-000200" está en estado "Desactivado"
    Y su propietario técnico está deshabilitado en Entra ID
    Cuando intento reactivar el objeto "OBJ-000200"
    Entonces el sistema exige asignar un propietario técnico válido antes de reactivar
```

---

## US-008 — Eliminar objeto lógicamente

| Campo | Valor |
|---|---|
| Actor | Custodio o Propietario (solicita); Seguridad, par del grupo o nadie (aprueba, según el objeto) |
| Prioridad | Must · F1 |
| Requisitos | RF-ROL-02, RF-APR-04, RF-APR-05, RF-APR-06, RF-CUM-02, RF-INV-12, RF-ACC-09 |
| Reglas | RN-008, RN-052, RN-054, RN-075, RN-096, RN-105, RN-106, RN-107, RN-108, RN-110 |
| Glosario | Managed Object, Approval, Four Eyes Principle, Security Group |

**Descripción:** Como Custodio, quiero eliminar lógicamente un objeto que ya no aplica, para depurar el inventario sin perder su historial ni su trazabilidad, con la autorización que corresponda a su criticidad.

**Criterios de aceptación:**
1. Solo se pueden eliminar lógicamente objetos en estado Borrador o Desactivado.
2. La aprobación depende del objeto (RN-008):

| Situación del objeto | Quién aprueba |
|---|---|
| Crítico | Un usuario con rol Seguridad (RN-107) |
| No crítico, en un grupo de dos o más miembros | Otro miembro del grupo (RN-106) |
| No crítico, en un grupo personal | Nadie: su único miembro lo elimina directamente (RN-108) |
| No crítico, sin grupo | Un usuario con rol Seguridad |

3. El aprobador siempre es distinto del solicitante (RN-054).
4. El objeto queda en estado Eliminado, se excluye de las búsquedas por defecto y conserva su historial, sus versiones y su auditoría. Sus Sensitive Payloads quedan inaccesibles y se conservan cifrados durante el período de retención.
5. Se notifica al grupo (RN-105) y a Seguridad (RN-110).
6. Para borrar por completo un objeto creado por error se usa la eliminación definitiva (US-055).

```gherkin
# language: es
Característica: Eliminación lógica de objetos
  Como Custodio
  Quiero eliminar lógicamente objetos que ya no aplican
  Para depurar el inventario sin perder trazabilidad

  Antecedentes:
    Dado que "custodio@banco.com" y "dba2@banco.com" son miembros activos del grupo "GRP-INFRA-BD"
    Y estoy autenticado como "custodio@banco.com" con rol "Custodio"

  Escenario: Objeto no crítico eliminado con aprobación de un par del grupo
    Dado que el objeto "OBJ-000410" de criticidad "Medio" está "Desactivado" y asignado al grupo "GRP-INFRA-BD"
    Cuando solicito eliminar el objeto "OBJ-000410" con el motivo "Duplicado de OBJ-000409"
    Y "dba2@banco.com" aprueba la solicitud de eliminación
    Entonces el objeto "OBJ-000410" queda en estado "Eliminado"
    Y el historial de versiones del objeto sigue disponible para consulta
    Y los miembros del grupo "GRP-INFRA-BD" reciben una notificación de la eliminación

  Escenario: Solo Seguridad autoriza la eliminación de un objeto crítico
    Dado que el objeto "OBJ-000412" de criticidad "Crítico" está "Desactivado" y asignado al grupo "GRP-INFRA-BD"
    Cuando solicito eliminar el objeto "OBJ-000412" con el motivo "Servicio retirado"
    Entonces los aprobadores elegibles son únicamente los usuarios con rol "Seguridad"
    Y si "dba2@banco.com" intenta aprobar la solicitud, el sistema la rechaza
    Y los usuarios con rol "Seguridad" reciben una notificación inmediata de la solicitud

  Escenario: Grupo personal elimina sin autorización
    Dado que "custodio@banco.com" es el único miembro del grupo personal "GRP-PERS-CUSTODIO"
    Y el objeto "OBJ-000413" de criticidad "Bajo" está "Desactivado" y asignado solo a "GRP-PERS-CUSTODIO"
    Cuando elimino el objeto "OBJ-000413" con el motivo "Ya no se utiliza"
    Entonces el objeto "OBJ-000413" queda en estado "Eliminado" sin solicitud de aprobación
    Y se registra un evento de auditoría "OBJECT_DELETED"

  Escenario: El solicitante no puede aprobar su propia eliminación
    Dado que el objeto "OBJ-000410" de criticidad "Medio" está "Desactivado" y asignado al grupo "GRP-INFRA-BD"
    Cuando solicito eliminar el objeto "OBJ-000410"
    Entonces "custodio@banco.com" no figura entre los aprobadores elegibles de la solicitud

  Escenario: No se puede eliminar un objeto activo
    Dado que el objeto "OBJ-000500" está en estado "Activo"
    Cuando solicito eliminar el objeto "OBJ-000500"
    Entonces el sistema indica que primero debe desactivarse
```

---

## US-009 — Consultar detalle de objeto

| Campo | Valor |
|---|---|
| Actor | Todos los perfiles con permiso Consultar |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-05, RF-ACC-07, RF-PRT-03, RF-AUD-02 |
| Reglas | RN-010, RN-043, RN-084 |
| Glosario | Managed Object, Permission, Security Group |

**Descripción:** Como Operador, quiero consultar el detalle de un objeto de mi ámbito, para conocer sus metadatos, propietarios, estado, vencimiento, historial y relaciones sin exponer su valor secreto.

**Criterios de aceptación:**
1. El detalle muestra metadatos, propietarios, estados, expiración, versiones, relaciones, alertas abiertas y accesos vigentes.
2. El Sensitive Payload siempre aparece enmascarado (RN-043, RN-084).
3. Un objeto fuera del ámbito del usuario responde «no encontrado», sin revelar que existe (RN-010).
4. Cada consulta de detalle genera un Audit Event «OBJECT_VIEWED».

```gherkin
# language: es
Característica: Consultar detalle de objetos
  Como Operador
  Quiero consultar el detalle de los objetos de mi ámbito
  Para conocer su información sin exponer valores secretos

  Escenario: Consulta de objeto dentro del ámbito
    Dado que estoy autenticado con rol "Operador" en el grupo "GRP-OPS-PAGOS"
    Y el objeto "OBJ-000120" está asignado al grupo "GRP-OPS-PAGOS"
    Cuando consulto el detalle de "OBJ-000120"
    Entonces veo los metadatos, propietarios, estado y fecha de expiración
    Y el valor secreto se muestra como "••••••••"
    Y se registra un evento de auditoría "OBJECT_VIEWED"

  Escenario: Objeto fuera del ámbito no es visible
    Dado que estoy autenticado con rol "Operador" en el grupo "GRP-OPS-PAGOS"
    Y el objeto "OBJ-000900" no está asignado a ninguno de mis grupos, áreas ni aplicaciones
    Cuando consulto el detalle de "OBJ-000900"
    Entonces el sistema responde que el objeto no fue encontrado
    Y se registra un evento de auditoría con resultado "Denied"
```

---

## US-010 — Buscar objetos por múltiples criterios

| Campo | Valor |
|---|---|
| Actor | Todos los perfiles con permiso Consultar |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-05, RF-INV-06 |
| Reglas | RN-010, RN-094 |
| Glosario | Managed Object, Lifecycle State / Expiration Status |

**Descripción:** Como Custodio, quiero buscar y filtrar objetos combinando criterios, para localizar rápidamente los objetos que necesito gestionar.

**Criterios de aceptación:**
1. Búsqueda de texto libre por código, nombre, descripción, Subject/SAN, sistema destino, nombre de cuenta y etiquetas.
2. Filtros combinables: tipo, subtipo, criticidad, sensibilidad, llave dividida, ambiente, área, aplicación, propietario, grupo, estado de ciclo de vida, estado de expiración, rango de fechas de expiración, configuración insegura, sin uso, huérfano.
3. Los resultados respetan el ámbito del usuario (RN-010) y excluyen los Eliminados salvo filtro explícito.
4. Resultados paginados y ordenables. Tiempo de respuesta según RNF-REN-02.
5. Se pueden guardar los filtros como vistas personales.

```gherkin
# language: es
Característica: Búsqueda multicriterio de objetos
  Como Custodio
  Quiero buscar objetos combinando filtros
  Para localizar rápidamente lo que debo gestionar

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en el área "Infraestructura"
    Y existen en mi ámbito los siguientes objetos:
      | codigo     | tipo        | criticidad | ambiente   | estado expiracion |
      | OBJ-000001 | Certificado | Crítico    | Producción | Próximo a vencer  |
      | OBJ-000002 | Certificado | Bajo       | QA         | Vigente           |
      | OBJ-000003 | Credencial  | Crítico    | Producción | Expirado          |

  Escenario: Filtrar por tipo, criticidad y ambiente
    Cuando busco objetos con tipo "Certificado", criticidad "Crítico" y ambiente "Producción"
    Entonces obtengo exactamente el objeto "OBJ-000001"

  Escenario: Filtrar por estado de expiración
    Cuando busco objetos con estado de expiración "Expirado"
    Entonces obtengo exactamente el objeto "OBJ-000003"

  Escenario: Los objetos eliminados no aparecen por defecto
    Dado que el objeto "OBJ-000002" está en estado "Eliminado"
    Cuando busco objetos con tipo "Certificado"
    Entonces el resultado no contiene "OBJ-000002"

  Escenario: Resultados limitados al ámbito
    Dado que existe el objeto "OBJ-000777" en el área "Tesorería"
    Cuando busco el texto "OBJ-000777"
    Entonces el resultado está vacío
```

---

## US-011 — Clasificar criticidad y sensibilidad

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario Funcional, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-07, RF-INV-08, RF-INV-12, RF-INV-13 |
| Reglas | RN-003, RN-004, RN-019, RN-045, RN-096, RN-107, RN-111 |
| Glosario | Managed Object, Access Policy |

**Descripción:** Como Propietario Funcional, quiero clasificar cada objeto por criticidad y sensibilidad, para que se apliquen los controles de acceso y alertamiento adecuados a su riesgo.

**Criterios de aceptación:**
1. Criticidad obligatoria: Crítico, Alto, Medio, Bajo (RN-003).
2. Sensibilidad obligatoria: Pública, Interna, Confidencial, Restringida, con los mínimos de RN-004.
3. Un cambio de clasificación recalcula de inmediato las políticas de acceso y expiración aplicables.
4. Los objetos Críticos se muestran con una marca visible. Rebajar la criticidad de cualquier objeto Crítico, incluidos los SWIFT, requiere la aprobación de Seguridad (RN-019, RN-096, RN-107).
5. Además de la criticidad y la sensibilidad, los objetos a los que aplique pueden marcarse con el check «Llave dividida» (US-056).

```gherkin
# language: es
Característica: Clasificación de criticidad y sensibilidad
  Como Propietario Funcional
  Quiero clasificar los objetos según su riesgo
  Para que se apliquen los controles adecuados

  Esquema del escenario: Valores permitidos de clasificación
    Dado que estoy autenticado como Propietario Funcional del objeto "OBJ-000050"
    Cuando establezco criticidad "<criticidad>" y sensibilidad "<sensibilidad>"
    Entonces el resultado de la operación es "<resultado>"

    Ejemplos:
      | criticidad | sensibilidad | resultado |
      | Crítico    | Restringida  | Aceptado  |
      | Alto       | Confidencial | Aceptado  |
      | Medio      | Interna      | Rechazado |
      | Urgente    | Confidencial | Rechazado |

  Escenario: Reclasificación aplica una política más restrictiva
    Dado que el objeto "OBJ-000050" tiene sensibilidad "Confidencial"
    Cuando cambio su sensibilidad a "Restringida"
    Entonces las nuevas solicitudes de revelado sobre "OBJ-000050" requieren doble aprobación

  Escenario: Rebaja de criticidad de certificado SWIFT requiere Seguridad
    Dado que el certificado SWIFT "OBJ-000060" tiene criticidad "Crítico"
    Cuando solicito cambiar su criticidad a "Alto"
    Entonces el cambio queda pendiente de aprobación de un usuario con rol "Seguridad"
    Y la criticidad permanece "Crítico" hasta la aprobación
```

> Nota: en el primer ejemplo, «Medio / Interna» se rechaza porque el objeto OBJ-000050 custodia un Sensitive Payload (RN-004).

---

## US-012 — Carga inicial masiva del inventario

| Campo | Valor |
|---|---|
| Actor | Custodio, Administrador |
| Prioridad | Must · F1 (formato CSV, DEC-04) |
| Requisitos | RF-INV-09, RF-INV-01, RF-PRO-04, RF-CUM-10 |
| Reglas | RN-002, RN-003, RN-004, RN-005, RN-085, RN-087, RN-124 |
| Glosario | Managed Object, Object Owner |

**Descripción:** Como Custodio, quiero cargar de forma masiva el inventario inicial desde una plantilla, para poblar la plataforma rápidamente con los objetos existentes.

**Criterios de aceptación:**
1. Plantilla CSV descargable (UTF-8, separador configurable) con columnas de metadatos. **La plantilla no admite columnas de valores secretos** (RN-085). Los valores se cargan después, objeto por objeto (US-002, US-003, US-005).
2. Validación previa completa, con reporte de errores por fila, antes de confirmar.
3. La carga es transaccional por lote: si se confirma, solo se importan las filas válidas y las inválidas se reportan.
4. Los objetos cargados quedan en estado Borrador hasta completar su valor o su referencia.
5. La carga genera un Audit Event con el resumen (filas totales, válidas y rechazadas) y la huella del archivo.

```gherkin
# language: es
Característica: Carga inicial masiva del inventario
  Como Custodio
  Quiero importar el inventario existente desde una plantilla
  Para poblar la plataforma rápidamente

  Escenario: Validación previa con errores por fila
    Dado que estoy autenticado con rol "Custodio"
    Cuando cargo el archivo "inventario-inicial.csv" con 100 filas
    Y 3 filas no tienen propietario técnico
    Entonces el sistema muestra 97 filas válidas y 3 filas con error
    Y cada error indica la fila y el campo faltante
    Y ningún objeto se crea hasta que confirme la importación

  Escenario: Confirmación de importación
    Dado que validé el archivo "inventario-inicial.csv" con 97 filas válidas
    Cuando confirmo la importación
    Entonces se crean 97 objetos en estado "Borrador"
    Y se registra un evento de auditoría "BULK_IMPORT" con el resumen de la carga

  Escenario: Rechazo de columnas con valores secretos
    Cuando cargo un archivo que contiene una columna "Password"
    Entonces el sistema rechaza el archivo completo
    Y el contenido del archivo no se almacena ni se registra en logs

  Escenario: Marca visible de objeto crítico
    Dado que el objeto "OBJ-000060" tiene criticidad "Crítico"
    Cuando consulto el listado de objetos
    Entonces "OBJ-000060" aparece con la marca de objeto crítico

  Escenario: Rechazo de números de tarjeta en la carga
    Cuando cargo un archivo cuya columna "Descripción" contiene un número de tarjeta válido en la fila 12
    Entonces el sistema rechaza la fila 12 indicando que no se admiten datos de tarjeta
    Y el número de tarjeta no se almacena ni se registra en logs
```

---

# E02 — Propiedad de los Objetos

## US-013 — Asignar propietarios funcional y técnico

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario Funcional |
| Prioridad | Must · F1 |
| Requisitos | RF-PRO-01, RF-PRO-02, RF-PRO-03, RF-PRO-04 |
| Reglas | RN-028, RN-036, RN-037, RN-087, RN-088, RN-089 |
| Glosario | Object Owner, User, Role |

**Descripción:** Como Custodio, quiero asignar y reasignar el Propietario Funcional y el Propietario Técnico de cada objeto, para que siempre exista un responsable identificado.

**Criterios de aceptación:**
1. Los propietarios se eligen entre usuarios activos de Entra ID (no cuentas de servicio) (RN-028).
2. Ambos propietarios son obligatorios para Activo. Al menos uno es obligatorio para guardar (RN-087).
3. En objetos Críticos, los propietarios deben ser personas distintas (RN-088).
4. No se puede quitar un propietario sin reemplazo (RN-089).
5. No se puede asignar como propietario a usuarios con rol Auditor o Seguridad (SoD, RN-036).
6. El cambio se notifica al propietario anterior y al nuevo, y el rol Propietario se ajusta de forma automática (RN-037).

```gherkin
# language: es
Característica: Asignación de propietarios
  Como Custodio
  Quiero asignar propietarios funcional y técnico
  Para que todo objeto tenga responsables identificados

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en el ámbito del objeto "OBJ-000070"

  Escenario: Reasignación de propietario técnico
    Dado que "luis.tecnico@banco.com" es propietario técnico de "OBJ-000070"
    Cuando reasigno el propietario técnico a "maria.tecnica@banco.com"
    Entonces "maria.tecnica@banco.com" obtiene el rol "Propietario" sobre "OBJ-000070"
    Y "luis.tecnico@banco.com" pierde el rol "Propietario" sobre "OBJ-000070"
    Y ambos reciben una notificación del cambio

  Escenario: Objeto crítico con el mismo propietario funcional y técnico
    Dado que el objeto "OBJ-000070" tiene criticidad "Crítico"
    Cuando asigno a "ana@banco.com" como propietaria funcional y técnica
    Entonces el sistema rechaza la asignación indicando que deben ser personas distintas

  Escenario: No se puede dejar un objeto sin propietario
    Cuando intento quitar el propietario funcional sin designar reemplazo
    Entonces el sistema rechaza la operación

  Escenario: Usuario auditor no puede ser propietario
    Dado que "auditor1@banco.com" tiene rol "Auditor"
    Cuando intento asignar a "auditor1@banco.com" como propietario técnico
    Entonces el sistema rechaza la asignación por conflicto de segregación de funciones
```

---

## US-014 — Detectar objetos con propietario inválido (huérfanos)

| Campo | Valor |
|---|---|
| Actor | Sistema, Custodio, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-PRO-03, RF-PRO-04, RF-DES-02 |
| Reglas | RN-031, RN-087, RN-097 |
| Glosario | Object Owner, User, Discovery Finding, Alert |

**Descripción:** Como Seguridad de la Información, quiero que el sistema detecte automáticamente los objetos cuyo propietario dejó de estar activo, para reasignarlos antes de que queden sin responsable.

**Criterios de aceptación:**
1. La sincronización con Entra ID detecta usuarios deshabilitados o eliminados (≤ 15 min) (RN-031).
2. Los objetos afectados quedan marcados como «Propietario inválido» y generan un hallazgo de categoría Huérfano (RN-097).
3. Se envía una alerta al otro propietario, al Custodio del área y a Seguridad.
4. El dashboard muestra el número de objetos huérfanos.

```gherkin
# language: es
Característica: Detección de objetos huérfanos
  Como Seguridad de la Información
  Quiero detectar objetos con propietario inválido
  Para asegurar que todo objeto tenga un responsable activo

  Escenario: Propietario deshabilitado en Entra ID
    Dado que "luis.tecnico@banco.com" es propietario técnico de los objetos "OBJ-000070" y "OBJ-000071"
    Cuando "luis.tecnico@banco.com" es deshabilitado en Entra ID
    Y se ejecuta la sincronización de usuarios
    Entonces los objetos "OBJ-000070" y "OBJ-000071" quedan marcados como "Propietario inválido"
    Y se crea un hallazgo de categoría "Huérfano" por cada objeto
    Y se notifica al propietario funcional, al Custodio del área y a Seguridad
```

---

# E03 — Protección de Información

## US-015 — Cifrar la información sensible en reposo y en tránsito

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad (verificación) |
| Prioridad | Must · F1 |
| Requisitos | RF-PRT-01, RF-PRT-02, RF-PRT-05 |
| Reglas | RN-083, RN-085, RN-086 |
| Glosario | Sensitive Payload |
| RNF | RNF-SEG-02, RNF-SEG-03, RNF-SEG-04 |

**Descripción:** Como Seguridad de la Información, quiero que toda la información sensible se cifre en reposo y en tránsito, para que un acceso no autorizado a la base de datos, los respaldos o la red no exponga secretos.

**Criterios de aceptación:**
1. Cifrado de sobre: DEK AES-256-GCM por objeto, envuelta con una KEK local: un certificado RSA no exportable instalado en el almacén de certificados de la máquina (RN-083, RNF-SEG-03, DEC-35).
2. No existe ninguna ruta de código que persista un Sensitive Payload sin cifrar. Si falla el cifrado, se rechaza la operación (fail-closed).
3. Toda comunicación usa TLS 1.3 (RNF-SEG-02). Se rechazan las conexiones HTTP o con TLS inferior.
4. La base de datos tiene además TDE activado. Los respaldos están cifrados.
5. Solo los endpoints de revelado o recuperación devuelven payloads (RN-086).

```gherkin
# language: es
Característica: Cifrado de información sensible
  Como Seguridad de la Información
  Quiero que la información sensible esté cifrada en reposo y en tránsito
  Para que un acceso indebido a la infraestructura no exponga secretos

  Escenario: Valor persistido cifrado
    Dado que un Custodio registró el secreto "OBJ-000120" en modo "Interno"
    Cuando se inspecciona directamente la tabla de valores sensibles en la base de datos
    Entonces el valor almacenado es un texto cifrado distinto del valor original
    Y la clave de datos del objeto está envuelta por el certificado local que actúa como llave maestra

  Escenario: Conexión sin TLS rechazada
    Cuando un cliente intenta conectarse a la API mediante HTTP sin cifrar
    Entonces la conexión es rechazada o redirigida a HTTPS sin procesar la solicitud

  Escenario: Conexión con TLS 1.1 rechazada
    Cuando un cliente intenta conectarse a la API usando TLS 1.1
    Entonces la negociación TLS falla

  Escenario: Endpoints de consulta no devuelven valores secretos
    Cuando un usuario con permiso de consulta obtiene el listado de objetos por API
    Entonces ninguna propiedad de la respuesta contiene valores secretos
```

---

## US-016 — Revelar valor sensible con autorización

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario, Operador, Seguridad (con acceso temporal) |
| Prioridad | Must · F1 |
| Requisitos | RF-PRT-03, RF-ROL-02, RF-TMP-01, RF-AUD-02, RF-USO-01, RF-ACC-09, RF-PRT-06 |
| Reglas | RN-042, RN-060, RN-079, RN-084, RN-108, RN-113 |
| Glosario | Sensitive Payload, Temporary Access, Audit Event, Usage Event |

**Descripción:** Como Operador con acceso temporal aprobado, quiero revelar o copiar el valor de una credencial, para ejecutar la tarea autorizada, dejando constancia de cada revelado.

**Criterios de aceptación:**
1. El botón «Revelar» solo está habilitado si existe un Temporary Access activo del usuario sobre el objeto (RN-042), o si el objeto no es crítico ni Restringido y pertenece a un grupo personal del usuario (RN-108).
2. El valor se muestra durante un máximo de 30 s. La copia al portapapeles se limpia a los 30 s (RN-084).
3. Cada revelado o copia genera un Audit Event y un Usage Event (RN-060).
4. Si no se puede registrar la auditoría, no se revela el valor (RN-079).
5. Revelar exige una reautenticación reciente con MFA (≤ 15 min) (RNF-SEG-01).
6. En objetos con llave dividida solo se revela el componente que corresponde al usuario (US-057).

```gherkin
# language: es
Característica: Revelado autorizado de valores sensibles
  Como Operador con acceso temporal aprobado
  Quiero revelar el valor de una credencial
  Para ejecutar la tarea autorizada dejando evidencia

  Antecedentes:
    Dado que estoy autenticado como "op1@banco.com" con rol "Operador"
    Y el objeto "OBJ-000300" tiene sensibilidad "Confidencial"

  Escenario: Revelado con acceso temporal activo
    Dado que tengo un acceso temporal activo sobre "OBJ-000300" hasta dentro de 2 horas
    Y completé MFA hace menos de 15 minutos
    Cuando selecciono "Revelar" en el objeto "OBJ-000300"
    Entonces el valor se muestra durante un máximo de 30 segundos
    Y se registra un evento de auditoría "SECRET_REVEALED"
    Y se registra un evento de uso con acción "Reveal"

  Escenario: Revelado sin acceso temporal
    Dado que no tengo un acceso temporal activo sobre "OBJ-000300"
    Cuando intento revelar el valor de "OBJ-000300"
    Entonces el sistema deniega la operación y me ofrece crear una solicitud de acceso
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Falla del registro de auditoría impide el revelado
    Dado que tengo un acceso temporal activo sobre "OBJ-000300"
    Y el almacén de auditoría no está disponible
    Cuando selecciono "Revelar" en el objeto "OBJ-000300"
    Entonces el valor no se muestra
    Y el sistema informa que la operación no pudo completarse

  Escenario: Reautenticación requerida
    Dado que tengo un acceso temporal activo sobre "OBJ-000300"
    Y mi última autenticación MFA fue hace 40 minutos
    Cuando selecciono "Revelar" en el objeto "OBJ-000300"
    Entonces el sistema me solicita reautenticación con MFA antes de mostrar el valor

  Escenario: Revelado directo en un grupo personal
    Dado que soy el único miembro del grupo personal "GRP-PERS-OP1"
    Y el objeto "OBJ-000310" de criticidad "Medio" y sensibilidad "Confidencial" está asignado a "GRP-PERS-OP1"
    Y completé MFA hace menos de 15 minutos
    Cuando selecciono "Revelar" en el objeto "OBJ-000310"
    Entonces el valor se muestra sin necesidad de solicitud ni aprobación
    Y se registra un evento de auditoría "SECRET_REVEALED"
```

---

## US-017 — Descargar llave privada o material de clave con autorización explícita

| Campo | Valor |
|---|---|
| Actor | Propietario Técnico, Custodio, par del grupo (aprobador) |
| Prioridad | Must · F1 |
| Requisitos | RF-PRT-04, RF-ROL-02, RF-APR-04, RF-APR-06, RF-AUD-02, RF-ACC-09, RF-PRT-06 |
| Reglas | RN-017, RN-026, RN-052, RN-057, RN-096, RN-105, RN-106, RN-108, RN-116 |
| Glosario | Certificate, Cryptographic Key, Temporary Access, Approval, Security Group |

**Descripción:** Como Propietario Técnico, quiero descargar el certificado con su llave privada (PFX) o el material de una clave criptográfica solo después de que otro miembro de mi grupo lo autorice, para instalarlo en el servidor destino sin riesgo de uso indebido.

**Criterios de aceptación:**
1. La descarga de la parte pública del certificado solo requiere Consultar.
2. Descargar la llave privada o el material de clave requiere **siempre** un Temporary Access aprobado por otro miembro del grupo del objeto (cuatro ojos) (RN-017, RN-026, RN-096, RN-106). La llave privada y el material de clave son siempre Restringidos (RN-004), así que estos objetos no pueden estar en un grupo personal (RN-104).
3. El PFX descargado se protege con una contraseña de un solo uso, que se muestra una única vez al usuario.
4. El enlace de descarga es de un solo uso y caduca en 5 min o al fin del acceso temporal, lo que ocurra antes (RN-057).
5. Cada descarga se audita con la huella del archivo entregado y se notifica a los demás miembros del grupo (RN-105).
6. En certificados con llave dividida, el PFX se entrega protegido con su contraseña dividida (componente del grupo + componente de Seguridad), en lugar de una contraseña de un solo uso (US-057).

```gherkin
# language: es
Característica: Descarga autorizada de llaves privadas
  Como Propietario Técnico
  Quiero descargar llaves privadas solo con la autorización de otro miembro de mi grupo
  Para instalarlas sin riesgo de uso indebido

  Antecedentes:
    Dado que el certificado "OBJ-000045" tiene llave privada y está asignado al grupo "GRP-CANALES"
    Y estoy autenticado como "luis.tecnico@banco.com", Propietario Técnico de "OBJ-000045" y miembro de "GRP-CANALES"

  Escenario: Descarga de certificado público
    Cuando descargo la parte pública de "OBJ-000045"
    Entonces obtengo el archivo en formato PEM sin llave privada
    Y se registra un evento de auditoría "CERTIFICATE_PUBLIC_DOWNLOADED"

  Escenario: Descarga de llave privada sin aprobación
    Dado que no tengo una solicitud de descarga aprobada para "OBJ-000045"
    Cuando intento descargar la llave privada de "OBJ-000045"
    Entonces el sistema deniega la descarga y me ofrece crear una solicitud de acceso

  Escenario: Descarga de llave privada aprobada por un par del grupo
    Dado que "maria@banco.com", miembro de "GRP-CANALES", aprobó mi solicitud de descarga
    Y tengo un acceso temporal activo
    Cuando descargo la llave privada de "OBJ-000045"
    Entonces obtengo un archivo PFX protegido con una contraseña de un solo uso
    Y la contraseña se muestra una única vez
    Y el enlace de descarga no puede reutilizarse
    Y se registra un evento de auditoría "PRIVATE_KEY_DOWNLOADED" con la huella del archivo entregado
    Y los demás miembros del grupo "GRP-CANALES" reciben una notificación de la descarga

  Escenario: Un certificado con llave privada no puede estar en un grupo personal
    Dado que soy el único miembro del grupo personal "GRP-PERS-LUIS"
    Y el certificado "OBJ-000046" de criticidad "Medio" tiene llave privada
    Cuando intento asignar "OBJ-000046" al grupo "GRP-PERS-LUIS"
    Entonces el sistema rechaza la asignación indicando que los objetos Restringidos requieren un grupo de al menos dos miembros
```

---

## US-018 — Retirada: referencia a Azure Key Vault

| Campo | Valor |
|---|---|
| Actor | — |
| Prioridad | Retirada · DEC-35 |
| Requisitos | Retirado: RF-INT-04 |
| Reglas | Retiradas: RN-080, RN-081, RN-082 |
| Glosario | Vault Reference |

**Estado:** Retirada (DEC-35). La plataforma no integra Azure Key Vault ni ningún otro servicio de nube en la Fase 1. Todo objeto con Sensitive Payload se registra en modo **Interno** (cifrado local); el modo **Solo metadatos** sigue disponible para certificados públicos y cuentas de servicio sin secreto propio (US-004). No existe un modo de referencia a una bóveda externa.

**Criterios de aceptación:** No aplica; historia retirada. Los criterios de aceptación y los escenarios Gherkin originales se retiraron junto con ella, por no ser aplicables sin Azure Key Vault.

```gherkin
# language: es
Característica: Historia retirada
  # US-018 se retiró por DEC-35 (sin Azure Key Vault en la Fase 1).
  # No aplica ningún escenario.

  Escenario: Referencia informativa
    Dado que la plataforma no integra ningún servicio de nube en la Fase 1
    Entonces todo objeto con valor sensible se registra en modo Interno o Solo metadatos
```

---

# E04 — Versionamiento

## US-019 — Consultar historial de versiones y comparar cambios

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario, Auditor, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-VER-01, RF-VER-02, RF-VER-03 |
| Reglas | RN-092, RN-093 |
| Glosario | Object Version, Audit Event |

**Descripción:** Como Auditor, quiero consultar el historial completo de versiones de un objeto y comparar dos versiones, para verificar quién cambió qué, cuándo y por qué.

**Criterios de aceptación:**
1. Lista de versiones con número, autor, fecha y hora UTC, motivo y resumen de cambios (RN-092).
2. Comparación lado a lado de dos versiones cualquiera. Los campos sensibles aparecen como «Valor modificado» (RN-093).
3. Las versiones no son editables ni eliminables.
4. Consultar el historial genera un Audit Event.

```gherkin
# language: es
Característica: Historial de versiones
  Como Auditor
  Quiero consultar y comparar versiones de un objeto
  Para verificar quién cambió qué, cuándo y por qué

  Antecedentes:
    Dado que estoy autenticado con rol "Auditor"
    Y el objeto "OBJ-000045" tiene 4 versiones

  Escenario: Consulta del historial
    Cuando consulto el historial de versiones de "OBJ-000045"
    Entonces veo 4 versiones con autor, fecha, hora UTC y motivo del cambio

  Escenario: Comparación de versiones con valor sensible modificado
    Dado que entre la versión 3 y la versión 4 cambió la fecha de expiración y el valor secreto
    Cuando comparo la versión 3 con la versión 4
    Entonces veo la fecha de expiración anterior y la nueva
    Y el valor secreto se muestra únicamente como "Valor modificado"

  Escenario: Las versiones son inmutables
    Cuando intento modificar la versión 2 de "OBJ-000045" mediante la API
    Entonces el sistema responde que la operación no está permitida
```

---

# E05 — Vencimientos

## US-020 — Configurar políticas de expiración

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información (aprueba), Administrador (propone) |
| Prioridad | Must · F1 |
| Requisitos | RF-VEN-02, RF-VEN-03, RF-VEN-04 |
| Reglas | RN-046, RN-061, RN-062, RN-063, RN-069, RN-070, RN-096 |
| Glosario | Expiration Policy, Escalation, Alert |

**Descripción:** Como Seguridad de la Información, quiero configurar políticas de expiración por tipo, subtipo y criticidad, con umbrales, destinatarios, reglas de escalamiento y vigencia máxima, para adaptar el alertamiento al riesgo de cada objeto.

**Criterios de aceptación:**
1. Umbrales por defecto 180, 120, 90, 60, 30, 15, 7 y 1 días. Los umbrales {30, 7, 1} no se pueden desactivar para objetos Críticos o Altos (RN-061).
2. Por umbral se configuran destinatarios (niveles N1–N4) y canal.
3. Las reglas de escalamiento incluyen el plazo sin reconocimiento por severidad (RN-070).
4. Se aplica la política más específica (RN-062).
5. Crear o modificar una política requiere cuatro ojos y genera una nueva versión (RN-046, RN-096).
6. Se configura la vigencia o rotación máxima por tipo (usada en RN-014 y RN-020).

```gherkin
# language: es
Característica: Configurar políticas de expiración
  Como Seguridad de la Información
  Quiero configurar políticas de expiración
  Para adaptar el alertamiento al riesgo de cada objeto

  Escenario: Creación de política con aprobación de cuatro ojos
    Dado que estoy autenticado como "admin@banco.com" con rol "Administrador"
    Cuando propongo una política para tipo "Certificado", subtipo "SWIFT" y criticidad "Crítico" con umbrales 180, 90, 60, 30, 15, 7 y 1
    Entonces la política queda en estado "Pendiente de aprobación"
    Cuando "seg1@banco.com" con rol "Seguridad" aprueba la política
    Entonces la política queda "Vigente" como versión 1

  Escenario: Umbrales mínimos protegidos para objetos críticos
    Dado que estoy autenticado con rol "Seguridad"
    Cuando intento guardar una política para criticidad "Crítico" sin el umbral de 7 días
    Entonces el sistema rechaza la política indicando que los umbrales 30, 7 y 1 son obligatorios

  Escenario: Prevalece la política más específica
    Dado que existe una política global con umbrales 90, 30 y 7
    Y existe una política para subtipo "SWIFT" y criticidad "Crítico" con umbrales 180, 120, 90, 60, 30, 15, 7 y 1
    Cuando se evalúa el certificado SWIFT crítico "OBJ-000060"
    Entonces se aplica la política de subtipo "SWIFT" y criticidad "Crítico"
```

---

## US-021 — Monitorear continuamente las fechas de expiración

| Campo | Valor |
|---|---|
| Actor | Sistema |
| Prioridad | Must · F1 |
| Requisitos | RF-VEN-01, RF-VEN-05 |
| Reglas | RN-014, RN-025, RN-063, RN-094 |
| Glosario | Expiration Policy, Lifecycle State / Expiration Status |

**Descripción:** Como Custodio, quiero que el sistema recalcule automática y continuamente el estado de expiración de todos los objetos, para saber en todo momento qué está vigente, próximo a vencer o expirado.

**Criterios de aceptación:**
1. El proceso de monitoreo se ejecuta al menos cada hora y, además, ante cada cambio de fecha de expiración (RN-094).
2. Estados calculados: Vigente, Próximo a vencer, Expirado, Sin vencimiento.
3. Considera la expiración propia, la calculada por rotación (RN-014) y la calculada por criptoperíodo (RN-025).
4. Solo se evalúan objetos Activos y Suspendidos.
5. La ejecución registra el inicio, el fin, los objetos evaluados y los errores. Un fallo del proceso genera una alerta al Administrador.

```gherkin
# language: es
Característica: Monitoreo continuo de vencimientos
  Como Custodio
  Quiero que el sistema calcule el estado de expiración automáticamente
  Para conocer en todo momento el riesgo de vencimiento

  Esquema del escenario: Cálculo del estado de expiración
    Dado que la fecha actual es "2026-09-24"
    Y el objeto activo "OBJ-000900" tiene fecha de expiración "<fecha>"
    Cuando se ejecuta el proceso de monitoreo
    Entonces el estado de expiración de "OBJ-000900" es "<estado>"

    Ejemplos:
      | fecha      | estado           |
      | 2027-12-31 | Vigente          |
      | 2027-01-15 | Próximo a vencer |
      | 2026-09-20 | Expirado         |

  Escenario: Objetos desactivados no se monitorean
    Dado que el objeto "OBJ-000901" está en estado "Desactivado"
    Cuando se ejecuta el proceso de monitoreo
    Entonces no se generan alertas para "OBJ-000901"

  Escenario: Fallo del proceso de monitoreo
    Cuando el proceso de monitoreo termina con error
    Entonces se notifica al Administrador
    Y se registra un evento técnico con el detalle del error sin datos sensibles
```

---

## US-022 — Generar alertas de vencimiento por umbrales

| Campo | Valor |
|---|---|
| Actor | Sistema, Propietarios, Custodio |
| Prioridad | Must · F1 |
| Requisitos | RF-VEN-03, RF-INT-06 |
| Reglas | RN-061, RN-064, RN-065, RN-067 |
| Glosario | Alert, Notification, Expiration Policy |

**Descripción:** Como Propietario Técnico, quiero recibir alertas cuando mis objetos se acercan a su vencimiento en cada umbral configurado, para renovarlos a tiempo.

**Criterios de aceptación:**
1. Se genera una alerta al alcanzar cada umbral activo: 180, 120, 90, 60, 30, 15, 7 y 1 días.
2. Una sola alerta por objeto y umbral (RN-064).
3. La severidad sigue RN-065.
4. La notificación incluye código, nombre, tipo, ambiente, fecha de expiración, días restantes y enlace. Nunca incluye valores (RN-067).
5. Las alertas son visibles en la bandeja de alertas del usuario.

```gherkin
# language: es
Característica: Alertas de vencimiento por umbrales
  Como Propietario Técnico
  Quiero recibir alertas en cada umbral de vencimiento
  Para renovar los objetos a tiempo

  Esquema del escenario: Generación de alerta en cada umbral
    Dado que el objeto activo "OBJ-000100" con criticidad "Medio" vence en <dias> días
    Y la política aplicable tiene activo el umbral de <dias> días
    Cuando se ejecuta el proceso de monitoreo
    Entonces se genera una alerta de umbral <dias> días con severidad "<severidad>"
    Y el Propietario Técnico recibe una notificación por correo

    Ejemplos:
      | dias | severidad |
      | 180  | Baja      |
      | 120  | Baja      |
      | 90   | Baja      |
      | 60   | Baja      |
      | 30   | Media     |
      | 15   | Media     |
      | 7    | Alta      |
      | 1    | Alta      |

  Escenario: Idempotencia de alertas
    Dado que ya existe una alerta del umbral de 30 días para "OBJ-000100"
    Cuando el proceso de monitoreo se ejecuta nuevamente el mismo día
    Entonces no se genera una segunda alerta del umbral de 30 días

  Escenario: La notificación no contiene valores sensibles
    Cuando se envía una notificación de vencimiento del secreto "OBJ-000120"
    Entonces el mensaje contiene el código, nombre, fecha de expiración y días restantes
    Pero el mensaje no contiene el valor del secreto
```

---

## US-023 — Escalar alertas automáticamente

| Campo | Valor |
|---|---|
| Actor | Sistema, Propietarios, Custodio, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-VEN-04 |
| Reglas | RN-069, RN-070, RN-071 |
| Glosario | Escalation, Alert |

**Descripción:** Como Seguridad de la Información, quiero que las alertas no atendidas escalen automáticamente a niveles superiores según reglas configurables, para evitar vencimientos por falta de atención.

**Criterios de aceptación:**
1. Niveles N1 Propietario Técnico → N2 Propietario Funcional → N3 Custodio y manager → N4 Seguridad (RN-069).
2. Escala si no se reconoce en el plazo de su severidad (RN-070).
3. Los objetos Críticos a ≤ 30 días y todos los expirados notifican directamente hasta N3 (RN-071).
4. Cada escalamiento queda registrado en la alerta y en la auditoría.

```gherkin
# language: es
Característica: Escalamiento automático de alertas
  Como Seguridad de la Información
  Quiero que las alertas no atendidas escalen automáticamente
  Para evitar vencimientos por falta de atención

  Escenario: Escalamiento por falta de reconocimiento
    Dado que existe una alerta de severidad "Alta" para "OBJ-000100" notificada al nivel N1 hace 25 horas
    Y la alerta no ha sido reconocida
    Y el plazo de reconocimiento para severidad "Alta" es de 24 horas
    Cuando se ejecuta el proceso de escalamiento
    Entonces la alerta queda en estado "Escalada" al nivel N2
    Y se notifica al Propietario Funcional de "OBJ-000100"
    Y se registra un evento de auditoría "ALERT_ESCALATED"

  Escenario: Objeto crítico próximo a vencer notifica directamente hasta N3
    Dado que el objeto "OBJ-000060" tiene criticidad "Crítico" y vence en 30 días
    Cuando se genera la alerta del umbral de 30 días
    Entonces se notifica simultáneamente a los niveles N1, N2 y N3

  Escenario: Alerta reconocida no escala por plazo
    Dado que existe una alerta de severidad "Media" para "OBJ-000101" reconocida por el Propietario Técnico
    Cuando transcurren 80 horas
    Entonces la alerta no escala por falta de reconocimiento
```

---

## US-024 — Identificar objetos expirados y gestionar alertas

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietarios, Operador |
| Prioridad | Must · F1 |
| Requisitos | RF-VEN-05, RF-DES-02 |
| Reglas | RN-065, RN-066, RN-068, RN-070 |
| Glosario | Alert, Lifecycle State / Expiration Status |

**Descripción:** Como Operador, quiero ver los objetos expirados y reconocer las alertas que me corresponden, para priorizar la atención y dejar constancia del seguimiento.

**Criterios de aceptación:**
1. Vista de objetos expirados, filtrable, dentro del ámbito del usuario.
2. Los expirados generan una alerta Crítica diaria hasta su resolución (RN-068).
3. Reconocer requiere un comentario y no resuelve la alerta (RN-070).
4. La alerta se resuelve automáticamente al renovar o desactivar el objeto (RN-066).

```gherkin
# language: es
Característica: Gestión de objetos expirados y alertas
  Como Operador
  Quiero identificar objetos expirados y reconocer alertas
  Para priorizar la atención y dejar constancia del seguimiento

  Escenario: Alerta diaria de objeto expirado
    Dado que el objeto "OBJ-000003" expiró hace 2 días y sigue activo
    Cuando se ejecuta el proceso de monitoreo diario
    Entonces se genera una alerta de severidad "Crítica" para "OBJ-000003"

  Escenario: Reconocimiento de alerta
    Dado que tengo una alerta abierta para "OBJ-000003"
    Cuando reconozco la alerta con el comentario "Renovación programada para hoy 18:00"
    Entonces la alerta queda en estado "Reconocida"
    Y la alerta permanece abierta hasta la renovación del objeto

  Escenario: Resolución automática al desactivar
    Dado que existe una alerta abierta para "OBJ-000003"
    Cuando un Custodio desactiva "OBJ-000003"
    Entonces la alerta queda en estado "Cerrada automáticamente"
```

---

# E06 — Gestión de Accesos

## US-025 — Autenticarse con Entra ID (SSO, MFA y acceso condicional)

| Campo | Valor |
|---|---|
| Actor | Todos los usuarios |
| Prioridad | Must · F1 |
| Requisitos | RF-ACC-02, RF-ACC-03, RF-ACC-04, RF-ACC-05, RF-INT-03 |
| Reglas | RN-030, RN-031, RN-032 |
| Glosario | User, Security Group |
| RNF | RNF-SEG-01, RNF-SEG-06 |

**Descripción:** Como usuario corporativo, quiero iniciar sesión con mi cuenta de Microsoft Entra ID mediante SSO y MFA, para acceder de forma segura sin credenciales adicionales.

**Criterios de aceptación:**
1. Autenticación con OpenID Connect / OAuth 2.0 contra Entra ID. Las únicas cuentas locales son las de emergencia (US-062, RN-030).
2. MFA obligatorio. Se rechazan tokens sin el claim de MFA (`amr` contiene `mfa`) (RN-032).
3. Se respetan las políticas de acceso condicional de Entra ID (dispositivo administrado, ubicación, riesgo). Las acciones sensibles pueden exigir un *authentication context* específico.
4. Aprovisionamiento just-in-time del usuario en su primer inicio de sesión.
5. Sesión inactiva > 15 min → cierre de sesión. Duración máxima de sesión: 8 h (RNF-SEG-06).
6. Los inicios de sesión exitosos y fallidos se auditan.

```gherkin
# language: es
Característica: Autenticación corporativa con Entra ID
  Como usuario corporativo
  Quiero autenticarme con SSO y MFA
  Para acceder de forma segura con mi identidad corporativa

  Escenario: Inicio de sesión exitoso con MFA
    Dado que "op1@banco.com" es un usuario activo en Entra ID
    Cuando inicia sesión mediante SSO y completa MFA
    Entonces accede a la plataforma con los roles que tiene asignados
    Y se registra un evento de auditoría "LOGIN_SUCCEEDED"

  Escenario: Token sin MFA rechazado
    Dado que "op1@banco.com" obtiene un token sin el método de autenticación MFA
    Cuando intenta acceder a la plataforma
    Entonces el acceso es denegado
    Y se registra un evento de auditoría "LOGIN_DENIED" con motivo "MFA requerido"

  Escenario: Acceso condicional bloquea dispositivo no administrado
    Dado que existe una política de acceso condicional que exige dispositivo administrado
    Cuando "op1@banco.com" intenta iniciar sesión desde un dispositivo no administrado
    Entonces Entra ID bloquea el inicio de sesión
    Y el usuario no accede a la plataforma

  Escenario: Cierre de sesión por inactividad
    Dado que "op1@banco.com" tiene una sesión activa
    Cuando permanece inactivo durante 15 minutos
    Entonces la sesión se cierra y debe autenticarse nuevamente
```

---

## US-026 — Integrar con Active Directory para validar cuentas de servicio

| Campo | Valor |
|---|---|
| Actor | Administrador, Sistema |
| Prioridad | Must · F1 (DEC-06) |
| Requisitos | RF-ACC-01, RF-INT-02 |
| Reglas | RN-027, RN-030, RN-031 |
| Glosario | User, Service Account |

**Descripción:** Como Administrador, quiero integrar la plataforma con Active Directory on-premise, para validar la existencia y el estado de las cuentas de servicio de AD registradas. AD no se usa ni para autenticar ni para grupos: los grupos son propios de la plataforma (DEC-12).

**Criterios de aceptación:**
1. Conexión LDAPS (puerto 636) con una cuenta de servicio de mínimo privilegio y solo lectura.
2. La autenticación de usuarios **siempre** se hace con Entra ID. De AD no se leen ni se usan grupos.
3. Valida la existencia y el estado (habilitada o deshabilitada, expiración de contraseña) de las cuentas de servicio de AD registradas.
4. Sincronización periódica configurable. Los errores de conexión generan una alerta al Administrador.

```gherkin
# language: es
Característica: Integración con Active Directory
  Como Administrador
  Quiero integrar la plataforma con Active Directory
  Para validar el estado de las cuentas de servicio registradas

  Escenario: Validación de cuenta de servicio de AD
    Dado que la integración con Active Directory está habilitada mediante LDAPS
    Y el objeto "OBJ-000330" es una cuenta de servicio con origen "AD" e identificador "svc_batch"
    Cuando se ejecuta la sincronización con Active Directory
    Y la cuenta "svc_batch" está deshabilitada en Active Directory
    Entonces el objeto "OBJ-000330" se marca con la condición "Deshabilitada en origen"
    Y se notifica al Propietario Técnico

  Escenario: Conexión no cifrada no permitida
    Cuando el Administrador intenta configurar la conexión a Active Directory mediante LDAP sin cifrado
    Entonces el sistema rechaza la configuración
```

---

## US-027 — Administrar grupos de acceso

| Campo | Valor |
|---|---|
| Actor | Administrador (crea grupos), Responsable del grupo (gestiona miembros) |
| Prioridad | Must · F1 |
| Requisitos | RF-ACC-06, RF-ROL-04, RF-ROL-05, RF-ACC-09, RF-ROL-06 |
| Reglas | RN-033, RN-034, RN-035, RN-103, RN-104, RN-108 |
| Glosario | Security Group, User, Role |

**Descripción:** Como Administrador, quiero crear grupos de acceso en la plataforma y designar a sus responsables, para organizar a las personas que pueden acceder a cada conjunto de objetos sin depender de los grupos de Entra ID ni de Active Directory.

**Criterios de aceptación:**
1. Los grupos se crean solo en la plataforma, con código, nombre, descripción, área (opcional) y al menos un Responsable (RN-033, RN-034). No se importan de Entra ID ni de AD.
2. El Responsable del grupo (o el Administrador) agrega y retira miembros, elegidos entre los usuarios activos de la plataforma.
3. No pueden ser miembros los usuarios con rol Auditor o Seguridad ni los usuarios deshabilitados (RN-103). El Administrador sí puede ser miembro (DEC-17).
4. Los grupos no otorgan roles. Los roles se asignan directamente a cada persona (US-029).
5. Cada alta, baja o cambio de Responsable se audita y se notifica a los miembros del grupo.
6. Retirar a un miembro o desactivar el grupo quita de inmediato la visibilidad, cancela las solicitudes pendientes y revoca los accesos temporales derivados (RN-035).
7. Si un grupo con objetos Críticos o Restringidos queda con menos de dos miembros activos, se alerta al Responsable y al Administrador (RN-104).
8. Vista del grupo: miembros, responsables, objetos asignados, accesos activos y configuración de notificaciones.
9. Un grupo con un solo miembro activo es un **grupo personal**: su miembro usa los objetos no críticos ni Restringidos sin autorizaciones y no puede tener objetos Críticos ni Restringidos (RN-104, RN-108).

```gherkin
# language: es
Característica: Administración de grupos de acceso
  Como Administrador
  Quiero crear grupos de acceso y designar responsables
  Para organizar a las personas que pueden acceder a los objetos

  Escenario: Creación de grupo con responsable
    Dado que estoy autenticado como "admin@banco.com" con rol "Administrador"
    Cuando creo el grupo "GRP-CANALES" con la descripción "Equipo de Canales Digitales"
    Y designo como responsable a "maria@banco.com"
    Entonces el grupo "GRP-CANALES" queda "Activo" con "maria@banco.com" como responsable y miembro
    Y se registra un evento de auditoría "GROUP_CREATED"

  Escenario: El responsable agrega un miembro
    Dado que estoy autenticado como "maria@banco.com", responsable del grupo "GRP-CANALES"
    Cuando agrego a "pedro@banco.com" como miembro del grupo "GRP-CANALES"
    Entonces "pedro@banco.com" puede ver los metadatos de los objetos asignados a "GRP-CANALES"
    Y los demás miembros del grupo reciben una notificación del alta

  Escenario: Un miembro que no es responsable no puede gestionar miembros
    Dado que estoy autenticado como "pedro@banco.com", miembro sin responsabilidad en "GRP-CANALES"
    Cuando intento agregar a "ana@banco.com" al grupo "GRP-CANALES"
    Entonces el sistema deniega la operación
    Y se registra un evento de auditoría con resultado "Denied"

  Esquema del escenario: Usuarios que no pueden ser miembros
    Dado que "<usuario>" tiene el rol "<rol>"
    Cuando el responsable intenta agregar a "<usuario>" al grupo "GRP-CANALES"
    Entonces el sistema rechaza el alta por conflicto de segregación de funciones

    Ejemplos:
      | usuario            | rol           |
      | seg1@banco.com     | Seguridad     |
      | auditor1@banco.com | Auditor       |

  Escenario: Retirar a un miembro revoca sus accesos
    Dado que "pedro@banco.com" tiene un acceso temporal activo sobre "OBJ-000045", asignado a "GRP-CANALES"
    Cuando el responsable retira a "pedro@banco.com" del grupo "GRP-CANALES"
    Entonces el acceso temporal de "pedro@banco.com" queda en estado "Revocado"
    Y "pedro@banco.com" deja de ver "OBJ-000045"

  Escenario: Grupo con objetos críticos por debajo del mínimo de miembros
    Dado que el grupo "GRP-SWIFT" tiene asignado el objeto crítico "OBJ-000060" y 2 miembros activos
    Cuando uno de los miembros es retirado del grupo "GRP-SWIFT"
    Entonces se genera una alerta al responsable y al Administrador indicando que el grupo necesita al menos dos miembros activos
    Y el objeto "OBJ-000060" queda bloqueado para revelado y descarga

  Escenario: El Administrador puede ser miembro de un grupo
    Dado que "admin@banco.com" tiene los roles "Administrador" y "Custodio"
    Cuando el responsable agrega a "admin@banco.com" al grupo "GRP-CANALES"
    Entonces "admin@banco.com" queda como miembro activo del grupo "GRP-CANALES"

  Escenario: Un grupo de un solo miembro es un grupo personal
    Dado que el grupo "GRP-PERS-OP1" tiene como único miembro activo a "op1@banco.com"
    Cuando consulto el detalle del grupo "GRP-PERS-OP1"
    Entonces el grupo figura como "Grupo personal"
    Y no admite la asignación de objetos críticos ni restringidos
```

---

## US-028 — Asignar objetos a usuarios, grupos, áreas y aplicaciones

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario |
| Prioridad | Must · F1 |
| Requisitos | RF-ACC-07, RF-ROL-04 |
| Reglas | RN-010, RN-011, RN-090, RN-091, RN-104 |
| Glosario | Managed Object, Security Group, Area, Application, User |

**Descripción:** Como Custodio, quiero asignar cada objeto a usuarios, grupos, áreas y aplicaciones, para controlar quién puede verlo y qué aplicaciones pueden consumirlo, bajo el principio de mínimo privilegio.

**Criterios de aceptación:**
1. Un objeto pertenece a un área (RN-090) y puede asignarse además a usuarios, grupos y aplicaciones.
2. Asignar un objeto otorga visibilidad (Consultar) y nunca acceso al payload (RN-042, RN-043).
3. Asignar un objeto a una aplicación con identidad registrada la autoriza a recuperarlo por API (RN-091). *F2 (DEC-03): en F1 la asignación a aplicaciones documenta la dependencia y otorga visibilidad, no recuperación.*
4. Se muestra quién tiene visibilidad efectiva sobre el objeto, con el origen de cada acceso (directo, grupo, área, propietario).
5. Toda asignación o desasignación se audita.
6. Un objeto Crítico o Restringido debe estar asignado al menos a un grupo con dos o más miembros activos y no puede asignarse a un grupo personal (RN-104).

```gherkin
# language: es
Característica: Asignación de objetos
  Como Custodio
  Quiero asignar objetos a usuarios, grupos, áreas y aplicaciones
  Para controlar la visibilidad y el consumo con mínimo privilegio

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" en el ámbito del objeto "OBJ-000120"

  Escenario: Asignación a grupo otorga solo visibilidad
    Cuando asigno "OBJ-000120" al grupo "GRP-OPS-PAGOS"
    Entonces los miembros de "GRP-OPS-PAGOS" pueden consultar los metadatos de "OBJ-000120"
    Pero no pueden revelar su valor sin un acceso temporal aprobado

  @F2
  Escenario: Asignación a aplicación habilita consumo por API
    Dado que la aplicación "Pagos" tiene registrada la identidad "sp-pagos-prod"
    Cuando asigno "OBJ-000120" a la aplicación "Pagos"
    Entonces la identidad "sp-pagos-prod" puede recuperar "OBJ-000120" mediante la API

  Escenario: Visibilidad efectiva
    Cuando consulto la visibilidad efectiva de "OBJ-000120"
    Entonces veo la lista de usuarios, grupos y aplicaciones con acceso y el origen de cada acceso

  Escenario: Objeto crítico requiere un grupo con al menos dos miembros
    Dado que el objeto "OBJ-000060" tiene criticidad "Crítico"
    Y el grupo "GRP-UNIPERSONAL" tiene un único miembro activo
    Cuando intento activar "OBJ-000060" asignado únicamente al grupo "GRP-UNIPERSONAL"
    Entonces el sistema rechaza la activación indicando que se requiere un grupo con al menos dos miembros activos
```

---

# E07 — Roles y Permisos

## US-029 — Administrar roles y permisos (RBAC)

| Campo | Valor |
|---|---|
| Actor | Administrador (propone), Seguridad (aprueba) |
| Prioridad | Must · F1 |
| Requisitos | RF-ROL-01, RF-ROL-02, RF-ROL-03, RF-ROL-04, RF-ROL-06 |
| Reglas | RN-036, RN-037, RN-038, RN-039, RN-040, RN-041, RN-042 |
| Glosario | Role, Permission |

**Descripción:** Como Administrador, quiero administrar los roles, sus permisos y sus asignaciones por ámbito, para aplicar control de acceso basado en roles con mínimo privilegio.

**Criterios de aceptación:**
1. Existen los roles de sistema Administrador, Custodio, Propietario, Operador, Auditor y Seguridad, que no se pueden eliminar (RN-039).
2. Permisos: Consultar, Crear, Modificar, Descargar, Eliminar, Aprobar, Exportar y Administrar (RN-040).
3. Los permisos por defecto siguen la matriz §4.2 del glosario.
4. Se pueden crear roles personalizados como subconjunto de permisos. No pueden incluir Descargar permanente sobre Confidencial o Restringida (RN-042).
5. Denegación por defecto (RN-040). Permiso efectivo según RN-041.
6. Asignar roles privilegiados requiere cuatro ojos y prohíbe la autoasignación (RN-038).
7. Consulta de permisos efectivos por usuario.
8. Los roles se asignan directamente a cada persona. No se heredan de los grupos de acceso (DEC-13).
9. Un usuario puede tener varios roles y sus permisos efectivos son la unión de todos ellos. Solo Auditor y Seguridad son exclusivos (DEC-17).

```gherkin
# language: es
Característica: Administración de roles y permisos
  Como Administrador
  Quiero administrar roles, permisos y asignaciones
  Para aplicar control de acceso basado en roles con mínimo privilegio

  Escenario: Denegación por defecto
    Dado que el usuario "nuevo@banco.com" no tiene roles asignados
    Cuando inicia sesión y consulta el inventario
    Entonces no ve ningún objeto

  Escenario: Un administrador no puede asignarse roles a sí mismo
    Dado que estoy autenticado como "admin@banco.com" con rol "Administrador"
    Cuando intento asignarme el rol "Seguridad"
    Entonces el sistema rechaza la operación
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Rol personalizado no puede incluir descarga permanente de objetos restringidos
    Dado que estoy autenticado con rol "Administrador"
    Cuando creo el rol personalizado "Soporte N2" con permiso "Descargar" permanente sobre sensibilidad "Restringida"
    Entonces el sistema rechaza la definición del rol

  Escenario: Los roles de sistema no pueden eliminarse
    Dado que estoy autenticado con rol "Administrador"
    Cuando intento eliminar el rol "Auditor"
    Entonces el sistema rechaza la operación

  Escenario: Usuario con varios roles
    Dado que "ana@banco.com" tiene los roles "Administrador" y "Custodio"
    Cuando consulto sus permisos efectivos
    Entonces incluyen los permisos de ambos roles
```

---

## US-030 — Aplicar segregación de funciones

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-ROL-05, RF-CUM-01, RF-ROL-06 |
| Reglas | RN-036, RN-054, RN-095, RN-103 |
| Glosario | Segregation of Duties, Role, Security Group |

**Descripción:** Como Seguridad de la Información, quiero que el sistema impida combinaciones de roles incompatibles y reporte cualquier violación, para cumplir el principio de segregación de funciones.

**Criterios de aceptación:**
1. Al asignar roles se aplica la matriz de incompatibilidades del glosario (§4.3) (RN-036). Los roles solo se asignan directamente a cada persona. Un usuario puede tener varios roles; solo Auditor y Seguridad son exclusivos (DEC-17).
2. Al agregar miembros a un grupo se valida que no tengan rol Auditor o Seguridad (RN-103). Si un miembro de un grupo va a recibir uno de esos roles, la asignación se bloquea hasta que se le retire de sus grupos.
3. Seguridad puede mantener la matriz de incompatibilidades con cuatro ojos.
4. Reporte de violaciones y excepciones.

```gherkin
# language: es
Característica: Segregación de funciones
  Como Seguridad de la Información
  Quiero impedir combinaciones de roles incompatibles
  Para cumplir el principio de segregación de funciones

  Esquema del escenario: Validación de combinaciones de roles
    Dado que el usuario "u1@banco.com" tiene el rol "<rol_actual>"
    Cuando se intenta asignarle el rol "<rol_nuevo>"
    Entonces el resultado de la asignación es "<resultado>"

    Ejemplos:
      | rol_actual    | rol_nuevo | resultado |
      | Auditor       | Custodio  | Bloqueado |
      | Administrador | Seguridad | Bloqueado |
      | Custodio      | Operador  | Permitido |
      | Administrador | Custodio  | Permitido |
      | Seguridad     | Custodio  | Bloqueado |
      | Operador      | Auditor   | Bloqueado |

  Escenario: Un miembro de grupo no puede recibir el rol Auditor
    Dado que "u2@banco.com" tiene rol "Operador" y es miembro del grupo de acceso "GRP-CANALES"
    Cuando se intenta asignarle el rol "Auditor"
    Entonces el sistema bloquea la asignación indicando que primero debe retirarse de sus grupos de acceso
```

---

# E08 — Solicitudes y Aprobaciones

## US-031 — Solicitar acceso a información sensible

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario, Operador, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-APR-01, RF-TMP-01, RF-TMP-02, RF-APR-07 |
| Reglas | RN-044, RN-047, RN-048, RN-049, RN-050, RN-106, RN-108, RN-122 |
| Glosario | Access Request, Access Policy, Temporary Access |

**Descripción:** Como Operador, quiero solicitar acceso temporal a un objeto indicando la justificación, el tiempo de acceso y el aprobador, para obtener autorización formal antes de usar información sensible.

**Criterios de aceptación:**
1. Campos obligatorios: objeto(s), acción (revelar, descargar llave privada o material, modificar valor), justificación (≥ 20 caracteres), inicio, duración y aprobador responsable (RN-047).
2. Referencia de ticket o incidente opcional.
3. El sistema muestra la política aplicable, los niveles de aprobación y la duración máxima, y valida que la duración solicitada no la supere (RN-048).
4. Se elige entre acceso programado o JIT, si la política lo permite.
5. No se admiten solicitudes sobre objetos no Activos (RN-050).
6. El solicitante puede cancelar mientras la solicitud no esté resuelta. Si no se resuelve en el plazo, expira (RN-049).
7. Se notifica a los aprobadores del primer nivel.
8. En objetos Críticos, los aprobadores elegibles son los usuarios con rol Seguridad designados como titulares o suplentes (RN-122). En objetos Restringidos no críticos, son los demás miembros activos del grupo; si no hay ninguno, no se puede enviar la solicitud (RN-106). En un grupo personal no se requiere solicitud para objetos no críticos ni Restringidos (RN-108).

```gherkin
# language: es
Característica: Solicitud de acceso a información sensible
  Como Operador
  Quiero solicitar acceso temporal con justificación
  Para obtener autorización formal antes de usar información sensible

  Antecedentes:
    Dado que estoy autenticado como "op1@banco.com" con rol "Operador"
    Y el objeto activo "OBJ-000300" tiene sensibilidad "Confidencial"
    Y la política aplicable a "OBJ-000300" establece duración máxima de 8 horas y 1 nivel de aprobación

  Escenario: Solicitud válida
    Cuando solicito acceso de tipo "Revelar" a "OBJ-000300"
    Y indico la justificación "Actualizar cadena de conexión en servidor APP-PRD-07 por cambio CHG-1234"
    Y indico una duración de 2 horas en modalidad "JIT"
    Y selecciono como aprobador al Propietario Funcional de "OBJ-000300"
    Y envío la solicitud
    Entonces la solicitud queda en estado "Pendiente"
    Y el aprobador recibe una notificación

  Escenario: Duración superior al máximo de la política
    Cuando solicito acceso de tipo "Revelar" a "OBJ-000300" con una duración de 12 horas
    Entonces el sistema rechaza la solicitud indicando que la duración máxima es de 8 horas

  Esquema del escenario: Campos obligatorios
    Cuando envío una solicitud de acceso a "OBJ-000300" sin "<campo>"
    Entonces el sistema no permite enviar la solicitud

    Ejemplos:
      | campo                 |
      | justificación         |
      | duración              |
      | aprobador responsable |

  Escenario: Justificación insuficiente
    Cuando envío una solicitud de acceso a "OBJ-000300" con la justificación "urgente"
    Entonces el sistema indica que la justificación debe tener al menos 20 caracteres

  Escenario: Solicitud sobre objeto desactivado
    Dado que el objeto "OBJ-000200" está en estado "Desactivado"
    Cuando solicito acceso de tipo "Revelar" a "OBJ-000200"
    Entonces el sistema rechaza la solicitud

  Escenario: Expiración de solicitud sin respuesta
    Dado que envié una solicitud de acceso hace 73 horas
    Y ningún aprobador la ha resuelto
    Cuando se ejecuta el proceso de vencimiento de solicitudes
    Entonces la solicitud queda en estado "Expirada"
    Y recibo una notificación
```

---

## US-032 — Configurar flujos de aprobación

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información, Administrador (propone) |
| Prioridad | Must · F1 |
| Requisitos | RF-APR-02, RF-APR-03, RF-APR-04, RF-APR-06, RF-APR-07 |
| Reglas | RN-044, RN-045, RN-046, RN-052, RN-053, RN-106, RN-122 |
| Glosario | Access Policy, Approval, Four Eyes Principle, Security Group |

**Descripción:** Como Seguridad de la Información, quiero configurar flujos de aprobación según tipo, criticidad, sensibilidad, ambiente y acción, para adecuar el control al riesgo. En los objetos críticos aprueba Seguridad y en los restringidos, un par del grupo.

**Criterios de aceptación:**
1. Una Access Policy define: su condición (tipo, subtipo, criticidad, sensibilidad, ambiente, acción), los niveles secuenciales, los aprobadores elegibles por nivel (Propietario Funcional, Propietario Técnico, Custodio del área, usuarios con rol Seguridad), el número de aprobaciones por nivel, la duración máxima y si se permite JIT.
2. Las políticas por defecto de RN-045 vienen precargadas.
3. Flujos fijos: en objetos **Críticos**, una aprobación de Seguridad, titular o suplente (RN-122); en objetos **Restringidos no críticos**, una aprobación de un par del mismo grupo (RN-106). La política solo puede ajustar la duración máxima (sin superar 4 h) y habilitar o no la modalidad JIT.
4. Si coinciden varias políticas de objetos no críticos, prevalece la más restrictiva (RN-044).
5. Los cambios requieren cuatro ojos con Seguridad y se versionan (RN-046).
6. Simulador: dado un objeto y una acción, muestra el flujo resultante y los aprobadores elegibles.

```gherkin
# language: es
Característica: Configuración de flujos de aprobación
  Como Seguridad de la Información
  Quiero configurar flujos de aprobación
  Para adecuar el control de acceso al riesgo

  Escenario: Flujo de dos niveles para objetos de criticidad alta en producción
    Dado que estoy autenticado con rol "Seguridad"
    Cuando configuro una política para criticidad "Alto", sensibilidad "Confidencial", ambiente "Producción" y acción "Revelar"
    Y defino el nivel 1 con aprobador "Propietario Funcional" y el nivel 2 con aprobador "Custodio del área"
    Y defino duración máxima de 4 horas
    Y otro usuario de Seguridad aprueba la política
    Entonces la política queda vigente
    Y el simulador para el objeto "OBJ-000150" de criticidad "Alto" muestra 2 niveles de aprobación

  Escenario: Los objetos críticos se aprueban siempre por Seguridad
    Dado que estoy autenticado con rol "Seguridad"
    Cuando configuro una política para criticidad "Crítico" con aprobador "Propietario Funcional"
    Entonces el sistema rechaza la política indicando que los objetos críticos los aprueba Seguridad

  Escenario: Simulación del flujo de un objeto crítico
    Dado que el objeto crítico "OBJ-000060" está asignado al grupo "GRP-SWIFT"
    Cuando simulo el flujo de la acción "Revelar" solicitada por "op1@banco.com" sobre "OBJ-000060"
    Entonces el simulador muestra 1 nivel de aprobación
    Y los aprobadores elegibles son los usuarios con rol "Seguridad" designados como titulares o suplentes

  Escenario: Prevalece la política más restrictiva
    Dado que existe una política A con 1 nivel y 8 horas para ambiente "Producción"
    Y existe una política B con 2 niveles y 4 horas para criticidad "Alto"
    Cuando se evalúa una solicitud sobre un objeto de criticidad "Alto" en producción
    Entonces se aplican 2 niveles de aprobación y duración máxima de 4 horas
```

---

## US-033 — Aprobar o rechazar solicitudes

| Campo | Valor |
|---|---|
| Actor | Seguridad (titular o suplente), miembros del grupo (par), Propietario Funcional, Propietario Técnico, Custodio |
| Prioridad | Must · F1 |
| Requisitos | RF-APR-03, RF-APR-04, RF-APR-05, RF-APR-06, RF-APR-07, RF-CUM-02 |
| Reglas | RN-051, RN-052, RN-053, RN-054, RN-055, RN-104, RN-105, RN-106, RN-122 |
| Glosario | Approval, Access Request, Four Eyes Principle, Security Group, Role |

**Descripción:** Como aprobador, quiero revisar y aprobar o rechazar las solicitudes que me corresponden, para autorizar solo accesos justificados, garantizando que siempre intervengan dos personas y que nadie se autoapruebe.

**Criterios de aceptación:**
1. Bandeja con las solicitudes pendientes en las que soy aprobador elegible, con el detalle completo: solicitante, objetos, acción, justificación, duración e historial del objeto.
2. Decisión: aprobar, o rechazar con comentario obligatorio (RN-051).
3. **Objetos de criticidad Crítico:** aprueba obligatoriamente un usuario con rol Seguridad designado como aprobador titular o suplente, distinto del solicitante. Nunca aprueba un par del grupo (RN-122).
4. Si ningún titular resuelve en el plazo configurado (4 h por defecto; 30 min fuera de horario, a través de la guardia), la solicitud pasa a los suplentes de Seguridad (RN-122).
5. **Objetos Restringidos no críticos:** aprueba un único par, miembro activo del mismo grupo, distinto del solicitante y del beneficiario (RN-106).
6. **Otros objetos:** niveles secuenciales según la política. Un rechazo en cualquier nivel termina el flujo (RN-053).
7. Nadie puede aprobar una solicitud propia o de la que es beneficiario (RN-054).
8. Delegación temporal de aprobación: en objetos Críticos, solo a otro usuario con rol Seguridad; en la aprobación por par, solo a otro miembro activo del mismo grupo (RN-055).
9. Aprobar el último nivel genera el Temporary Access y notifica al solicitante y a los miembros del grupo (RN-105).
10. Para aprobar desde una notificación hay que autenticarse con MFA en la plataforma. No se aprueba con un simple enlace.

```gherkin
# language: es
Característica: Aprobación de solicitudes
  Como aprobador
  Quiero aprobar o rechazar solicitudes de acceso
  Para autorizar solo accesos justificados con la intervención de dos personas

  Antecedentes:
    Dado que el objeto "OBJ-000060" tiene criticidad "Crítico" y sensibilidad "Restringida"
    Y "OBJ-000060" está asignado al grupo "GRP-SWIFT" con los miembros activos "op1@banco.com", "op2@banco.com" y "op3@banco.com"
    Y "seg1@banco.com" es aprobador titular y "seg2@banco.com" aprobador suplente, ambos con rol "Seguridad"
    Y "op1@banco.com" envió la solicitud "SOL-0100" de tipo "Revelar" sobre "OBJ-000060"

  Escenario: Seguridad aprueba el acceso a un objeto crítico
    Cuando "seg1@banco.com" aprueba la solicitud "SOL-0100"
    Entonces la solicitud "SOL-0100" queda en estado "Aprobada"
    Y se crea un acceso temporal para "op1@banco.com" sobre "OBJ-000060"
    Y "op2@banco.com" y "op3@banco.com" reciben una notificación de la aprobación
    Y se registra un evento de auditoría "REQUEST_APPROVED"

  Escenario: Un par del grupo no puede aprobar un objeto crítico
    Cuando "op2@banco.com" intenta aprobar la solicitud "SOL-0100"
    Entonces el sistema rechaza la aprobación indicando que los objetos críticos los aprueba Seguridad

  Escenario: La solicitud pasa al suplente de Seguridad
    Dado que "seg1@banco.com" no resolvió la solicitud "SOL-0100" en 4 horas hábiles
    Entonces "seg2@banco.com" recibe la solicitud "SOL-0100" para su aprobación
    Cuando "seg2@banco.com" aprueba la solicitud "SOL-0100"
    Entonces la solicitud "SOL-0100" queda en estado "Aprobada"

  Escenario: Un par aprueba un objeto restringido no crítico
    Dado que el objeto "OBJ-000160" tiene criticidad "Alto", sensibilidad "Restringida" y está asignado al grupo "GRP-SWIFT"
    Y "op1@banco.com" envió la solicitud "SOL-0110" de tipo "Revelar" sobre "OBJ-000160"
    Cuando "op2@banco.com" aprueba la solicitud "SOL-0110"
    Entonces la solicitud "SOL-0110" queda en estado "Aprobada"

  Escenario: Prohibición de autoaprobación
    Cuando "op1@banco.com" intenta aprobar la solicitud "SOL-0100"
    Entonces el sistema rechaza la aprobación indicando que no puede aprobar su propia solicitud
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Rechazo con comentario
    Cuando "seg1@banco.com" rechaza la solicitud "SOL-0100" con el comentario "No existe cambio aprobado"
    Entonces la solicitud "SOL-0100" queda en estado "Rechazada"
    Y "op1@banco.com" recibe una notificación con el motivo del rechazo

  Escenario: Rechazo sin comentario
    Cuando "seg1@banco.com" intenta rechazar la solicitud "SOL-0100" sin comentario
    Entonces el sistema no registra la decisión

  Escenario: Objeto crítico bloqueado en un grupo que quedó con un solo miembro
    Dado que el objeto crítico "OBJ-000061" está asignado únicamente al grupo "GRP-TESORERIA"
    Y "tes1@banco.com" es el único miembro activo de "GRP-TESORERIA"
    Cuando "tes1@banco.com" intenta enviar una solicitud de tipo "Revelar" sobre "OBJ-000061"
    Entonces el sistema no permite enviar la solicitud porque el objeto crítico está bloqueado mientras el grupo tenga un solo miembro activo
    Y se genera una alerta al responsable del grupo "GRP-TESORERIA"

  Escenario: Aprobación multinivel en objetos no críticos
    Dado que el objeto "OBJ-000150" tiene criticidad "Alto" y sensibilidad "Confidencial"
    Y la política aplicable requiere nivel 1 "Propietario Funcional" y nivel 2 "Custodio del área"
    Y "op1@banco.com" envió la solicitud "SOL-0300" de tipo "Revelar" sobre "OBJ-000150"
    Cuando el Propietario Funcional de "OBJ-000150" aprueba el nivel 1 de "SOL-0300"
    Y el Custodio del área aprueba el nivel 2 de "SOL-0300"
    Entonces la solicitud "SOL-0300" queda en estado "Aprobada"

  Escenario: Delegación de aprobación de objetos críticos fuera de Seguridad no permitida
    Cuando "seg1@banco.com" intenta delegar sus aprobaciones en "op2@banco.com", que no tiene rol "Seguridad"
    Entonces el sistema rechaza la delegación indicando que el delegado debe tener rol "Seguridad"
```

---

# E09 — Acceso Temporal

## US-034 — Usar acceso temporal y Just-In-Time

| Campo | Valor |
|---|---|
| Actor | Beneficiario del acceso (Operador, Custodio, Propietario) |
| Prioridad | Must · F1 |
| Requisitos | RF-TMP-01, RF-TMP-02 |
| Reglas | RN-056, RN-059, RN-060 |
| Glosario | Temporary Access |

**Descripción:** Como Operador, quiero activar mi acceso aprobado justo cuando lo necesito (Just-In-Time), para minimizar el tiempo de exposición de la información sensible.

**Criterios de aceptación:**
1. Acceso programado: se activa automáticamente al llegar la hora de inicio aprobada.
2. Acceso JIT: queda Disponible y el beneficiario lo activa dentro de la ventana de activación (24 h por defecto). La duración corre desde la activación (RN-056).
3. Vista «Mis accesos» con estado, inicio, fin y tiempo restante.
4. No hay extensiones: se requiere una nueva solicitud (RN-059).
5. Si la ventana de activación vence sin uso, el acceso pasa a Expirado.

```gherkin
# language: es
Característica: Acceso temporal y Just-In-Time
  Como Operador
  Quiero activar mi acceso aprobado justo cuando lo necesito
  Para minimizar el tiempo de exposición

  Antecedentes:
    Dado que estoy autenticado como "op1@banco.com"
    Y tengo un acceso JIT aprobado sobre "OBJ-000300" con duración de 2 horas y ventana de activación de 24 horas

  Escenario: Activación JIT dentro de la ventana
    Cuando activo el acceso a las "10:00"
    Entonces el acceso queda en estado "Activo" con fin a las "12:00"
    Y se registra un evento de auditoría "ACCESS_ACTIVATED"

  Escenario: Ventana de activación vencida
    Dado que transcurrieron 25 horas desde la aprobación sin activar el acceso
    Cuando intento activar el acceso
    Entonces el sistema indica que el acceso expiró
    Y el acceso queda en estado "Expirado"

  Escenario: No se permiten extensiones
    Dado que mi acceso está activo y termina en 10 minutos
    Cuando intento extender el acceso
    Entonces el sistema me indica que debo crear una nueva solicitud
```

---

## US-035 — Revocar accesos vencidos automáticamente y de forma manual

| Campo | Valor |
|---|---|
| Actor | Sistema, Propietario, Seguridad, Beneficiario |
| Prioridad | Must · F1 |
| Requisitos | RF-TMP-03 |
| Reglas | RN-009, RN-031, RN-057, RN-058 |
| Glosario | Temporary Access |

**Descripción:** Como Seguridad de la Información, quiero que los accesos temporales se revoquen automáticamente al vencer y poder revocarlos de forma manual antes de tiempo, para garantizar que ningún acceso sobreviva a su autorización.

**Criterios de aceptación:**
1. Revocación automática en `EndAt` (desfase máximo de 60 s) (RN-057).
2. Al revocar, se ocultan los valores revelados en pantalla y se invalidan los enlaces de descarga.
3. Revocación manual por el Propietario, Seguridad o el beneficiario, con motivo (RN-058).
4. Revocación automática por desactivación del objeto (RN-009), por deshabilitación del usuario (RN-031) o por retiro del usuario del grupo que le daba acceso (RN-035).
5. Cada revocación se audita y se notifica al beneficiario.

```gherkin
# language: es
Característica: Revocación de accesos temporales
  Como Seguridad de la Información
  Quiero que los accesos vencidos se revoquen automáticamente
  Para que ningún acceso sobreviva a su autorización

  Escenario: Revocación automática al vencer
    Dado que "op1@banco.com" tiene un acceso activo sobre "OBJ-000300" que termina a las "12:00:00"
    Cuando el reloj del sistema marca las "12:01:00"
    Entonces el acceso está en estado "Expirado"
    Y "op1@banco.com" no puede revelar el valor de "OBJ-000300"
    Y se registra un evento de auditoría "ACCESS_EXPIRED"

  Escenario: Revocación manual por Seguridad
    Dado que "op1@banco.com" tiene un acceso activo sobre "OBJ-000300"
    Cuando "seg1@banco.com" revoca el acceso con el motivo "Actividad sospechosa"
    Entonces el acceso queda en estado "Revocado"
    Y "op1@banco.com" recibe una notificación de la revocación

  Escenario: Revocación por deshabilitación del usuario
    Dado que "op1@banco.com" tiene un acceso activo sobre "OBJ-000300"
    Cuando "op1@banco.com" es deshabilitado en Entra ID y se sincronizan los usuarios
    Entonces el acceso queda en estado "Revocado"

  Escenario: Revocación por retiro del grupo
    Dado que "op1@banco.com" tiene un acceso activo sobre "OBJ-000300" obtenido como miembro del grupo "GRP-OPS-PAGOS"
    Cuando el responsable retira a "op1@banco.com" del grupo "GRP-OPS-PAGOS"
    Entonces el acceso queda en estado "Revocado"
```

---

# E10 — Auditoría

## US-036 — Registrar eventos de auditoría

| Campo | Valor |
|---|---|
| Actor | Sistema |
| Prioridad | Must · F1 |
| Requisitos | RF-AUD-01, RF-AUD-02, RF-CUM-07 |
| Reglas | RN-075, RN-077, RN-079, RN-085 |
| Glosario | Audit Event |

**Descripción:** Como Auditor, quiero que el sistema registre todas las acciones relevantes con su contexto completo, para contar con trazabilidad total de lo ocurrido en la plataforma.

**Criterios de aceptación:**
1. Se auditan como mínimo: inicios de sesión (exitosos y fallidos), consultas de detalle, búsquedas, revelados, descargas, creaciones, modificaciones, cambios de estado, eliminaciones, solicitudes, aprobaciones, rechazos, activaciones y revocaciones de acceso, cambios de configuración, de políticas, de roles y de grupos, exportaciones, alertas, escalamientos, intentos denegados y recuperaciones por API.
2. Cada evento contiene los atributos de §2.19 del glosario, con hora UTC sincronizada (NTP).
3. Los estados antes y después se enmascaran (RN-077). Ningún evento contiene Sensitive Payloads (RN-085).
4. Operaciones sensibles en modo fail-closed (RN-079).

```gherkin
# language: es
Característica: Registro de eventos de auditoría
  Como Auditor
  Quiero que toda acción relevante quede registrada
  Para contar con trazabilidad completa

  Esquema del escenario: Acciones auditadas
    Dado que un usuario autenticado ejecuta la acción "<accion>" sobre el objeto "OBJ-000120"
    Cuando la acción finaliza
    Entonces existe un evento de auditoría de tipo "<evento>" con usuario, fecha, hora UTC, IP de origen y resultado

    Ejemplos:
      | accion                | evento             |
      | Consultar detalle     | OBJECT_VIEWED      |
      | Revelar valor         | SECRET_REVEALED    |
      | Descargar certificado | OBJECT_DOWNLOADED  |
      | Modificar metadatos   | OBJECT_UPDATED     |
      | Eliminar objeto       | OBJECT_DELETED     |
      | Aprobar solicitud     | REQUEST_APPROVED   |
      | Exportar reporte      | REPORT_EXPORTED    |

  Escenario: El valor secreto nunca se registra en auditoría
    Cuando un Custodio actualiza el valor secreto de "OBJ-000120"
    Entonces el evento de auditoría "OBJECT_UPDATED" indica "[PROTEGIDO]" en el campo de valor
    Y ni el valor anterior ni el nuevo aparecen en el evento

  Escenario: Intentos denegados quedan registrados
    Cuando un usuario sin permisos intenta descargar "OBJ-000120"
    Entonces se registra un evento de auditoría con resultado "Denied"
```

---

## US-037 — Garantizar la inmutabilidad de la bitácora

| Campo | Valor |
|---|---|
| Actor | Sistema, Auditor, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-AUD-03, RF-AUD-04 |
| Reglas | RN-075, RN-076, RN-078, RN-109 |
| Glosario | Audit Event |
| RNF | RNF-AUD-01, RNF-AUD-02, RNF-AUD-03 |

**Descripción:** Como Auditor, quiero que la bitácora sea inmutable y que su integridad se pueda verificar, para confiar en que ningún usuario, incluidos los administradores, la ha alterado.

**Criterios de aceptación:**
1. No existe funcionalidad ni endpoint para modificar o eliminar eventos (RN-075).
2. La cuenta de base de datos de la aplicación solo tiene permiso INSERT/SELECT sobre las tablas de auditoría, y se aplican controles adicionales: tabla *append-only* de SQL Server Ledger o protección equivalente (RNF-AUD-02).
3. Encadenamiento hash SHA-256 y verificación automática diaria y bajo demanda (RN-076).
4. Copia a almacenamiento inmutable (WORM) (RNF-AUD-02).
5. Cualquier ruptura de la cadena genera una alerta Crítica a Seguridad y al Auditor.
6. Retención según RN-078, sin purga anticipada.
7. La eliminación definitiva de objetos (US-055) nunca elimina eventos de auditoría (RN-109).

```gherkin
# language: es
Característica: Inmutabilidad de la bitácora de auditoría
  Como Auditor
  Quiero verificar que la bitácora no ha sido alterada
  Para confiar en la evidencia de auditoría

  Escenario: Ningún rol puede modificar eventos
    Dado que estoy autenticado con rol "Administrador"
    Cuando intento modificar o eliminar un evento de auditoría mediante la API
    Entonces el sistema responde que la operación no existe o no está permitida
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Verificación de integridad exitosa
    Dado que estoy autenticado con rol "Auditor"
    Cuando ejecuto la verificación de integridad para el rango "2026-09-01" a "2026-09-24"
    Entonces el resultado indica "Cadena íntegra" con el número de eventos verificados

  Escenario: Detección de alteración
    Dado que un evento del "2026-09-10" fue alterado directamente en la base de datos
    Cuando se ejecuta la verificación de integridad
    Entonces el resultado indica "Cadena rota" y el número de secuencia del primer evento inconsistente
    Y se genera una alerta de severidad "Crítica" a Seguridad y a Auditoría
```

---

## US-038 — Consultar y exportar la auditoría

| Campo | Valor |
|---|---|
| Actor | Auditor, Seguridad, Custodio y Propietario (sus objetos) |
| Prioridad | Must · F1 |
| Requisitos | RF-AUD-01, RF-REP-04, RF-CUM-03 |
| Reglas | RN-010, RN-085, RN-101 |
| Glosario | Audit Event, Evidence Package |

**Descripción:** Como Auditor, quiero consultar, filtrar y exportar eventos de auditoría, para investigar incidentes y responder requerimientos de auditoría.

**Criterios de aceptación:**
1. Filtros por rango de fechas, usuario, aplicación, objeto, tipo de evento, resultado, IP y correlación.
2. La vista de línea de tiempo de un objeto muestra todos sus eventos.
3. Custodio y Propietario ven solo la auditoría de los objetos de su ámbito. Auditor y Seguridad, toda. El Administrador no ve la auditoría de negocio (glosario §4.2, nota ¹).
4. Exportación a CSV, Excel o PDF con marca de agua (RN-101).
5. Las consultas y exportaciones de auditoría también se auditan.

```gherkin
# language: es
Característica: Consulta y exportación de auditoría
  Como Auditor
  Quiero consultar y exportar la auditoría
  Para investigar incidentes y responder requerimientos

  Escenario: Línea de tiempo de un objeto
    Dado que estoy autenticado con rol "Auditor"
    Cuando consulto la línea de tiempo del objeto "OBJ-000060"
    Entonces veo en orden cronológico todos los eventos de creación, modificaciones, solicitudes, aprobaciones, revelados y alertas

  Escenario: Exportación de auditoría filtrada
    Dado que estoy autenticado con rol "Auditor"
    Cuando filtro eventos de tipo "SECRET_REVEALED" del mes "2026-09"
    Y exporto el resultado a "CSV"
    Entonces obtengo un archivo con los eventos filtrados y una marca de usuario y fecha de generación
    Y se registra un evento de auditoría "AUDIT_EXPORTED"

  Escenario: Administrador no accede a la auditoría de negocio
    Dado que estoy autenticado con rol "Administrador"
    Cuando intento consultar la auditoría del objeto "OBJ-000060"
    Entonces el sistema deniega el acceso
```

---

# E11 — Trazabilidad de Uso

## US-039 — Registrar el uso de cada objeto

| Campo | Valor |
|---|---|
| Actor | Sistema, Aplicaciones consumidoras |
| Prioridad | Must · F1 |
| Requisitos | RF-USO-01, RF-USO-02, RF-INT-01 |
| Reglas | RN-060, RN-072, RN-074, RN-091 |
| Glosario | Usage Event, Application |

**Descripción:** Como Propietario Técnico, quiero saber qué usuarios y aplicaciones usan cada objeto, cuándo y para qué acción, para conocer sus dependencias y detectar usos anómalos.

**Criterios de aceptación:**
1. Se registra un Usage Event cuando: un usuario revela o descarga; una aplicación recupera el objeto por API *(F2, DEC-03)*; una aplicación notifica su uso mediante el endpoint de notificación de uso.
2. Atributos: usuario o aplicación, fecha, hora UTC, acción realizada, canal, IP y resultado (RN-072).
3. El detalle del objeto muestra el último uso y el historial de usos.
4. Sin valores sensibles (RN-074).
5. *F2:* registro de uso ampliado cuando las aplicaciones recuperen valores directamente por API (ver C-09).

```gherkin
# language: es
Característica: Registro de uso de objetos
  Como Propietario Técnico
  Quiero conocer quién usa cada objeto y cuándo
  Para conocer dependencias y detectar usos anómalos

  @F2
  Escenario: Uso por aplicación mediante API
    Dado que la aplicación "Pagos" con identidad "sp-pagos-prod" está autorizada sobre "OBJ-000120"
    Cuando la aplicación recupera "OBJ-000120" mediante la API
    Entonces se registra un evento de uso con actor "Pagos", acción "Retrieve", canal "API", fecha y hora UTC
    Y el campo último uso de "OBJ-000120" se actualiza

  @F2
  Escenario: Aplicación no autorizada
    Dado que la aplicación "Reportes" no está asignada a "OBJ-000120"
    Cuando la aplicación intenta recuperar "OBJ-000120" mediante la API
    Entonces la API responde acceso denegado
    Y se registra un evento de uso con resultado "Denied"

  Escenario: Uso por usuario
    Dado que "op1@banco.com" revela el valor de "OBJ-000300" con un acceso temporal activo
    Entonces se registra un evento de uso con actor "op1@banco.com", acción "Reveal" y canal "UI"
```

---

## US-040 — Identificar objetos sin uso

| Campo | Valor |
|---|---|
| Actor | Sistema, Custodio, Propietarios |
| Prioridad | Should · F1 |
| Requisitos | RF-USO-03 |
| Reglas | RN-073 |
| Glosario | Usage Event, Alert |

**Descripción:** Como Custodio, quiero identificar los objetos que no se han usado en un período configurable, para evaluar su desactivación y reducir la superficie de riesgo.

**Criterios de aceptación:**
1. Período configurable (90 días por defecto) (RN-073).
2. Los objetos activos sin Usage Events en el período se marcan «Sin uso» y generan una alerta de baja severidad a los propietarios.
3. Filtro «Sin uso» en la búsqueda y en el dashboard.
4. Los objetos registrados hace menos tiempo que el período no se marcan.

```gherkin
# language: es
Característica: Identificación de objetos sin uso
  Como Custodio
  Quiero identificar objetos sin uso
  Para evaluar su desactivación y reducir riesgo

  Escenario: Objeto sin uso en el período
    Dado que el período de inactividad configurado es de 90 días
    Y el objeto activo "OBJ-000555" fue registrado hace 200 días
    Y su último evento de uso fue hace 120 días
    Cuando se ejecuta el análisis de uso
    Entonces "OBJ-000555" queda marcado como "Sin uso"
    Y sus propietarios reciben una alerta de severidad "Baja"

  Escenario: Objeto recién registrado no se marca
    Dado que el objeto activo "OBJ-000556" fue registrado hace 30 días y no tiene eventos de uso
    Cuando se ejecuta el análisis de uso
    Entonces "OBJ-000556" no queda marcado como "Sin uso"
```

---

# E12 — Dashboard y Monitoreo

## US-041 — Consultar el dashboard operativo

| Campo | Valor |
|---|---|
| Actor | Custodio, Operador, Propietarios, Seguridad |
| Prioridad | Must · F1 |
| Requisitos | RF-DSH-01, RF-DSH-03 |
| Reglas | RN-010, RN-094 |
| Glosario | Managed Object, Alert, Temporary Access |

**Descripción:** Como Custodio, quiero un dashboard operativo con el estado del inventario de mi ámbito, para priorizar las acciones del día.

**Criterios de aceptación:**
1. Indicadores: objetos registrados; objetos por tipo; por criticidad; próximos a vencer (≤ 30, ≤ 90, ≤ 180 días); expirados; alertas abiertas por severidad; accesos temporales activos; solicitudes pendientes; objetos huérfanos; objetos con configuración insegura; objetos sin uso.
2. Todos los indicadores respetan el ámbito del usuario (RN-010).
3. Cada indicador permite navegar a la lista filtrada correspondiente.
4. Datos actualizados con un retraso máximo de 5 min.

```gherkin
# language: es
Característica: Dashboard operativo
  Como Custodio
  Quiero ver el estado operativo de mi inventario
  Para priorizar las acciones del día

  Escenario: Indicadores del dashboard operativo
    Dado que estoy autenticado con rol "Custodio" en el área "Infraestructura"
    Cuando abro el dashboard operativo
    Entonces veo los indicadores de objetos registrados, por tipo, por criticidad, próximos a vencer, expirados, accesos activos y alertas abiertas
    Y todos los valores corresponden únicamente a objetos de mi ámbito

  Escenario: Navegación desde un indicador
    Dado que el indicador "Expirados" muestra 3 objetos
    Cuando selecciono el indicador "Expirados"
    Entonces veo la lista de los 3 objetos expirados de mi ámbito
```

---

## US-042 — Consultar el dashboard ejecutivo de riesgo y cumplimiento

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información, Dirección de Tecnología, Auditor |
| Prioridad | Should · F1 |
| Requisitos | RF-DSH-02, RF-DSH-03, RF-CUM-03 |
| Reglas | RN-102 |
| Glosario | Compliance Control, Managed Object |

**Descripción:** Como CISO, quiero un dashboard ejecutivo con indicadores agregados de riesgo, accesos y cumplimiento y su tendencia, para supervisar la postura de seguridad y reportar a la dirección.

**Criterios de aceptación:**
1. Indicadores: total de objetos y distribución por tipo y criticidad; % de objetos con propietario válido; % vencidos; objetos críticos próximos a vencer; accesos a información sensible en el período (aprobados, rechazados, revocados); violaciones de SoD; resultado de la integridad de la auditoría; índice de riesgo; % de cumplimiento por control (RN-102).
2. Tendencia de los últimos 12 meses.
3. Filtros por área, ambiente y período.
4. Exportable a PDF.

**Índice de riesgo (definición funcional):** suma ponderada por objeto de: criticidad (Crítico 4, Alto 3, Medio 2, Bajo 1) × factor de condición (expirado 3, ≤ 30 días 2, configuración insegura 2, huérfano 2, sin uso 1), normalizada de 0 a 100. Los pesos son configurables por Seguridad.

```gherkin
# language: es
Característica: Dashboard ejecutivo
  Como CISO
  Quiero indicadores agregados de riesgo y cumplimiento
  Para supervisar la postura de seguridad y reportar a la dirección

  Escenario: Indicadores ejecutivos
    Dado que estoy autenticado con rol "Seguridad"
    Cuando abro el dashboard ejecutivo
    Entonces veo el porcentaje de objetos con propietario válido
    Y veo el índice de riesgo actual y su tendencia de 12 meses
    Y veo el porcentaje de cumplimiento por control
    Y veo el número de accesos a información sensible aprobados, rechazados y revocados en el período

  Escenario: Exportación del dashboard ejecutivo
    Dado que estoy autenticado con rol "Seguridad"
    Cuando exporto el dashboard ejecutivo a "PDF"
    Entonces obtengo un documento PDF con los indicadores y la fecha de corte
```

---

# E13 — Reportería

## US-043 — Generar reportes operativos y ejecutivos

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietarios, Seguridad, Auditor |
| Prioridad | Must · F1 (operativos) · Should · F1 (ejecutivos) |
| Requisitos | RF-REP-01, RF-REP-02 |
| Reglas | RN-010, RN-085, RN-101 |
| Glosario | Managed Object, Alert, Access Request, Audit Event |

**Descripción:** Como Custodio, quiero generar reportes operativos y ejecutivos predefinidos, para dar seguimiento al inventario, los vencimientos y los accesos.

**Criterios de aceptación:**
1. **Reportes operativos:** inventario completo; objetos por propietario; vencimientos por rango de fechas; objetos expirados; alertas y escalamientos; solicitudes y aprobaciones; accesos temporales activos e históricos; objetos sin uso; objetos huérfanos; objetos con configuración insegura; actividad de auditoría por usuario u objeto.
2. **Reportes ejecutivos:** resumen de riesgo; cumplimiento por área; tendencia de vencimientos e incidentes evitados; resumen de accesos privilegiados.
3. Parámetros: rango de fechas, área, aplicación, tipo, criticidad y ambiente.
4. Respetan el ámbito del usuario (RN-010) y nunca incluyen payloads (RN-085).
5. Programación opcional con envío por correo a destinatarios autorizados.

```gherkin
# language: es
Característica: Reportes operativos y ejecutivos
  Como Custodio
  Quiero generar reportes predefinidos
  Para dar seguimiento al inventario, vencimientos y accesos

  Escenario: Reporte de vencimientos por rango
    Dado que estoy autenticado con rol "Custodio"
    Cuando genero el reporte "Vencimientos" para el rango "2026-10-01" a "2026-12-31"
    Entonces obtengo los objetos de mi ámbito que vencen en el rango con propietario, criticidad y días restantes

  Escenario: Reporte programado
    Dado que estoy autenticado con rol "Seguridad"
    Cuando programo el reporte "Resumen de riesgo" con envío mensual a "ciso@banco.com"
    Entonces el reporte se genera el primer día de cada mes y se envía al destinatario
    Y cada envío queda registrado en auditoría
```

---

## US-044 — Generar reportes regulatorios y paquetes de evidencia

| Campo | Valor |
|---|---|
| Actor | Auditor, Seguridad, Cumplimiento |
| Prioridad | Should · F1 |
| Requisitos | RF-REP-03, RF-CUM-03, RF-CUM-04 |
| Reglas | RN-100, RN-101, RN-102 |
| Glosario | Evidence Package, Compliance Control, Audit Event |

**Descripción:** Como Auditor, quiero generar reportes regulatorios y paquetes de evidencia con integridad verificable, para responder auditorías y requerimientos regulatorios con rapidez.

**Criterios de aceptación:**
1. Reportes regulatorios predefinidos, alineados con los controles de [04-non-functional-requirements.md §6](04-non-functional-requirements.md#6-cumplimiento): inventario de activos criptográficos y credenciales (DORA art. 8, PCI DSS 12.3.3); revisión de accesos y privilegios (ISO 27001 A.5.18, PCI DSS 7.2.4); gestión de claves (PCI DSS 3.6/3.7); cuentas de sistema y aplicación (PCI DSS 8.6); registros de auditoría (PCI DSS 10, ISO 27001 A.8.15); SoD (ISO 27001 A.5.3); certificados SWIFT (SWIFT CSP).
2. Paquete de evidencia: ZIP con reportes, extracto de auditoría, resultado de la verificación de integridad, manifiesto con hashes SHA-256 y firma de la plataforma (RN-100).
3. Verificación posterior del paquete mediante su manifiesto.
4. Sin payloads (RN-100).

```gherkin
# language: es
Característica: Reportes regulatorios y evidencias
  Como Auditor
  Quiero generar paquetes de evidencia verificables
  Para responder auditorías con rapidez

  Escenario: Generación de paquete de evidencia
    Dado que estoy autenticado con rol "Auditor"
    Cuando genero un paquete de evidencia del control "Revisión de accesos" para el período "2026-01-01" a "2026-06-30"
    Entonces obtengo un archivo ZIP con los reportes, el extracto de auditoría y un manifiesto firmado
    Y el manifiesto contiene la huella SHA-256 de cada archivo
    Y ningún archivo contiene valores secretos

  Escenario: Verificación de paquete alterado
    Dado que un archivo del paquete de evidencia fue modificado después de su generación
    Cuando verifico el paquete contra su manifiesto
    Entonces el resultado indica que el paquete no es íntegro e identifica el archivo alterado
```

---

## US-045 — Exportar a PDF, Excel y CSV sin exponer secretos

| Campo | Valor |
|---|---|
| Actor | Usuarios con permiso Exportar |
| Prioridad | Must · F1 |
| Requisitos | RF-REP-04, RF-CUM-06 |
| Reglas | RN-026, RN-085, RN-101 |
| Glosario | Evidence Package, Permission |

**Descripción:** Como usuario con permiso de exportación, quiero exportar reportes, listados y auditoría a PDF, Excel y CSV, para compartir y analizar la información fuera de la plataforma sin exponer secretos.

**Criterios de aceptación:**
1. Formatos: PDF, Excel (.xlsx) y CSV (UTF-8).
2. Requiere permiso Exportar. Respeta el ámbito del usuario (RN-101).
3. Nunca incluye Sensitive Payloads ni material de clave (RN-026, RN-085).
4. En CSV y Excel se neutralizan las fórmulas (prevención de *CSV injection*: los valores que comienzan con `=`, `+`, `-` o `@` se escapan).
5. Marca de agua o metadatos con usuario y fecha. La exportación se audita.
6. Límite configurable de filas por exportación síncrona. Los volúmenes mayores se procesan de forma asíncrona con notificación.

```gherkin
# language: es
Característica: Exportación segura de información
  Como usuario con permiso de exportación
  Quiero exportar información a PDF, Excel y CSV
  Para compartirla y analizarla sin exponer secretos

  Esquema del escenario: Exportación en múltiples formatos
    Dado que estoy autenticado con rol "Custodio" con permiso "Exportar"
    Cuando exporto el reporte "Inventario completo" en formato "<formato>"
    Entonces obtengo un archivo "<extension>" con los objetos de mi ámbito
    Y el archivo no contiene valores secretos ni material de claves
    Y se registra un evento de auditoría "REPORT_EXPORTED"

    Ejemplos:
      | formato | extension |
      | PDF     | .pdf      |
      | Excel   | .xlsx     |
      | CSV     | .csv      |

  Escenario: Usuario sin permiso de exportación
    Dado que estoy autenticado con rol "Operador"
    Cuando intento exportar el reporte "Inventario completo"
    Entonces el sistema deniega la exportación

  Escenario: Prevención de inyección de fórmulas
    Dado que el objeto "OBJ-000999" tiene la descripción "=HYPERLINK('http://x')"
    Cuando exporto el inventario a "CSV"
    Entonces la descripción de "OBJ-000999" se exporta escapada como texto
```

---

# E14 — Integraciones

## US-046 — Exponer una API REST documentada con OpenAPI

| Campo | Valor |
|---|---|
| Actor | Aplicaciones consumidoras, Front-End, sistemas externos |
| Prioridad | Must · F1 |
| Requisitos | RF-INT-01, RF-USO-01, RF-PRT-06 |
| Reglas | RN-040, RN-086, RN-091, RN-113 |
| Glosario | Application, Permission, Usage Event |
| RNF | RNF-SEG-02, RNF-SEG-08, RNF-MAN-03 |

**Descripción:** Como Administrador de Aplicaciones, quiero una API REST segura y documentada, para integrar las aplicaciones y el Front-End con la plataforma sin acceder directamente a la base de datos.

**Criterios de aceptación:**
1. Todas las funcionalidades del Front-End se consumen vía API. El Front-End no accede a la BD (R-07).
2. Especificación OpenAPI 3.x publicada y versionada (`/api/v1/...`).
3. Autenticación OAuth 2.0 con Entra ID: usuarios (flujo de código de autorización con PKCE) y aplicaciones (credenciales de cliente con certificado; en on-premise no hay Managed Identity salvo con Azure Arc).
4. Autorización por permisos y ámbito en cada endpoint (RN-040). Las aplicaciones solo recuperan objetos asignados (RN-091).
5. Limitación de tasa por cliente, validación de entradas y respuestas de error que no revelan información interna.
6. Los endpoints de recuperación de payload están separados, auditados y registran uso (RN-086). *La recuperación por aplicaciones es F2 (DEC-03).* Los objetos con llave dividida nunca se entregan completos por API (RN-113).
7. Endpoint de notificación de uso para que las aplicaciones informen del uso de objetos no recuperados vía API.

```gherkin
# language: es
Característica: API REST documentada
  Como Administrador de Aplicaciones
  Quiero una API REST segura y documentada
  Para integrar aplicaciones con la plataforma

  Escenario: Documentación OpenAPI disponible
    Cuando un desarrollador autenticado consulta la especificación OpenAPI de la versión 1
    Entonces obtiene un documento OpenAPI 3 válido con todos los endpoints y esquemas

  Escenario: Llamada sin token
    Cuando un cliente invoca un endpoint de la API sin token de acceso
    Entonces la API responde con código 401

  @F2
  Escenario: Aplicación autenticada con credenciales de cliente recupera un objeto asignado
    Dado que la aplicación "Pagos" obtiene un token de Entra ID con su certificado de cliente
    Y "OBJ-000120" está asignado a la aplicación "Pagos"
    Cuando invoca el endpoint de recuperación de "OBJ-000120"
    Entonces la API responde con código 200 y el valor del secreto
    Y se registra un evento de auditoría y un evento de uso

  Escenario: Limitación de tasa
    Dado que la aplicación "Pagos" superó el límite de solicitudes por minuto
    Cuando invoca nuevamente la API
    Entonces la API responde con código 429
```

---

## US-047 — Enviar notificaciones por correo corporativo y Microsoft Teams

| Campo | Valor |
|---|---|
| Actor | Sistema |
| Prioridad | Must · F1 (correo y Teams, DEC-07) |
| Requisitos | RF-INT-05, RF-INT-06, RF-VEN-03 |
| Reglas | RN-067, RN-099 |
| Glosario | Notification, Alert |

**Descripción:** Como Propietario, quiero recibir notificaciones de alertas, solicitudes, aprobaciones y revocaciones por correo corporativo y, si se habilita, por Microsoft Teams, para enterarme a tiempo por el canal que uso.

**Criterios de aceptación:**
1. Correo mediante el servicio corporativo (Microsoft Graph / SMTP autenticado con TLS).
2. Teams mediante Microsoft Graph (mensaje de chat o canal) o webhook de flujo (DEC-07). Requiere salida controlada desde el centro de datos hacia Microsoft 365.
3. Plantillas por tipo de evento, en español, sin payloads (RN-067).
4. Reintentos y registro de fallos (RN-099).
5. Las notificaciones de aprobación enlazan a la plataforma. No permiten aprobar sin autenticarse (US-033).
6. Las notificaciones de actividad de los grupos de acceso se especifican en US-053.

```gherkin
# language: es
Característica: Notificaciones por correo y Teams
  Como Propietario
  Quiero recibir notificaciones por los canales corporativos
  Para enterarme a tiempo de eventos que requieren mi atención

  Escenario: Notificación por correo
    Dado que se genera una alerta de vencimiento para "OBJ-000100"
    Cuando se procesa la notificación
    Entonces el Propietario Técnico recibe un correo con la plantilla "Alerta de vencimiento"
    Y el correo contiene un enlace al objeto en la plataforma

  Escenario: Notificación por Teams habilitada
    Dado que el canal Teams está habilitado
    Y el Propietario Técnico tiene preferencia de canal "Correo y Teams"
    Cuando se genera una alerta de vencimiento para "OBJ-000100"
    Entonces el Propietario Técnico recibe la alerta por correo y por Teams

  Escenario: Fallo definitivo de envío
    Dado que el servicio de correo no está disponible
    Cuando se agotan los 3 reintentos de envío de una notificación
    Entonces la notificación queda en estado "Fallida"
    Y se genera una alerta al Administrador
```

---

## US-048 — Enviar eventos de seguridad y auditoría al SIEM corporativo

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad (SOC) |
| Prioridad | Should · F2 (DEC-08) |
| Requisitos | RF-INT-07, RF-CUM-07 |
| Reglas | RN-076, RN-085 |
| Glosario | Audit Event |

**Descripción:** Como analista del SOC, quiero recibir en el SIEM los eventos de auditoría y seguridad de la plataforma, para correlacionarlos y detectar comportamientos anómalos.

**Criterios de aceptación:**
1. Envío casi en tiempo real (≤ 1 min) de todos los Audit Events en un formato estándar (CEF o JSON sobre syslog TLS, o conector nativo de Microsoft Sentinel / Azure Monitor).
2. Los eventos no contienen Sensitive Payloads (RN-085).
3. Cola persistente con reintentos. No se pierden eventos ante una indisponibilidad del SIEM.
4. Eventos de alta prioridad marcados: revelados de objetos Críticos, intentos denegados repetidos, violaciones de SoD, ruptura de integridad, cambios de políticas y roles.

```gherkin
# language: es
Característica: Integración con el SIEM corporativo
  Como analista del SOC
  Quiero recibir los eventos de la plataforma en el SIEM
  Para correlacionar y detectar comportamientos anómalos

  Escenario: Envío de evento de revelado
    Dado que la integración con el SIEM está habilitada
    Cuando un usuario revela el valor de "OBJ-000060"
    Entonces el SIEM recibe en menos de 1 minuto un evento "SECRET_REVEALED" con usuario, objeto, fecha e IP
    Pero el evento no contiene el valor revelado

  Escenario: Indisponibilidad temporal del SIEM
    Dado que el SIEM no está disponible durante 30 minutos
    Cuando se generan 500 eventos de auditoría en ese período
    Entonces los 500 eventos se entregan al SIEM cuando se restablece la conexión
```

---

# E15 — Descubrimiento Automático

## US-049 — Descubrir automáticamente objetos en la infraestructura

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información, Custodio, Sistema |
| Prioridad | Could · **F2** (fuera de alcance de la Fase 1, ver C-03) |
| Requisitos | RF-DES-01, RF-DES-02 |
| Reglas | RN-097, RN-098 |
| Glosario | Discovery Finding, Certificate, Secret, Credential, Service Account |

**Descripción:** Como Seguridad de la Información, quiero que la plataforma descubra automáticamente certificados, secretos, credenciales y cuentas de servicio en servidores, endpoints TLS, bóvedas y directorios, para identificar objetos no registrados.

**Criterios de aceptación (para la fase 2):**
1. Fuentes de descubrimiento configurables: escaneo de endpoints TLS por rango de IP/puertos, almacenes de certificados de servidores Windows/Linux (mediante agente o conector), Entra ID (Service Principals y sus credenciales), Active Directory (cuentas de servicio).
2. Ejecución programada y bajo demanda, con credenciales de descubrimiento de solo lectura custodiadas en la propia plataforma.
3. Correlación con el inventario (huella del certificado, identificador de cuenta).
4. Resultados como Discovery Findings de categoría «No registrado». No se registran automáticamente (RN-098).
5. El descubrimiento **no extrae valores de secretos o contraseñas**, solo metadatos.

```gherkin
# language: es
Característica: Descubrimiento automático de objetos
  Como Seguridad de la Información
  Quiero descubrir objetos en la infraestructura
  Para identificar objetos no registrados

  Escenario: Certificado TLS no registrado
    Dado que existe una fuente de descubrimiento TLS para el rango "10.10.0.0/24" puerto 443
    Y el servidor "10.10.0.15" presenta un certificado cuya huella no existe en el inventario
    Cuando se ejecuta el descubrimiento
    Entonces se crea un hallazgo de categoría "No registrado" con los metadatos del certificado
    Y el certificado no se registra automáticamente en el inventario

  Escenario: Certificado descubierto ya registrado
    Dado que el servidor "10.10.0.16" presenta el certificado del objeto "OBJ-000045"
    Cuando se ejecuta el descubrimiento
    Entonces se agrega "10.10.0.16" a las ubicaciones de instalación de "OBJ-000045"
    Y no se crea un hallazgo
```

---

## US-050 — Identificar y gestionar objetos huérfanos, expirados e inseguros

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información, Custodio, Propietarios |
| Prioridad | Must · F1 (sobre el inventario registrado) · F2 (categoría «No registrado») |
| Requisitos | RF-DES-02, RF-VEN-05, RF-PRO-04 |
| Reglas | RN-020, RN-029, RN-031, RN-097, RN-098 |
| Glosario | Discovery Finding, Alert |

**Descripción:** Como Seguridad de la Información, quiero una bandeja de hallazgos con los objetos huérfanos, expirados y con configuración insegura (y no registrados en F2), para gestionarlos hasta su resolución o aceptación de riesgo.

**Criterios de aceptación:**
1. Categorías: Huérfano, Expirado, Configuración insegura (F1) y No registrado (F2, requiere descubrimiento) (RN-097).
2. Por hallazgo: objeto, categoría, detalle, fecha de detección, responsable y estado (Abierto, En gestión, Resuelto, Riesgo aceptado, Descartado).
3. La aceptación de riesgo requiere justificación, fecha de revisión y aprobación de Seguridad con cuatro ojos (RN-098).
4. Los hallazgos se resuelven automáticamente cuando desaparece la condición.
5. Todo cambio de estado se audita.

```gherkin
# language: es
Característica: Gestión de hallazgos
  Como Seguridad de la Información
  Quiero gestionar hallazgos de objetos huérfanos, expirados e inseguros
  Para reducir el riesgo del inventario

  Escenario: Hallazgo por configuración insegura
    Dado que el certificado "OBJ-000077" usa clave RSA de 1024 bits
    Cuando se ejecuta el análisis de hallazgos
    Entonces existe un hallazgo abierto de categoría "Configuración insegura" para "OBJ-000077"

  Escenario: Aceptación de riesgo con cuatro ojos
    Dado que existe un hallazgo abierto para "OBJ-000077"
    Cuando el Custodio solicita aceptar el riesgo con la justificación "Dispositivo legado sin soporte a 2048 bits, reemplazo en Q1 2027" y fecha de revisión "2027-03-31"
    Y un usuario de Seguridad distinto del solicitante aprueba la aceptación
    Entonces el hallazgo queda en estado "Riesgo aceptado" hasta "2027-03-31"

  Escenario: Resolución automática
    Dado que existe un hallazgo de categoría "Expirado" para "OBJ-000003"
    Cuando el Propietario Técnico renueva "OBJ-000003"
    Entonces el hallazgo queda en estado "Resuelto"
```

---

# E16 — Cumplimiento y Seguridad

## US-051 — Impedir la exposición de secretos en logs, reportes y auditoría

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad (verificación) |
| Prioridad | Must · F1 |
| Requisitos | RF-CUM-05, RF-CUM-06, RF-CUM-07, RF-PRT-03 |
| Reglas | RN-067, RN-074, RN-077, RN-085, RN-086 |
| Glosario | Sensitive Payload, Audit Event, Usage Event |
| RNF | RNF-SEG-09 |

**Descripción:** Como Seguridad de la Información, quiero garantizar que ningún secreto aparezca en logs técnicos, reportes, exportaciones, notificaciones ni auditoría, para eliminar canales secundarios de fuga.

**Criterios de aceptación:**
1. Los Sensitive Payloads se modelan con un tipo protegido que no se serializa en logs (redacción automática en el framework de logging).
2. Los mensajes de error y las trazas de excepción no incluyen payloads ni cadenas de conexión.
3. Los reportes, exportaciones, notificaciones, eventos de auditoría, eventos de uso y eventos al SIEM no los contienen (RN-085).
4. Se incluyen pruebas automatizadas de «canario»: registrar un secreto con un valor marcador y verificar que no aparece en ningún log, reporte, exportación ni evento.
5. Análisis estático (SAST) y detección de secretos en el pipeline de CI.

```gherkin
# language: es
Característica: Prevención de exposición de secretos
  Como Seguridad de la Información
  Quiero que ningún secreto aparezca en canales secundarios
  Para eliminar vías de fuga de información

  Antecedentes:
    Dado que existe el secreto "OBJ-CANARIO" con el valor marcador "CANARY-7f3a9c"

  Esquema del escenario: El valor no aparece en canales secundarios
    Cuando se ejecutan las operaciones de registro, modificación, revelado, recuperación por API, exportación y notificación sobre "OBJ-CANARIO"
    Entonces el texto "CANARY-7f3a9c" no aparece en "<canal>"

    Ejemplos:
      | canal                          |
      | logs técnicos de la aplicación |
      | eventos de auditoría           |
      | eventos de uso                 |
      | eventos enviados al SIEM       |
      | reportes exportados            |
      | notificaciones enviadas        |
      | mensajes de error              |
```

---

## US-052 — Evaluar controles de cumplimiento

| Campo | Valor |
|---|---|
| Actor | Seguridad de la Información, Auditor, Cumplimiento |
| Prioridad | Should · F1 |
| Requisitos | RF-CUM-01, RF-CUM-02, RF-CUM-03, RF-CUM-04 |
| Reglas | RN-095, RN-096, RN-102 |
| Glosario | Compliance Control, Segregation of Duties, Four Eyes Principle |

**Descripción:** Como Seguridad de la Información, quiero que el sistema evalúe diariamente un conjunto de controles de cumplimiento, para conocer el nivel de cumplimiento y generar evidencia continua.

**Criterios de aceptación:**
1. Controles iniciales precargados:

| ID | Control | Fórmula | Umbral |
|---|---|---|---|
| CC-01 | Objetos con propietarios válidos | objetos con PF y PT activos / objetos activos | 100 % |
| CC-02 | Objetos no expirados | activos no expirados / activos | 100 % |
| CC-03 | Revelados con aprobación | revelados con Temporary Access aprobado / revelados | 100 % |
| CC-04 | Operaciones críticas con cuatro ojos | operaciones RN-096 aprobadas por una persona distinta del solicitante (par del grupo o Seguridad) / operaciones RN-096 | 100 % |
| CC-09 | Grupos con objetos críticos y al menos dos miembros activos | grupos conformes / grupos con objetos Críticos o Restringidos | 100 % |
| CC-10 | Revisión diaria de eventos de seguridad a tiempo | reportes diarios revisados en ≤ 1 día hábil / reportes generados | 100 % |
| CC-11 | Objetos con llave dividida con custodios aceptados en ambos lados | objetos conformes / objetos con llave dividida | 100 % |
| CC-05 | Usuarios sin conflicto de SoD | usuarios sin violación / usuarios con roles | 100 % |
| CC-06 | Integridad de auditoría | verificaciones íntegras / verificaciones | 100 % |
| CC-07 | Objetos sin configuración insegura no aceptada | objetos sin hallazgo inseguro abierto / activos | ≥ 98 % |
| CC-08 | Objetos con uso reciente | activos con uso en 90 días / activos | ≥ 90 % |

2. Cada control referencia la norma aplicable (RN-102).
3. Serie histórica diaria, visible en el dashboard ejecutivo (US-042) e incluida en las evidencias (US-044).
4. Un incumplimiento de un control con umbral del 100 % genera una alerta a Seguridad.

```gherkin
# language: es
Característica: Evaluación de controles de cumplimiento
  Como Seguridad de la Información
  Quiero evaluar diariamente los controles de cumplimiento
  Para conocer el nivel de cumplimiento y generar evidencia continua

  Escenario: Control de propietarios incumplido
    Dado que existen 1000 objetos activos
    Y 2 objetos activos tienen propietario inválido
    Cuando se ejecuta la evaluación diaria de controles
    Entonces el control "CC-01" registra un resultado de 99.8 por ciento
    Y se genera una alerta a Seguridad por incumplimiento del control "CC-01"

  Escenario: Histórico de cumplimiento
    Dado que la evaluación de controles se ejecutó diariamente durante 30 días
    Cuando consulto el histórico del control "CC-03"
    Entonces veo 30 resultados diarios con su fecha de cálculo
```

---

# E17 — Grupos de Acceso

## US-053 — Notificar a los miembros del grupo la actividad sobre sus objetos

| Campo | Valor |
|---|---|
| Actor | Sistema, miembros del grupo, Responsable del grupo |
| Prioridad | Must · F1 |
| Requisitos | RF-ACC-08, RF-INT-05, RF-INT-06 |
| Reglas | RN-067, RN-085, RN-099, RN-105 |
| Glosario | Security Group, Notification, Audit Event |

**Descripción:** Como miembro de un grupo de acceso, quiero enterarme cuando otro miembro hace algo sobre los objetos del grupo, para tener visibilidad compartida y detectar a tiempo cualquier uso no esperado.

**Criterios de aceptación:**
1. Cuando un miembro ejecuta una acción sobre un objeto del grupo, se notifica a los demás miembros. El autor no recibe su propia notificación (RN-105).
2. El Responsable configura por grupo los tipos de acción notificados. Por defecto: revelar, descargar, modificar, cambiar estado, eliminar, solicitar acceso y aprobar o rechazar. Las consultas no se notifican.
3. Canales: correo y Teams (DEC-07), según la preferencia de cada usuario.
4. Contenido: autor, acción, objeto (código y nombre), fecha y hora, justificación cuando aplica y enlace al objeto. Nunca incluye Sensitive Payloads (RN-067, RN-085).
5. Si la acción afecta a un objeto asignado a varios grupos, cada persona recibe una sola notificación.
6. Los envíos se registran y los fallos siguen RN-099.
7. Los cambios en la configuración de notificaciones del grupo se auditan.

```gherkin
# language: es
Característica: Notificaciones de actividad del grupo
  Como miembro de un grupo de acceso
  Quiero enterarme de las acciones de los demás miembros sobre los objetos del grupo
  Para tener visibilidad compartida y detectar usos no esperados

  Antecedentes:
    Dado que el grupo "GRP-CANALES" tiene como miembros a "maria@banco.com", "luis.tecnico@banco.com" y "pedro@banco.com"
    Y el objeto "OBJ-000045" está asignado al grupo "GRP-CANALES"

  Escenario: Notificación al resto del grupo
    Cuando "luis.tecnico@banco.com" revela el valor de "OBJ-000045" con un acceso temporal activo
    Entonces "maria@banco.com" y "pedro@banco.com" reciben una notificación con el autor, la acción, el objeto y la fecha
    Pero "luis.tecnico@banco.com" no recibe notificación de su propia acción
    Y la notificación no contiene el valor revelado

  Escenario: Las consultas no se notifican
    Cuando "pedro@banco.com" consulta el detalle de "OBJ-000045"
    Entonces no se envían notificaciones a los miembros del grupo

  Esquema del escenario: Tipos de acción configurados por el responsable
    Dado que la responsable "maria@banco.com" configuró la notificación de la acción "<accion>" como "<estado>"
    Cuando "pedro@banco.com" ejecuta la acción "<accion>" sobre "OBJ-000045"
    Entonces el resultado de la notificación al grupo es "<resultado>"

    Ejemplos:
      | accion    | estado      | resultado  |
      | Descargar | Activada    | Enviada    |
      | Modificar | Desactivada | No enviada |

  Escenario: Una sola notificación cuando el objeto está en varios grupos
    Dado que "OBJ-000045" también está asignado al grupo "GRP-PERIMETRO" del que "maria@banco.com" es miembro
    Cuando "pedro@banco.com" descarga la parte pública de "OBJ-000045"
    Entonces "maria@banco.com" recibe una sola notificación
```

---

# E18 — Eliminación definitiva y supervisión de Seguridad

## US-054 — Notificar a Seguridad la creación, edición y eliminación de objetos

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad de la Información |
| Prioridad | Must · F1 |
| Requisitos | RF-CUM-08, RF-INV-12, RF-INT-05, RF-INT-06 |
| Reglas | RN-067, RN-085, RN-107, RN-110 |
| Glosario | Notification, Managed Object, Audit Event |

**Descripción:** Como Seguridad de la Información, quiero enterarme de la creación, edición y eliminación de objetos (al instante si son críticos y en un resumen diario en los demás casos), para supervisar el inventario sin saturar mi bandeja.

**Criterios de aceptación:**
1. **Objetos Críticos:** aviso inmediato a todos los usuarios con rol Seguridad por cada creación, edición (metadatos, valor, clasificación, propietarios, asignaciones) y eliminación lógica o definitiva (RN-110).
2. **Resto de objetos:** un resumen diario (a una hora configurable; por defecto, 08:00) con las creaciones, ediciones y eliminaciones del día anterior, agrupadas por área. Si no hubo cambios, no se envía.
3. Si un objeto pasa a ser Crítico o deja de serlo, el cambio se avisa al instante.
4. Contenido: autor, acción, objeto (código, nombre, tipo, criticidad), fecha y hora, campos modificados y enlace. Nunca incluye Sensitive Payloads: un cambio de valor aparece como «Valor modificado» (RN-067, RN-085).
5. Canales: correo y Teams, según la preferencia de cada usuario.

```gherkin
# language: es
Característica: Notificaciones a Seguridad sobre cambios en objetos
  Como Seguridad de la Información
  Quiero enterarme de las creaciones, ediciones y eliminaciones de objetos
  Para supervisar el inventario sin saturar mi bandeja

  Antecedentes:
    Dado que "seg1@banco.com" y "seg2@banco.com" tienen rol "Seguridad"

  Escenario: Aviso inmediato por creación de un objeto crítico
    Cuando un Custodio registra el certificado "OBJ-000901" con criticidad "Crítico"
    Entonces "seg1@banco.com" y "seg2@banco.com" reciben un aviso inmediato con el autor, la acción y el objeto

  Escenario: Edición del valor de un objeto crítico sin exponer el valor
    Dado que el objeto "OBJ-000060" tiene criticidad "Crítico"
    Cuando un Custodio actualiza el valor secreto de "OBJ-000060"
    Entonces Seguridad recibe un aviso inmediato que indica "Valor modificado"
    Pero el aviso no contiene el valor anterior ni el nuevo

  Escenario: Cambios en objetos no críticos van al resumen diario
    Dado que ayer se crearon 3 objetos y se editaron 5 objetos, todos de criticidad "Medio"
    Cuando se ejecuta el resumen diario a las "08:00"
    Entonces Seguridad recibe un único resumen con los 8 cambios agrupados por área
    Y no recibió avisos inmediatos por esos cambios

  Escenario: Rebaja de criticidad se avisa al instante
    Cuando se aprueba el cambio de criticidad del objeto "OBJ-000060" de "Crítico" a "Alto"
    Entonces Seguridad recibe un aviso inmediato del cambio de criticidad

  Escenario: Sin cambios no hay resumen
    Dado que ayer no se crearon, editaron ni eliminaron objetos no críticos
    Cuando se ejecuta el resumen diario
    Entonces no se envía ningún resumen
```

---

## US-055 — Eliminar definitivamente un objeto creado por error

| Campo | Valor |
|---|---|
| Actor | Administrador (ejecuta), Seguridad (autoriza si el objeto es crítico) |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-11, RF-INV-12, RF-AUD-03 |
| Reglas | RN-054, RN-075, RN-107, RN-109, RN-110 |
| Glosario | Managed Object, Object Version, Sensitive Payload, Audit Event |

**Descripción:** Como Administrador, quiero eliminar definitivamente un objeto que se creó por error, para que no quede en el inventario, conservando en la auditoría la evidencia de que existió y de quién lo eliminó.

**Criterios de aceptación:**
1. Solo el rol Administrador puede ejecutar la eliminación definitiva, sobre objetos en cualquier estado (RN-109).
2. Motivo obligatorio, elegido de un catálogo (Creado por error, Duplicado, Datos incorrectos, Otro) y con una descripción, más una confirmación explícita escribiendo el código del objeto (RN-109).
3. Si el objeto es Crítico, requiere autorización previa de un usuario con rol Seguridad (RN-107).
4. Efectos (RN-109): revoca los accesos, cancela las solicitudes y cierra las alertas y hallazgos; destruye las versiones y el Sensitive Payload (destrucción de la llave de datos).
5. Los eventos de auditoría y de uso se conservan. Se registra el evento `OBJECT_PURGED` con el motivo, quién ejecutó, quién autorizó (si aplica) y una instantánea de metadatos sin valores sensibles. El código del objeto no se reutiliza.
6. Se notifica a los propietarios, al grupo y a Seguridad (RN-110).

```gherkin
# language: es
Característica: Eliminación definitiva de objetos creados por error
  Como Administrador
  Quiero eliminar definitivamente objetos creados por error
  Para depurar el inventario conservando la evidencia en la auditoría

  Antecedentes:
    Dado que estoy autenticado como "admin@banco.com" con rol "Administrador"

  Escenario: Eliminación definitiva de un objeto no crítico
    Dado que el objeto "OBJ-000778" de criticidad "Bajo" fue registrado por error
    Cuando elimino definitivamente "OBJ-000778" con el motivo "Creado por error" y la descripción "Registrado en el área equivocada"
    Y confirmo escribiendo el código "OBJ-000778"
    Entonces "OBJ-000778" no aparece en búsquedas ni consultas
    Y su valor cifrado y sus versiones quedan destruidos
    Y se registra un evento de auditoría "OBJECT_PURGED" con el motivo, el ejecutor y los metadatos, sin valores sensibles
    Y los eventos de auditoría anteriores de "OBJ-000778" siguen consultables

  Escenario: Solo el Administrador puede eliminar definitivamente
    Dado que "custodio@banco.com" tiene rol "Custodio" y no tiene rol "Administrador"
    Cuando "custodio@banco.com" intenta eliminar definitivamente "OBJ-000778"
    Entonces el sistema deniega la operación
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Objeto crítico requiere autorización de Seguridad
    Dado que el objeto "OBJ-000779" tiene criticidad "Crítico"
    Cuando solicito eliminar definitivamente "OBJ-000779" con el motivo "Certificado cargado dos veces"
    Entonces la eliminación queda pendiente de autorización de un usuario con rol "Seguridad"
    Cuando "seg1@banco.com" autoriza la eliminación
    Entonces "OBJ-000779" queda eliminado definitivamente

  Escenario: Seguridad rechaza la eliminación de un objeto crítico
    Dado que solicité eliminar definitivamente el objeto crítico "OBJ-000779"
    Cuando "seg1@banco.com" rechaza la solicitud con el comentario "El certificado está en uso"
    Entonces "OBJ-000779" no se elimina
    Y recibo una notificación con el motivo del rechazo

```

---

# E19 — Llave dividida

## US-056 — Marcar un objeto con llave dividida

| Campo | Valor |
|---|---|
| Actor | Custodio, Propietario (marcan); Seguridad (aprueba desmarcar) |
| Prioridad | Must · F1 |
| Requisitos | RF-INV-13, RF-PRT-06 |
| Reglas | RN-107, RN-110, RN-111, RN-112, RN-114, RN-116 |
| Glosario | Split Key, Managed Object, Key Component |

**Descripción:** Como Custodio, quiero marcar con un check «Llave dividida» los objetos que lo requieran, para que su valor se custodie dividido entre el grupo y Seguridad.

**Criterios de aceptación:**
1. Los objetos con valor sensible en custodia interna muestran el check «Llave dividida» (RN-111). Es una marca opcional, no una clasificación: no cambia la criticidad, la sensibilidad ni las demás reglas del objeto.
2. Por defecto está desmarcado. El sistema sugiere marcarlo cuando la criticidad es Crítico.
3. Sin el check, el valor es una sola pieza (RN-112). Con el check, se divide en dos componentes (US-057).
4. Marcar el check en un objeto existente exige cargar los dos componentes; mientras no estén, sigue vigente la versión anterior (RN-114).
5. Desmarcarlo requiere aprobación de Seguridad y registrar un valor nuevo, porque nadie conoce el valor completo (RN-114).
6. El check solo aplica a objetos en modo Interno; no aplica a Solo metadatos, que no tiene Sensitive Payload (RN-116).
7. El check se muestra en el detalle, los listados y los filtros. Sus cambios se auditan y se avisan a Seguridad al instante (RN-110).

```gherkin
# language: es
Característica: Check de llave dividida
  Como Custodio
  Quiero marcar los objetos que requieren llave dividida
  Para que su valor se custodie dividido entre el grupo y Seguridad

  Antecedentes:
    Dado que estoy autenticado con rol "Custodio" y soy miembro del grupo "GRP-SWIFT"

  Escenario: Check desmarcado por defecto
    Cuando registro una credencial de criticidad "Medio" en el grupo "GRP-SWIFT"
    Entonces el check "Llave dividida" aparece desmarcado

  Escenario: Sugerencia para objetos críticos
    Cuando registro una credencial de criticidad "Crítico" en el grupo "GRP-SWIFT"
    Entonces el sistema sugiere marcar el check "Llave dividida"

  Esquema del escenario: El check es independiente de la criticidad
    Cuando registro un objeto de criticidad "<criticidad>" con el check "Llave dividida" "<check>"
    Entonces el sistema acepta el registro

    Ejemplos:
      | criticidad | check      |
      | Alto       | Marcado    |
      | Crítico    | Desmarcado |
      | Bajo       | Desmarcado |

  Escenario: Desmarcar requiere Seguridad y un valor nuevo
    Dado que el objeto "OBJ-000065" tiene marcado el check "Llave dividida"
    Cuando solicito desmarcar el check "Llave dividida" de "OBJ-000065"
    Entonces el cambio queda pendiente de aprobación de un usuario con rol "Seguridad"
    Y al aprobarse se me exige registrar un valor nuevo completo
    Y el check permanece marcado hasta completar ambos pasos

  Escenario: El check no está disponible en modo Solo metadatos
    Cuando registro un certificado en modo "Solo metadatos"
    Entonces el check "Llave dividida" no está disponible
```

---

## US-057 — Custodiar los componentes de un objeto con llave dividida

| Campo | Valor |
|---|---|
| Actor | Miembros del grupo (componente del usuario), Seguridad (componente de Seguridad) |
| Prioridad | Must · F1 |
| Requisitos | RF-PRT-06, RF-PRT-03, RF-PRT-04, RF-AUD-02 |
| Reglas | RN-054, RN-060, RN-085, RN-103, RN-113, RN-114, RN-115, RN-116, RN-117 |
| Glosario | Key Component, Split Key, Sensitive Payload, Security Group, Temporary Access |

**Descripción:** Como miembro del grupo de un objeto con llave dividida, quiero conocer solo mi parte del valor mientras Seguridad custodia la otra, para que ninguna persona pueda usar el objeto por sí sola.

**Criterios de aceptación:**
1. Al registrar o renovar el objeto, un custodio designado del grupo ingresa el **componente del usuario** y un custodio designado de Seguridad ingresa el **componente de Seguridad** (US-058). El objeto no se activa, o la nueva versión no entra en vigor, hasta tener ambos (RN-114).
2. Cada componente se cifra con su propia llave de datos. Cada lado solo puede ver o cambiar su componente. Nadie ve ni recibe el valor completo, ni por la interfaz ni por la API (RN-113).
3. Como Seguridad no puede ser miembro de grupos (RN-103), ninguna persona puede conocer ambos componentes.
4. Cada objeto declara cómo se combinan los componentes (RN-116): concatenación (contraseñas, secretos, API keys, tokens), XOR con KCV por componente (claves criptográficas) o contraseña dividida del PFX (certificados con llave privada).
5. El miembro del grupo revela su componente con las reglas habituales. Al aprobarse la solicitud, se notifica a Seguridad para que revele su componente dentro de la misma ventana. Seguridad solo puede revelarlo mientras exista un acceso aprobado y vigente sobre el objeto (RN-115).
6. Cada revelado de un componente se audita indicando qué componente se reveló y quién lo hizo, y registra su uso (RN-060).
7. Los objetos con llave dividida no pueden recuperarse por API de aplicaciones.

```gherkin
# language: es
Característica: Custodia de componentes en objetos con llave dividida
  Como miembro del grupo de un objeto con llave dividida
  Quiero conocer solo mi parte del valor mientras Seguridad custodia la otra
  Para que ninguna persona pueda usar el objeto por sí sola

  Antecedentes:
    Dado que "op1@banco.com" y "op2@banco.com" son miembros activos del grupo "GRP-SWIFT"
    Y "seg1@banco.com" tiene rol "Seguridad"
    Y "op1@banco.com" y "op2@banco.com" son custodios aceptados del componente del usuario
    Y "seg1@banco.com" es custodio aceptado del componente de Seguridad

  Escenario: Registro con los dos componentes
    Dado que estoy autenticado como "op1@banco.com"
    Cuando registro la credencial "OBJ-000065" marcada con llave dividida y combinación "Concatenación"
    Y ingreso el componente del usuario
    Entonces "OBJ-000065" queda en estado "Borrador" con la condición "Pendiente de componente de Seguridad"
    Y "seg1@banco.com" recibe un aviso para ingresar el componente de Seguridad
    Cuando "seg1@banco.com" ingresa el componente de Seguridad de "OBJ-000065"
    Entonces "OBJ-000065" puede activarse

  Escenario: Cada lado ve solo su componente
    Dado que "op1@banco.com" tiene un acceso temporal activo sobre el objeto con llave dividida "OBJ-000065"
    Cuando "op1@banco.com" revela el valor de "OBJ-000065"
    Entonces ve únicamente el componente del usuario
    Y el componente de Seguridad se muestra como "Custodiado por Seguridad"
    Y se registra un evento de auditoría "SECRET_COMPONENT_REVEALED" con el componente "Usuario"

  Escenario: Seguridad revela su componente dentro de una solicitud aprobada
    Dado que la solicitud "SOL-0400" de "op1@banco.com" sobre "OBJ-000065" fue aprobada por "op2@banco.com"
    Entonces "seg1@banco.com" recibe un aviso para revelar su componente
    Cuando "seg1@banco.com" revela el componente de Seguridad de "OBJ-000065"
    Entonces ve únicamente el componente de Seguridad
    Y se registra un evento de auditoría "SECRET_COMPONENT_REVEALED" con el componente "Seguridad"

  Escenario: Seguridad no puede revelar sin un acceso vigente
    Dado que no existe ningún acceso aprobado y vigente sobre "OBJ-000065"
    Cuando "seg1@banco.com" intenta revelar el componente de Seguridad de "OBJ-000065"
    Entonces el sistema deniega la operación
    Y se registra un evento de auditoría con resultado "Denied"

  Escenario: Nadie obtiene el valor completo por API
    Cuando un cliente autorizado solicita por API el valor completo de "OBJ-000065"
    Entonces la API rechaza la solicitud indicando que el objeto tiene llave dividida

  Escenario: Renovación con los dos componentes
    Dado que "OBJ-000065" está activo en la versión 2
    Cuando "op2@banco.com" ingresa un nuevo componente del usuario
    Entonces la versión 3 queda pendiente del componente de Seguridad
    Y la versión 2 sigue vigente
    Cuando "seg1@banco.com" ingresa el nuevo componente de Seguridad
    Entonces la versión 3 entra en vigor

  Esquema del escenario: Forma de combinación según el tipo de objeto
    Cuando registro un objeto de tipo "<tipo>" marcado con llave dividida
    Entonces la forma de combinación propuesta es "<combinacion>"

    Ejemplos:
      | tipo                           | combinacion                   |
      | Credencial                     | Concatenación                 |
      | Clave criptográfica            | XOR                           |
      | Certificado con llave privada  | Contraseña dividida del PFX   |
```

---

## US-058 — Designar custodios de los componentes y guardia de Seguridad

| Campo | Valor |
|---|---|
| Actor | Responsable del grupo, Seguridad, custodios designados |
| Prioridad | Must · F1 |
| Requisitos | RF-PRT-07, RF-PRT-06 |
| Reglas | RN-103, RN-113, RN-115, RN-117, RN-118 |
| Glosario | Key Component, Split Key, Security Group, Escalation |

**Descripción:** Como Seguridad de la Información, quiero que cada objeto con llave dividida tenga custodios con nombre que acepten formalmente su rol, y una guardia de Seguridad para los usos urgentes, para cumplir el conocimiento dividido de PCI DSS sin bloquear la operación.

**Criterios de aceptación:**
1. Por cada objeto con llave dividida se designan de 1 a 2 custodios del componente del usuario (miembros activos del grupo) y de 1 a 2 custodios del componente de Seguridad (usuarios con rol Seguridad). Se recomiendan dos por lado: titular y suplente (RN-117).
2. El Responsable del grupo designa a los custodios del grupo y el responsable de Seguridad designa a los de Seguridad.
3. Cada custodio acepta formalmente su rol en la plataforma, con MFA. Hasta aceptarlo no puede ingresar ni revelar su componente. La aceptación queda registrada con su fecha.
4. Solo los custodios designados y aceptados pueden ingresar, ver o renovar su componente, y solo los custodios del grupo pueden solicitar acceso al objeto.
5. El objeto no puede activarse sin al menos un custodio aceptado por cada lado.
6. Si un custodio deja el grupo, pierde el rol Seguridad o es deshabilitado, deja de ser custodio, se alerta para designar un reemplazo y se recomienda renovar el valor (RN-117).
7. Seguridad mantiene un calendario de guardia 24x7 entre sus custodios, y la plataforma muestra quién está de guardia (RN-118).
8. Si, tras aprobarse una solicitud, ningún custodio de Seguridad revela su componente en el plazo configurado (30 min por defecto), se escala al responsable de la guardia y al Propietario Funcional (RN-118).

```gherkin
# language: es
Característica: Custodios de componentes y guardia de Seguridad
  Como Seguridad de la Información
  Quiero custodios con nombre para cada componente y una guardia de Seguridad
  Para cumplir el conocimiento dividido sin bloquear la operación

  Antecedentes:
    Dado que el objeto "OBJ-000065" tiene marcado el check "Llave dividida" y está asignado al grupo "GRP-SWIFT"
    Y "maria@banco.com" es responsable del grupo "GRP-SWIFT", del que son miembros "op1@banco.com", "op2@banco.com" y "op3@banco.com"
    Y "ciso@banco.com", "seg1@banco.com" y "seg2@banco.com" tienen rol "Seguridad"

  Escenario: Designación y aceptación de custodios
    Cuando "maria@banco.com" designa a "op1@banco.com" y "op2@banco.com" como custodios del componente del usuario de "OBJ-000065"
    Y "ciso@banco.com" designa a "seg1@banco.com" y "seg2@banco.com" como custodios del componente de Seguridad de "OBJ-000065"
    Entonces los cuatro custodios reciben una solicitud de aceptación
    Cuando "op1@banco.com" acepta su designación con MFA
    Entonces su aceptación queda registrada con la fecha

  Escenario: Un custodio sin aceptación no puede ver su componente
    Dado que "op2@banco.com" fue designado custodio de "OBJ-000065" pero no aceptó su designación
    Cuando "op2@banco.com" intenta revelar el componente del usuario de "OBJ-000065"
    Entonces el sistema deniega la operación indicando que primero debe aceptar su designación

  Escenario: Un miembro que no es custodio no puede solicitar acceso
    Dado que "op3@banco.com" es miembro de "GRP-SWIFT" pero no es custodio de "OBJ-000065"
    Cuando "op3@banco.com" intenta solicitar acceso a "OBJ-000065"
    Entonces el sistema indica que solo los custodios designados pueden solicitarlo

  Escenario: Reemplazo de un custodio que deja el grupo
    Dado que "op2@banco.com" es custodio aceptado de "OBJ-000065"
    Cuando "op2@banco.com" es retirado del grupo "GRP-SWIFT"
    Entonces "op2@banco.com" deja de ser custodio de "OBJ-000065"
    Y "maria@banco.com" recibe una alerta para designar un reemplazo

  Escenario: Escalamiento a la guardia de Seguridad
    Dado que "seg2@banco.com" está de guardia
    Y la solicitud "SOL-0500" de "op1@banco.com" sobre "OBJ-000065" fue aprobada a las "02:00"
    Cuando a las "02:30" ningún custodio de Seguridad ha revelado su componente
    Entonces se escala al responsable de la guardia y al Propietario Funcional de "OBJ-000065"
```

---

# E20 — Supervisión y emergencia

## US-059 — Revisar diariamente los eventos de seguridad

| Campo | Valor |
|---|---|
| Actor | Sistema, Seguridad de la Información |
| Prioridad | Must · F1 |
| Requisitos | RF-CUM-09, RF-AUD-01 |
| Reglas | RN-085, RN-119 |
| Glosario | Audit Event, Alert, Evidence Package |
| RNF | RNF-AUD-07 |

**Descripción:** Como Seguridad de la Información, quiero un reporte diario automático de los eventos de seguridad y alertas inmediatas por reglas de detección, para cumplir la revisión diaria de logs de PCI DSS (10.4.1 y 10.4.1.1) mientras no exista integración con el SIEM.

**Criterios de aceptación:**
1. Cada día, a una hora configurable (07:00 por defecto), se genera automáticamente el reporte de eventos de seguridad del día anterior: revelados, descargas, eliminaciones lógicas y definitivas (con su motivo), intentos denegados, cambios de roles, grupos, custodios y políticas, activaciones del modo de emergencia, inicios de sesión con cuentas locales y fallos de integridad (RN-119).
2. Reglas de detección automáticas, con umbrales configurables, generan alertas inmediatas a Seguridad. Como mínimo: 5 o más intentos denegados de un usuario en 15 minutos; revelados o descargas fuera del horario configurado; revelados masivos de un mismo usuario; cualquier inicio de sesión con cuenta local de emergencia; ruptura de la integridad de la bitácora.
3. Un usuario con rol Seguridad marca el reporte como revisado, con un comentario, a más tardar el siguiente día hábil. Si no lo hace, se alerta al responsable de Seguridad.
4. Las excepciones del reporte se gestionan (investigar, justificar o escalar) y quedan registradas.
5. El reporte, su revisión y las excepciones quedan como evidencia (US-044) y alimentan el control CC-10.
6. Sin valores sensibles (RN-085).

```gherkin
# language: es
Característica: Revisión diaria de eventos de seguridad
  Como Seguridad de la Información
  Quiero un reporte diario automático y alertas por reglas de detección
  Para cumplir la revisión diaria de logs sin integración con el SIEM

  Escenario: Generación del reporte diario
    Dado que ayer hubo 12 revelados, 3 descargas y 4 intentos denegados
    Cuando llega la hora configurada del reporte diario
    Entonces se genera el reporte con esas operaciones
    Y los usuarios con rol "Seguridad" reciben un aviso para revisarlo

  Escenario: Marcar el reporte como revisado
    Dado que existe el reporte diario pendiente de revisión
    Cuando "seg1@banco.com" lo marca como revisado con el comentario "Sin anomalías"
    Entonces el reporte queda "Revisado" con el usuario, la fecha y el comentario

  Escenario: Reporte no revisado a tiempo
    Dado que el reporte diario no se revisó durante el siguiente día hábil
    Cuando se ejecuta el control de revisión
    Entonces se alerta al responsable de Seguridad

  Esquema del escenario: Reglas de detección con alerta inmediata
    Cuando ocurre el evento "<evento>"
    Entonces Seguridad recibe una alerta inmediata de la regla "<regla>"

    Ejemplos:
      | evento                                                   | regla                         |
      | 5 intentos denegados de "op9@banco.com" en 10 minutos    | Intentos denegados repetidos  |
      | Revelado de "OBJ-000300" a las 03:00                     | Revelado fuera de horario     |
      | Inicio de sesión con la cuenta local "emerg.admin1"      | Uso de cuenta de emergencia   |
```

---

## US-060 — Retirada: conciliar Azure Key Vault con el inventario

| Campo | Valor |
|---|---|
| Actor | — |
| Prioridad | Retirada · DEC-35, DEC-26 |
| Requisitos | Retirado: RF-DES-03 |
| Reglas | Retirada: RN-120 |
| Glosario | Discovery Finding |

**Estado:** Retirada (DEC-35, DEC-26). Sin integración con Azure Key Vault no hay bóvedas que conciliar. La detección de objetos no registrados queda íntegramente a cargo del descubrimiento automático de la Fase 2 (US-049).

**Criterios de aceptación:** No aplica; historia retirada.

```gherkin
# language: es
Característica: Historia retirada
  # US-060 se retiró por DEC-35/DEC-26 (sin Azure Key Vault).

  Escenario: Referencia informativa
    Dado que no existe integración con Azure Key Vault en ninguna fase de este alcance
    Entonces la categoría de hallazgo "No registrado" depende únicamente del descubrimiento de la Fase 2
```

---

## US-061 — Retirada: modo de contingencia ante la pérdida de Azure Key Vault

| Campo | Valor |
|---|---|
| Actor | — |
| Prioridad | Retirada · DEC-35, DEC-27 |
| Requisitos | Retirado: RF-PRT-08 |
| Reglas | Retirada: RN-121 |
| Glosario | Sensitive Payload |

**Estado:** Retirada (DEC-35, DEC-27). Al no depender de Azure Key Vault, no existe el escenario "Azure Key Vault no disponible" que esta historia mitigaba. El riesgo equivalente — pérdida del certificado local que protege la KEK — se cubre con los respaldos cifrados y el procedimiento de recuperación ante desastres ya definidos (RNF-DIS-04, RNF-DIS-07), sin necesidad de un modo de activación conjunta independiente.

**Criterios de aceptación:** No aplica; historia retirada.

```gherkin
# language: es
Característica: Historia retirada
  # US-061 se retiró por DEC-35/DEC-27 (sin Azure Key Vault).

  Escenario: Referencia informativa
    Dado que la KEK es un certificado local respaldado según RNF-DIS-04 y RNF-DIS-07
    Entonces no existe un modo de contingencia independiente para la pérdida de un servicio de nube
```

---

## US-062 — Acceder en emergencia con cuentas locales

| Campo | Valor |
|---|---|
| Actor | Personas designadas con cuenta de emergencia, Administrador y Seguridad (activan), Sistema |
| Prioridad | Must · F1 |
| Requisitos | RF-ACC-10 |
| Reglas | RN-030, RN-032, RN-123 |
| Glosario | User, Role, Audit Event, Alert |
| RNF | RNF-SEG-01, RNF-SEG-15 |

**Descripción:** Como Seguridad de la Información, quiero que un conjunto reducido de personas designadas pueda entrar con cuentas locales cuando Entra ID no responde, para no perder el control de la plataforma durante una caída del servicio de identidad.

**Criterios de aceptación:**
1. La autenticación normal es siempre con Entra ID. Las cuentas locales existen solo para emergencias (RN-030, RN-123).
2. El Administrador las crea, con aprobación de Seguridad, para personas designadas por el responsable de Seguridad (6 como máximo por defecto). Son nominales, no se comparten y heredan los roles y grupos de la persona.
3. MFA local obligatorio (FIDO2 o TOTP), contraseña de al menos 15 caracteres y bloqueo tras 5 intentos fallidos (RNF-SEG-15).
4. Están deshabilitadas por defecto. La pantalla de acceso de emergencia solo aparece si la plataforma detecta que Entra ID no responde.
5. Activación conjunta: el modo de emergencia empieza cuando se autentican la cuenta de emergencia de un Administrador y la de un usuario de Seguridad. A partir de ahí, las demás cuentas de emergencia pueden entrar durante la ventana, de 8 h como máximo.
6. Al volver Entra ID, o al vencer la ventana, el modo termina y se cierran las sesiones locales.
7. Cada acción se audita marcada como «emergencia». La activación y cada inicio de sesión local generan una alerta crítica.
8. Tras cada uso se cambian las contraseñas de las cuentas usadas. Las cuentas se revisan cada trimestre y el procedimiento se prueba cada semestre.

```gherkin
# language: es
Característica: Acceso de emergencia con cuentas locales
  Como Seguridad de la Información
  Quiero que personas designadas puedan entrar con cuentas locales si Entra ID no responde
  Para no perder el control de la plataforma durante una caída del servicio de identidad

  Antecedentes:
    Dado que existen las cuentas locales de emergencia "emerg.admin1" de un Administrador, "emerg.seg1" de un usuario de Seguridad y "emerg.op1" de un operador designado

  Escenario: La pantalla de emergencia no está disponible con Entra ID activo
    Dado que Entra ID responde con normalidad
    Cuando un usuario intenta abrir la pantalla de acceso de emergencia
    Entonces la pantalla no está disponible

  Escenario: Activación conjunta del modo de emergencia
    Dado que la plataforma detecta que Entra ID no responde
    Cuando "emerg.admin1" inicia sesión con su contraseña y su MFA local
    Y "emerg.seg1" inicia sesión con su contraseña y su MFA local
    Entonces el modo de emergencia queda activo por un máximo de 8 horas
    Y Seguridad y el Administrador reciben una alerta crítica

  Escenario: Una sola persona no activa el modo de emergencia
    Dado que la plataforma detecta que Entra ID no responde
    Cuando solo "emerg.admin1" inicia sesión
    Entonces el modo de emergencia no se activa
    Y "emerg.op1" no puede iniciar sesión

  Escenario: Un operador designado entra durante la ventana
    Dado que el modo de emergencia está activo
    Cuando "emerg.op1" inicia sesión con su contraseña y su MFA local
    Entonces accede con sus roles y grupos habituales
    Y cada acción que realice queda auditada como "emergencia"

  Escenario: Fin del modo al volver Entra ID
    Dado que el modo de emergencia está activo
    Cuando Entra ID vuelve a responder
    Entonces el modo de emergencia termina y se cierran las sesiones locales
    Y se abre una alerta hasta que se cambien las contraseñas de las cuentas usadas

  Escenario: Bloqueo tras intentos fallidos
    Dado que la plataforma detecta que Entra ID no responde
    Cuando "emerg.op1" falla 5 veces su contraseña
    Entonces la cuenta "emerg.op1" queda bloqueada
    Y Seguridad recibe una alerta
```
