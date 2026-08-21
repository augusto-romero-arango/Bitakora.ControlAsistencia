// Issue #424: todos los campos de MarcacionDelDia son primitivos -- la igualdad por valor del record
// por defecto ya es correcta y no necesita Equals/GetHashCode propios (MEF-ADR-0012). Este test
// congela esa premisa.

using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class MarcacionDelDiaIgualdadTests : IgualdadTestBase<MarcacionDelDia>
{
    private static readonly DateTime Timestamp = new(2026, 3, 15, 7, 0, 0);

    protected override MarcacionDelDia CrearInstancia() => new(Timestamp, "ENTRADA");

    protected override MarcacionDelDia CrearInstanciaCopia() => new(Timestamp, "ENTRADA");

    protected override IEnumerable<(string, MarcacionDelDia)> CrearInstanciasDiferentes()
    {
        yield return ("Timestamp", new MarcacionDelDia(Timestamp.AddMinutes(1), "ENTRADA"));
        yield return ("Tipo", new MarcacionDelDia(Timestamp, "SALIDA"));
        yield return ("Tipo (nulo)", new MarcacionDelDia(Timestamp, null));
    }
}
