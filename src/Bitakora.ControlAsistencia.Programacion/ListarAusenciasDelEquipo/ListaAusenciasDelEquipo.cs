namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public sealed record TramoAplicado(DateOnly Desde, DateOnly Hasta);

public sealed record AusenciaDelPeriodo(Guid Id, string Motivo, IReadOnlyList<TramoAplicado> Tramos);

public sealed record AusenciasDeColaborador(
    string CodigoColaborador,
    string NombreCompleto,
    IReadOnlyList<AusenciaDelPeriodo> Ausencias);

public sealed record ListaAusenciasDelEquipo(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<AusenciasDeColaborador> Colaboradores);
