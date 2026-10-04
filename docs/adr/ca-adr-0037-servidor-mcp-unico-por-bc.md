# CA-ADR-0037: Servidor MCP unico por bounded context

## Estado

Aceptado (2026-10-03). Desviacion local de **MEF-ADR-0047 decision 2** (particion de servidores MCP por proposito), adoptada antes de que el marco la revise (harness#1848). Cuando harness#1848 se resuelva, este CA-ADR pasa a superado o se alinea. Field note de origen: `docs/bitacora/field-notes/2026-10-03-2117-planner.md` (PR #796).

## Contexto

MEF-ADR-0047 decision 2 fija la particion Consultas/Comandos con dos argumentos: CQS (Meyer) y "el servidor es la credencial": la system key `mcp_extension` es unica por Function App y da minimo privilegio real. El BC la aplico el 2026-08-29 (field note `procesadas/2026-08-29-2019-planner.md`).

Desde #558 los dos servidores estan detras de APIM: APIM lee la key con `listkeys` y la inyecta (`infra/modules/apim-mcp-api/main.tf`), y el cliente se autentica con OAuth (WorkOS AuthKit, flujo Connect). **La key nunca llega al cliente**, asi que la frontera real es la audiencia del token por recurso (RFC 9728 seccion 3; bitacora 2026-09-02; MEF-ADR-0032; MEF-ADR-0047 decision 7, gate OAuth en el borde).

No hay roles ni permisos en el codigo MCP: cualquier usuario autenticado de la organizacion obtiene cualquiera de las dos audiencias. La particion no restringe a nadie; cada usuario decide que conectores instala.

Quien escribe necesita leer para enriquecer la interaccion. Las descripciones de las tools de Comandos ya mandan al modelo al otro servidor ("obtenlo con listar_sedes", "miralo con listar_turnos"), y el operador instala siempre los dos conectores: la experiencia queda partida.

## Decision

1. **Un solo servidor MCP por bounded context**, con lectura y escritura: `Bitakora.ControlAsistencia.Mcp.Asistente`, ruta `{gateway}/mcp-asistente`.
2. El MCP es un **adaptador del lado del cliente (tipo BFF), no backend**: el corte de servidores sigue la tarea del agente y su perfil de riesgo, no CQS/CQRS.
3. **Nombre:** `/mefisto:scaffold-mcp` exige un proposito, y se elige `Asistente` porque "asistente MCP" es el actor que el glosario ya usa para el cliente. Se descarto "Mcp a secas" (el scaffold no lo soporta) y renombrar a mano despues del scaffold.
4. **Transicion sin ruptura:** el servidor nuevo se crea y convive con Consultas y Comandos hasta que los clientes se reconectan. Despues se retiran codigo y workflows (#805) e infra (#806).
5. **Duplicacion temporal aceptada:** durante la convivencia la capa de identidad existe en tres copias. Esto dispara la senal "tercer servidor" de la Rule of Three (MEF-ADR-0018), registrada en #572, pero **no se extrae** una biblioteca: el retiro deja una sola copia.

## Consecuencias

**Positivas**
- Un conector, una audiencia, una API de APIM y un Function App menos (al final queda uno de cada tipo en vez de dos).
- El modelo ve lectura y escritura en el mismo servidor.

**Negativas**
- Se pierde la **sesion de solo lectura**, el unico beneficio real que quedaba de la particion: un modelo no puede invocar tools que no ve, lo que limita el dano por prompt injection.
- El catalogo unico tiene 27 tools; la cantidad no cambia para el operador, que ya ve 28 hoy.

**Condicion de reapertura:** si aparece un actor real que deba leer sin poder escribir, se vuelve a separar, pero con autorizacion efectiva (roles o permisos en el token, o audiencias otorgadas por rol), nunca solo por CQS.
