namespace Bitakora.ControlAsistencia.ReadModels.Sedes;

/// <summary>
/// Sede en la que esta instalado un dispositivo de marcacion, para estampar la sede de una marcacion.
/// </summary>
/// <remarks>
/// Sin historial: si un dispositivo queda instalado en mas de una sede a la vez, vale el ultimo
/// DispositivoInstalado aplicado.
///
/// SedeId es el stream key completo de la sede ("s:{codigo}"), con el que se carga FichaSede por Id;
/// nunca se parte ni se recompone a mano (CA-ADR-0031). Id es el DispositivoId opaco, tal cual llega.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record UbicacionDispositivo(string Id, string SedeId);
