---
fecha: 2026-06-23
hora: 19:24
sesion: bug-investigator
tema: Smoke test CA-5 (HU-181) falla con NullReferenceException al leer DesgloseHoras del evento DiaCalculado
---

## Sintoma reportado
Los smoke tests del PR #182 (HU-181: "Publicar el DesgloseHoras real en el evento DiaCalculado")
fallaron en CI. Run 28065636580, job 83090255615, sobre el commit de main `dd70cab`.

## Investigacion

### Log de CI
- Falla 1 de 11 tests: el nuevo CA-5
  `RegistrarMarcacion_PublicaDiaCalculadoConDesgloseReal_CuandoMarcacionesCompletanLaFranja`.
- NO es timeout, NO es dead-letter, NO es infra. El test recibio el evento `DiaCalculado`
  (no hubo `TimeoutException`) y rompio al EVALUAR el desglose.
- Excepcion: `System.NullReferenceException` en
  `IntervaloTemporal.get_DuracionEnMinutos()` (IntervaloTemporal.cs:57:
  `_fin.MinutosAbsolutos - _inicio.MinutosAbsolutos`).
- Cadena del stacktrace: el test (linea 422, asercion
  `DesgloseHoras.TotalMinutosPorConcepto.Should().ContainKey(...)`) ->
  `DesgloseHoras.TotalMinutosPorConcepto` -> `DesgloseFranja.MinutosPorConcepto` ->
  `IntervaloClasificado.DuracionEnMinutos` -> `IntervaloTemporal.DuracionEnMinutos` -> NRE.
  Es decir: `_inicio`/`_fin` del `IntervaloTemporal` llegaron NULL tras deserializar.

### App Insights / Service Bus (entorno dev)
- `exceptions` ultimas 8h: 0 excepciones. La Function App NO fallo al publicar.
- Dead-letters en `dia-calculado/smoke-tests`: 0 activos, 0 DLQ (verificado con
  `az servicebus topic subscription show`). Descarta entrega/DLQ.
- Conclusion: el evento se publico OK y se consumio OK a nivel de transporte. El fallo es
  de CONTENIDO del payload, del lado del consumidor (el test) al usar los campos del VO.

### Correlacion con codigo
- `IntervaloTemporal` (Contracts) es `sealed partial class` con campos privados
  `_inicio`/`_fin` y SIN propiedades publicas `Inicio`/`Fin`. Su unica forma JSON valida
  (con `Inicio`/`Fin`) la produce el resolver `ConfigurarSerializacion` (ADR-0015).
- Productor (Program.cs de ControlHoras): el resolver con
  `ConfiguracionSerializacionControlHoras.ConfigurarResolver` SOLO se enchufa a Marten
  (`ConfigureMarten`, lineas 42-53). El canal de publicacion de eventos publicos a Service
  Bus (Wolverine, `HabilitarAzureServiceBusParaServerLess` + `PublicarEventoServerless<DiaCalculado>`)
  NO recibe ese resolver: serializa con STJ/Newtonsoft por defecto.
- `DiaCalculado` empezo a llevar `IntervaloTemporal` reales recien con HU-181: antes
  `CrearDiaCalculado()` publicaba `DesgloseHoras.Vacio` (sin intervalos), por eso el defecto
  no se manifestaba hasta ahora.

### Reproduccion empirica (prueba definitiva)
Mini-proyecto contra Contracts. Serializando `IntervaloClasificado(IntervaloTemporal 08:00-16:00)`:
- STJ por defecto (publicacion Wolverine):
  `{"Intervalo":{"DuracionEnMinutos":480,"DuracionEnHorasDecimales":8},"Concepto":0,...}`
  -> NO incluye `Inicio` ni `Fin`.
- STJ + resolver (lectura del fixture / Marten):
  `{"Intervalo":{"DuracionEnMinutos":480,...,"Inicio":{...},"Fin":{...}},...}`.
- Round-trip: deserializar el JSON "por defecto" con el resolver del fixture deja
  `_inicio`/`_fin` en null y `DuracionEnMinutos` lanza `NullReferenceException` IDENTICA al CI.

## Diagnostico
Causa raiz confirmada: **asimetria de serializacion entre el productor y el consumidor del
evento `DiaCalculado`**. El productor publica el `IntervaloTemporal` SIN sus campos
`Inicio`/`Fin` porque el canal de publicacion de eventos a Service Bus no aplica el resolver
de ADR-0015 (`ConfigurarSerializacion`); ese resolver solo esta registrado para la
persistencia con Marten. HU-181 destapo el defecto al ser el primer cambio que hace viajar
`IntervaloTemporal` reales dentro de `DiaCalculado` (antes iba `DesgloseHoras.Vacio`).

No es bug del test, ni de infra, ni del aggregate. Es bug de configuracion de serializacion
del publisher de eventos publicos del dominio ControlHoras (y, por contrato ADR-0002,
afecta a cualquier consumidor externo: el JSON publicado es lossy / no round-trippeable).

## Acciones
Issues propuestos (pendientes de confirmacion del usuario):
- [propuesto] `bug, tipo:refactor, dom:control-horas, estado:listo` — Aplicar el resolver de
  ADR-0015 (`ConfiguracionSerializacionControlHoras.ConfigurarResolver`) tambien al canal de
  publicacion de eventos a Service Bus (Wolverine), no solo a Marten, para que el
  `IntervaloTemporal` viaje con `Inicio`/`Fin` en el payload de `DiaCalculado`.

Workarounds discutidos (NO ejecutados): ninguno seguro recomendado; el fix correcto es de codigo.

## Preguntas abiertas
- Punto exacto de configuracion del serializador de Wolverine para el envio a Service Bus en
  `Cosmos.EventDriven.CritterStack.AzureServiceBus` (no expone hook obvio en el ensamblado;
  `wolverinefx.newtonsoft` esta presente como dependencia, lo que sugiere Newtonsoft por
  defecto para el body). Determina si el fix vive en el repo o requiere cambio en el harness.
- Otros VOs con ctor privado / campos privados que viajen en eventos publicos podrian sufrir
  el mismo defecto (ej. cualquier VO con `ConfigurarSerializacion`). Revisar el contrato
  completo de `DiaCalculado` y otros `IPublicEvent`.
- Validar el round-trip productor->consumidor como check de contrato (test que serialice con
  el path del publisher y deserialice con el del consumidor) para no depender solo del smoke.
