using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;

// DTO de respuesta HTTP: excepcion al "el GET serializa la vista" bajo Rule of Three
// (MEF-ADR-0041 decision 4) porque COMPONE dos vistas -- CuadroSemanalTurnos + FichaTurno -- en una
// sola respuesta; precedente FichaColaboradorRespuesta.DesdeVista. Vive junto al endpoint, nunca en
// ReadModels: el read model no conoce esta composicion (CA-ADR-0034 decision 5 enmendada).
//
// Componer es PURA (sin QuerySession, sin IO): el endpoint carga cuadro + fichas y le delega toda la
// logica de lectura (Completa, Retirado), unica y testeable sin Marten.
public sealed record CuadroSemanalTurnosRespuesta(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<DiaDelCuadroRespuesta> Dias)
{
    private const int DiasPorSemana = 7;

    /// <summary>
    /// Compone el cuadro semanal resuelto: cada dia junta su <see cref="ReadModels.Programacion.DiaDelCuadro"/>
    /// con la <see cref="FichaTurno"/> correspondiente (ausencia = turno retirado). Completa es true
    /// solo si los 7 x Semanas dias tienen turno y todos esos turnos tienen ficha presente con
    /// Completo == true: "vigente" incluye programable, porque un turno que quedo incompleto vuelve
    /// la plantilla inusable (la asignacion futura la rechazaria con 409 TurnoIncompleto).
    /// </summary>
    /// <param name="cuadro">La vista <see cref="CuadroSemanalTurnos"/> ya cargada via LoadAsync.</param>
    /// <param name="fichasPorId">Las <see cref="FichaTurno"/> de los TurnoId distintos del cuadro,
    /// cargadas con una unica LoadManyAsync/Query, indexadas por su Id (TurnoId como string).</param>
    public static CuadroSemanalTurnosRespuesta Componer(
        CuadroSemanalTurnos cuadro,
        IReadOnlyDictionary<string, FichaTurno> fichasPorId)
    {
        var dias = cuadro.Dias
            .Select(dia => new DiaDelCuadroRespuesta(dia.Semana, dia.Dia, ResolverTurno(dia.TurnoId, fichasPorId)))
            .ToList();

        var completa = dias.Count == cuadro.Semanas * DiasPorSemana
                       && dias.All(dia => dia.Turno is { Retirado: false, Completo: true });

        return new CuadroSemanalTurnosRespuesta(cuadro.Id, cuadro.Nombre, cuadro.Semanas, completa, dias);
    }

    private static TurnoDelCuadroRespuesta ResolverTurno(
        string turnoId, IReadOnlyDictionary<string, FichaTurno> fichasPorId)
    {
        // Retirado se deriva de la AUSENCIA de FichaTurno (la proyeccion la borra en TurnoRetirado):
        // no hay flag que leer.
        if (!fichasPorId.TryGetValue(turnoId, out var ficha))
            return new TurnoDelCuadroRespuesta(turnoId, Nombre: null, Descripcion: null, Completo: false, Retirado: true);

        return new TurnoDelCuadroRespuesta(turnoId, ficha.Nombre, ficha.Descripcion, ficha.Completo, Retirado: false);
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
