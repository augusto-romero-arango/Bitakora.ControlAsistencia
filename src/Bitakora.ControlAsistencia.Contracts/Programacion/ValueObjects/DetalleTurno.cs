namespace Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

/// <summary>
/// Representacion plana del turno que viaja en eventos entre dominios.
/// No tiene comportamiento de dominio, solo datos.
/// </summary>
public record DetalleTurno(
    string Nombre,
    IReadOnlyList<DetalleFranjaOrdinaria> FranjasOrdinarias);
