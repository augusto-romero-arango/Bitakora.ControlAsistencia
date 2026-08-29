namespace Bitakora.ControlAsistencia.ReadModels.Sedes;

/// <summary>
/// Ubicacion vigente de un dispositivo de marcacion: responde "en que sede esta instalado el
/// dispositivo X" para estampar la sede de una marcacion.
/// </summary>
/// <remarks>
/// Solo la ubicacion vigente, sin lista ni historial: ante un dispositivo instalado en mas de una
/// sede a la vez vale el ultimo DispositivoInstalado aplicado -- la recencia la da el orden de
/// aplicacion de los eventos, no un campo de fecha en esta vista.
///
/// SedeId guarda el stream key completo de la sede ("s:{codigo}", SedeAggregateRoot.ComputarStreamId):
/// es la referencia con la que el consumidor carga FichaSede por Id, y nunca se parte ni se recompone
/// a mano (CA-ADR-0031, MEF-ADR-0037). Id es el DispositivoId opaco, tal cual llega.
///
/// Record plano sin partial: el comportamiento de proyeccion vive en la clase companion
/// UbicacionDispositivoProjection, en el worker (MEF-ADR-0035); ReadModels es la cuarta isla y no
/// referencia ningun otro proyecto.
///
/// No es calco del aggregate: SedeAggregateRoot modela una lista de dispositivos por sede; esta
/// vista invierte la relacion -- una sede por dispositivo (MEF-ADR-0041).
/// </remarks>
public sealed record UbicacionDispositivo(string Id, string SedeId);
