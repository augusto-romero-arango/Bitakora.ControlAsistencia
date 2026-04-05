// Issue #2: Tests de igualdad por valor para SubFranja
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

public class SubFranjaIgualdadTests : IgualdadTestBase<SubFranja>
{
    protected override SubFranja CrearInstancia() =>
        SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

    protected override SubFranja CrearInstanciaCopia() =>
        SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

    protected override IEnumerable<(string, SubFranja)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicio",
            SubFranja.Crear(new TimeOnly(10, 5), new TimeOnly(10, 15)));
        yield return ("HoraFin",
            SubFranja.Crear(new TimeOnly(10, 0), new TimeOnly(10, 30)));
        yield return ("OffsetInicio",
            SubFranja.Crear(new TimeOnly(1, 0), new TimeOnly(1, 15), diaOffsetInicio: 1, diaOffsetFin: 0));
        yield return ("OffsetFin",
            SubFranja.Crear(new TimeOnly(23, 50), new TimeOnly(0, 10), diaOffsetInicio: 0, diaOffsetFin: 1));
    }
}
