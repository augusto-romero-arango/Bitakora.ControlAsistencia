using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class MomentoDelDiaIgualdadTests : IgualdadTestBase<MomentoDelDia>
{
    protected override MomentoDelDia CrearInstancia() =>
        new MomentoDelDia(new TimeOnly(8, 0));

    protected override MomentoDelDia CrearInstanciaCopia() =>
        new MomentoDelDia(new TimeOnly(8, 0));

    protected override IEnumerable<(string, MomentoDelDia)> CrearInstanciasDiferentes()
    {
        yield return ("Hora", new MomentoDelDia(new TimeOnly(9, 0)));
        yield return ("DiaOffset", new MomentoDelDia(new TimeOnly(8, 0), 1));
    }
}
