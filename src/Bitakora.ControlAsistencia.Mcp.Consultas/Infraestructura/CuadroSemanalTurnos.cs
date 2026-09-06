namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de GET programacion/plantillas-semanales y
/// programacion/plantillas-semanales/{id} (issue #625), redeclarado aqui (cero referencias a los
/// ensamblados del BC, MEF-ADR-0047 decision 3). Espejo de CuadroSemanalTurnosRespuesta tal como lo
/// serializa el endpoint: si el contrato upstream cambia, los tests de remodelado con JSON reales
/// de dev son quienes lo detectan.
/// </summary>
public sealed record CuadroSemanalTurnos(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<DiaDelCuadro> Dias);

/// <summary>Un dia con turno asignado. Ausencia en Dias = dia vacio (el cuadro materializado los omite, #624).</summary>
public sealed record DiaDelCuadro(int Semana, int Dia, TurnoDelCuadro Turno);

/// <summary>
/// El turno de un dia. Con Retirado true, Nombre y Descripcion viajan null (la ficha del turno ya
/// no existe en el catalogo).
/// </summary>
public sealed record TurnoDelCuadro(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);
