# Field Note: Review del PR #393

**Fecha**: 2026-08-16
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/393
**Issue**: #396

## Contexto

El PR nacio fuera del flujo Mefisto -- sin issue, sin DoR -- y se salto la etapa `infra-reviewer`.
Se regularizo despues con el issue #396. La revision la hizo el usuario a mano el 2026-08-15,
haciendo las veces de esa etapa saltada, y dejo las observaciones en el **body de la review**
(`PRR_kwDOR3ZjV88AAAABJriSSQ`) en vez de como comentarios inline. Eso importa para la mecanica de
respuesta: sin comentarios inline no hay `in_reply_to`, asi que las respuestas van como un
comentario unico del PR.

## Comentarios del review

| # | Categoria  | Resumen |
|---|------------|---------|
| 1 | corregir   | La alerta 3 (`servicebus-failure-spike`) tiene un nombre mas estrecho que su semantica: el filtro cubre cualquier trigger no-HTTP, no solo Service Bus |
| 2 | corregir   | Los comentarios HCL duplican extensamente CA-ADR-0009; comprimir segun MEF-ADR-0044 |
| 3 | sin accion | `resultCode == "500"` exacto (no 5xx): decision deliberada ya documentada en el ADR con su linea de escape |

## Correcciones aplicadas

Commit `739d438` (2 archivos, +12/-36).

**C1 -- rename.** `service_bus_failure_spike` -> `non_http_failure_spike` en el recurso Terraform,
su atributo `name`, su `description`, y la fila de la tabla de CA-ADR-0009 (donde la columna "Cubre"
paso de "Consumidores de eventos" a "Triggers no-HTTP", para que la tabla no reintrodujera por la
columna el sesgo que el rename le quita al nombre).

El rename era gratis y se verifico antes de hacerlo, no se asumio: el plan de CI reportaba
`service_bus_failure_spike will be created` con `1 to add, 1 to change, 0 to destroy` -- el recurso
no existia en Azure. Despues del merge habria costado destroy + create. Se verifico ademas que el
rename estaba contenido: 3 referencias en todo el repo, ningun `output` del modulo expone la alerta,
ningun otro modulo la consume.

**C2 -- compresion de comentarios.** De ~24 lineas a 8. Se conservo, con su cita a CA-ADR-0009, lo
que es restriccion local activa: que `exceptions` no expone el status code (vive en `requests`, de
ahi el join por `operation_Id`), y que los triggers no-HTTP reportan `resultCode "0"` -- valor no
contractual -- por lo que se descarta lo que si es un status HTTP (`100..599`). Se podo el racional
de `DeadletteredMessages`/`EntityName`, el detalle de la subscription `smoke-tests` y la ventaja de
deteccion temprana; los tres se verificaron presentes en CA-ADR-0009 antes de removerlos, para que
la poda no dejara nada huerfano. Los headers de recurso quedaron en una linea, replicando el estilo
que ya tenia la alerta 1 en el mismo archivo.

## Verificacion

El diff es solo HCL y Markdown -- no toca un solo `.cs` --, asi que `dotnet build`/`dotnet test` no
ejercitan nada de el. Se corrio la estatica de Terraform, que es la que aplica (y la misma que corre
`infra-reviewer`):

| Check | Resultado |
|---|---|
| `terraform fmt -check` (modulo monitoring) | exit 0 |
| `terraform init -backend=false` + `validate` (dev) | `Success! The configuration is valid.` |
| `terraform-plan` en CI tras el push | pass -- `non_http_failure_spike will be created`, `1 to add, 1 to change, 0 to destroy` |
| Referencias al nombre anterior | 0 |
| Referencias al nombre nuevo | 3 (las esperadas) |

## Mejoras a agentes

Se identificaron dos gaps del harness, ambos verificados empiricamente contra el cache del plugin
(version 0.24.0). **El usuario decidio no enrutar ninguno como draft.** Se dejan registrados aqui
por si reaparecen.

| Agente | Gap | Destino | Ajuste aplicado |
|---|---|---|---|
| `infra-writer` | regla faltante | (no enrutado) | Ninguno -- decision del usuario |
| `infra-reviewer` | regla faltante | (no enrutado) | Ninguno -- decision del usuario |

**Gap 1 -- `infra-writer` sin la doctrina de MEF-ADR-0044.** `test-writer`, `implementer`,
`smoke-test-writer` y `reviewer` tienen su seccion `## Doctrina de comentarios (MEF-ADR-0044)`;
`infra-writer` no tiene ninguna coincidencia con `0044`/`Context Delta`/`Decision Delta`. La causa
raiz esta en el propio ADR: su linea de alcance nombra como objetivos de propagacion a los tres
escritores de `.cs`, y el issue de propagacion (harness #635, cerrado) siguio esa lista -- pero la
§5 del ADR **si pone HCL en alcance del umbral de escritura**. El ADR sub-especifico su propio
conjunto de propagacion, omitiendo al unico agente que escribe HCL.

**Gap 2 -- `infra-reviewer` valida la forma del nombre, no su correspondencia con la semantica.** Su
checklist de Calidad tiene una sola regla de nombres: que sigan el patron
`<tipo>-<proyecto>-<ambiente>`. `servicebus-failure-spike` la pasaba -- esta bien formado, solo que
describe un subconjunto de lo que su filtro captura. No existe regla que compare el nombre contra lo
que la configuracion del recurso realmente hace. Para una alerta el costo es mayor que para otros
recursos: el nombre es la carga util que recibe el humano de guardia. Nota honesta de alcance: este
ajuste no habria prevenido este caso, porque el PR se salto `infra-reviewer` por completo.

## Lecciones

- **Un nombre de alerta es interfaz de usuario, no etiqueta interna.** Lo lee un humano en el peor
  momento posible. Cuando el nombre es mas estrecho que el filtro, el error no es cosmetico: produce
  un diagnostico equivocado. El costo de arreglarlo tiene fecha de vencimiento -- gratis mientras el
  `plan` diga `will be created`, destroy + create despues del merge --, asi que conviene revisar
  nombres de recursos que aun no existen antes de mergear, no despues.
- **Comprimir un comentario exige verificar que su contenido ya vive en otro lado.** La poda de
  MEF-ADR-0044 es segura solo si lo removido esta verificadamente en el ADR. HCL es hogar canonico
  de doctrina (MEF-ADR-0027, MEF-ADR-0032 B6) y por eso el ADR lo excluye del modo limpieza; aqui
  aplico el umbral de escritura -- no el de limpieza -- porque los comentarios se estaban
  introduciendo en un PR sin mergear.
- **La verificacion se elige por el diff, no por el runbook.** El pipeline manda `dotnet build` +
  `dotnet test`, pero un diff de solo HCL y Markdown no ejercita nada de eso: correrlos habria dado
  un verde que no significa nada. La verificacion real era `fmt -check` + `validate` + el `plan` de
  CI.
- **Saltarse una etapa del flujo desplaza el trabajo, no lo elimina.** Las dos observaciones son
  exactamente lo que `infra-reviewer` existe para atrapar (una de ellas, el gap 2, ni siquiera esta
  hoy en su checklist). El costo del atajo se pago despues, en revision manual.
