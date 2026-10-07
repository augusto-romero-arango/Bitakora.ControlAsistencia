using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class HorasYMinutosIgualdadTests : IgualdadTestBase<HorasYMinutos>
{
    protected override HorasYMinutos CrearInstancia() => HorasYMinutos.Crear(8, 12);
    protected override HorasYMinutos CrearInstanciaCopia() => HorasYMinutos.Crear(8, 12);
    protected override IEnumerable<(string atributo, HorasYMinutos diferente)> CrearInstanciasDiferentes()
    {
        yield return ("horas", HorasYMinutos.Crear(9, 12));
        yield return ("minutos", HorasYMinutos.Crear(8, 13));
    }
}
