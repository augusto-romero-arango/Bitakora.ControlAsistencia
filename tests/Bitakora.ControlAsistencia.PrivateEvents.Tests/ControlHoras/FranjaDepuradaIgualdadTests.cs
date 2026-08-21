// Issue #424: todos los campos de FranjaDepurada son primitivos -- la igualdad por valor del record
// por defecto ya es correcta y no necesita Equals/GetHashCode propios (MEF-ADR-0012). Este test
// congela esa premisa: si el record gana una coleccion, la compararia por referencia y el test rojo
// lo delata.

using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class FranjaDepuradaIgualdadTests : IgualdadTestBase<FranjaDepurada>
{
    private static readonly DateTime Entrada = new(2026, 3, 15, 6, 0, 0);
    private static readonly DateTime Salida = new(2026, 3, 15, 14, 0, 0);

    protected override FranjaDepurada CrearInstancia() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false);

    protected override FranjaDepurada CrearInstanciaCopia() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false);

    protected override IEnumerable<(string, FranjaDepurada)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicioProgramada",
            new FranjaDepurada(new TimeOnly(7, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false));
        yield return ("HoraFinProgramada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(15, 0), 0, Entrada, Salida, false));
        yield return ("DiaOffsetFin",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 1, Entrada, Salida, false));
        yield return ("Entrada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, null, Salida, false));
        yield return ("Salida",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, null, false));
        yield return ("EsAnomala",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, true));
    }
}
