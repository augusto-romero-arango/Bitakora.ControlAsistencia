namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de GET programacion/turnos y programacion/turnos/{id}, redeclarado aqui
/// (cero referencias a los ensamblados del BC, CA-1 del issue #502). Espejo del read model
/// FichaTurno de ReadModels tal como lo serializa el endpoint: si el contrato upstream cambia,
/// los tests de remodelado con JSON reales de dev son quienes lo detectan.
/// </summary>
public sealed record FichaTurno(
    string Id,
    string Nombre,
    bool EsDescanso,
    string HorarioResumido,
    IReadOnlyList<FranjaFicha> Franjas,
    string Descripcion,
    bool Completo);

public sealed record FranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetFin,
    IReadOnlyList<SubFranjaFicha> Descansos,
    IReadOnlyList<SubFranjaFicha> Extras,
    string? SedeId,
    string? NombreSede,
    string Descripcion);

public sealed record SubFranjaFicha(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DiaOffsetInicio,
    int DiaOffsetFin);
