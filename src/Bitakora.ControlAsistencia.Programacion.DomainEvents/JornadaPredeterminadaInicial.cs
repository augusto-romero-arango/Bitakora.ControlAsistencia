namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public static class JornadaPredeterminadaInicial
{
    public static LimitesJornada Limites { get; } = LimitesJornada.Crear(
        HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1);
}
