using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public record HorasYMinutosSolicitadas(int Horas, int Minutos);

public record CrearJornada(Guid JornadaId, HorasYMinutosSolicitadas HorasSemanales,
    HorasYMinutosSolicitadas TopeDiario, HorasYMinutosSolicitadas MinimoDiario,
    int DiasDescansoPorSemana)
{
    public LimitesJornada ToLimitesJornada()
    {
        var errores = new List<Exception>();
        var semanales = CrearHoras(HorasSemanales);
        var tope = CrearHoras(TopeDiario);
        var minimo = CrearHoras(MinimoDiario);
        if (errores.Count > 0)
            throw new AggregateException(errores);

        return LimitesJornada.Crear(semanales!, tope!, minimo!, DiasDescansoPorSemana);

        HorasYMinutos? CrearHoras(HorasYMinutosSolicitadas datos)
        {
            try
            {
                return HorasYMinutos.Crear(datos.Horas, datos.Minutos);
            }
            catch (AggregateException ex)
            {
                errores.AddRange(ex.InnerExceptions);
                return null;
            }
        }
    }
}
