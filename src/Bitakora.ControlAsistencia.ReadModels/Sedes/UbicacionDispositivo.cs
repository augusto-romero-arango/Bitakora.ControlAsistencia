namespace Bitakora.ControlAsistencia.ReadModels.Sedes;

/// <summary>
/// Ubicacion vigente de un dispositivo de marcacion (issue #475), consumida por el resolver
/// ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado (#467, Function App de Sedes) para
/// estampar la sede de una marcacion sin front: dispositivo -> sede -> centro de costos.
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el
/// comportamiento de proyeccion vive en la clase companion UbicacionDispositivoProjection, en el
/// worker (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels, la cuarta isla
/// del repo -- cero referencias de proyecto (ver el .csproj).
///
/// Sin sufijo de implementacion (MEF-ADR-0041 decision 3). No es calco del aggregate:
/// SedeAggregateRoot modela una LISTA de dispositivos por sede (FichaSede.Dispositivos, issue
/// #461); esta vista invierte la relacion -- una sede por dispositivo -- porque es exactamente lo
/// que el resolver necesita preguntar ("en que sede esta instalado el dispositivo X").
///
/// Id es el DispositivoId opaco (ajeno al sistema, se estampa tal cual llega -- ver
/// Sedes.DomainEvents.DispositivoInstalado). SedeId es el stream key completo de la sede vigente
/// ("s:{codigo}", SedeAggregateRoot.ComputarStreamId, CA-ADR-0031/MEF-ADR-0037) -- referencia
/// directa para que el consumidor cargue FichaSede por Id sin partir strings a mano; este read
/// model nunca recomputa ni parte ese valor a mano.
///
/// Solo la ubicacion vigente: sin lista ni historial (decision del experto, sesion 2026-08-29).
/// Ante multi-sede (el mismo dispositivo instalado en mas de una sede a la vez), vale el registro
/// mas reciente aplicado -- la recencia la da el orden de aplicacion de eventos del daemon (la
/// secuencia global del event store), no un campo de fecha persistido en esta vista.
/// </remarks>
public sealed record UbicacionDispositivo(string Id, string SedeId);
