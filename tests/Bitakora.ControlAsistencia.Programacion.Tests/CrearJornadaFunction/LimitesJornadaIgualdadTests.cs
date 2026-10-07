using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class LimitesJornadaIgualdadTests : IgualdadTestBase<LimitesJornada>
{
    private static LimitesJornada Crear(int semanales, int tope, int minimo, int descansos) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(minimo, 0), descansos);

    protected override LimitesJornada CrearInstancia() => Crear(42, 8, 4, 1);
    protected override LimitesJornada CrearInstanciaCopia() => Crear(42, 8, 4, 1);
    protected override IEnumerable<(string atributo, LimitesJornada diferente)> CrearInstanciasDiferentes()
    {
        yield return ("horas semanales", Crear(43, 8, 4, 1));
        yield return ("tope diario", Crear(42, 9, 4, 1));
        yield return ("mínimo diario", Crear(42, 8, 5, 1));
        yield return ("días de descanso", Crear(42, 8, 4, 0));
    }
}
