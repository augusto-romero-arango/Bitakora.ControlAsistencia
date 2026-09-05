using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;

// Issue #625 (opcion B, decision del experto 2026-09-05, reemplaza CA-ADR-0034 decision 5): DTO de
// respuesta HTTP, excepcion bajo Rule of Three (MEF-ADR-0041 decision 4, skills/projections/
// read-apis.md "El GET serializa la vista; el DTO de respuesta es excepcion") -- el proposito real
// es COMPONER dos vistas (CuadroSemanalTurnos + FichaTurno) en una sola respuesta, precedente
// FichaColaboradorRespuesta.DesdeVista(...) en ObtenerFichaColaborador. Vive en el namespace del
// endpoint, nunca en ReadModels: el read model no conoce esta composicion.
//
// Componer es una funcion PURA (sin QuerySession, sin IO): el endpoint carga cuadro + fichas y le
// delega toda la logica de negocio de lectura (Completa, Retirado) a este metodo -- la unica pieza
// testeable sin Marten (CA-1..CA-3). Stub de fase roja: implementacion real fuera del alcance de
// este agente (projection-test-writer).
public sealed record CuadroSemanalTurnosRespuesta(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<DiaDelCuadroRespuesta> Dias)
{
    /// <summary>
    /// Compone el cuadro semanal resuelto: cada dia junta su <see cref="ReadModels.Programacion.DiaDelCuadro"/>
    /// con la <see cref="FichaTurno"/> correspondiente (ausencia = turno retirado). Completa es true
    /// solo si los 7 x Semanas dias tienen turno y todos esos turnos tienen ficha presente con
    /// Completo == true (decision del planner, issue #625: "vigente" incluye programable).
    /// </summary>
    /// <param name="cuadro">La vista <see cref="CuadroSemanalTurnos"/> ya cargada via LoadAsync.</param>
    /// <param name="fichasPorId">Las <see cref="FichaTurno"/> de los TurnoId distintos del cuadro,
    /// cargadas con una unica LoadManyAsync/Query, indexadas por su Id (TurnoId como string).</param>
    public static CuadroSemanalTurnosRespuesta Componer(
        CuadroSemanalTurnos cuadro,
        IReadOnlyDictionary<string, FichaTurno> fichasPorId)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Un dia resuelto del cuadro: mismo (Semana, Dia) de <see cref="ReadModels.Programacion.DiaDelCuadro"/>,
/// con el turno ya juntado con su ficha.
/// </summary>
public sealed record DiaDelCuadroRespuesta(int Semana, int Dia, TurnoDelCuadroRespuesta Turno);

/// <summary>
/// El turno de un dia, resuelto contra <see cref="FichaTurno"/>. Con ficha ausente (turno retirado):
/// Retirado = true, Nombre/Descripcion null, Completo = false.
/// </summary>
public sealed record TurnoDelCuadroRespuesta(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);
