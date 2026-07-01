// Issue #114: Tests de contrato IEquatable para Retardo.
// Retardo es sealed class con IEquatable<Retardo> manual y SequenceEqual en las listas.
// CrearInstanciaCopia construye intervalos equivalentes en instancias nuevas para verificar
// que la comparacion es por valor, no por referencia.
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class RetardoIgualdadTests : IgualdadTestBase<Retardo>
{
    private static IntervaloTemporal Retardo30Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(8, 0)), new MomentoDelDia(new TimeOnly(8, 30)));

    private static IntervaloTemporal Compensacion20Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(12, 0)), new MomentoDelDia(new TimeOnly(12, 20)));

    private static IntervaloTemporal Retardo15Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(9, 0)), new MomentoDelDia(new TimeOnly(9, 15)));

    private static IntervaloTemporal Compensacion10Min() =>
        IntervaloTemporal.Crear(new MomentoDelDia(new TimeOnly(13, 0)), new MomentoDelDia(new TimeOnly(13, 10)));

    protected override Retardo CrearInstancia() =>
        Retardo.Crear([Retardo30Min()], [Compensacion20Min()]);

    protected override Retardo CrearInstanciaCopia() =>
        Retardo.Crear([Retardo30Min()], [Compensacion20Min()]);

    protected override IEnumerable<(string, Retardo)> CrearInstanciasDiferentes()
    {
        yield return ("TiempoRetardado",
            Retardo.Crear([Retardo15Min()], [Compensacion10Min()]));
        yield return ("TiempoCompensado",
            Retardo.Crear([Retardo30Min()], [Compensacion10Min()]));
        yield return ("CantidadDeIntervalosRetardados",
            Retardo.Crear([Retardo30Min(), Retardo15Min()], [Compensacion20Min()]));
    }
}
