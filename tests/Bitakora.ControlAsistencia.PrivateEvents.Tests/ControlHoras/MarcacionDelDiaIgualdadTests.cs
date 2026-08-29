// Issue #424: todos los campos de MarcacionDelDia son primitivos -- la igualdad por valor del record
// por defecto ya es correcta y no necesita Equals/GetHashCode propios (MEF-ADR-0012). Este test
// congela esa premisa.
//
// Issue #464 (CA-6): suma los tres campos planos de sede marcada a la matriz de diferencias.

using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

public class MarcacionDelDiaIgualdadTests : IgualdadTestBase<MarcacionDelDia>
{
    private static readonly DateTime Timestamp = new(2026, 3, 15, 7, 0, 0);

    private const string CodigoSede = "001";
    private const string NombreSede = "Sede Principal";
    private const string CentroDeCostos = "CC-100";

    protected override MarcacionDelDia CrearInstancia() =>
        new(Timestamp, "ENTRADA", CodigoSede, NombreSede, CentroDeCostos);

    protected override MarcacionDelDia CrearInstanciaCopia() =>
        new(Timestamp, "ENTRADA", CodigoSede, NombreSede, CentroDeCostos);

    protected override IEnumerable<(string, MarcacionDelDia)> CrearInstanciasDiferentes()
    {
        yield return ("Timestamp",
            new MarcacionDelDia(Timestamp.AddMinutes(1), "ENTRADA", CodigoSede, NombreSede, CentroDeCostos));
        yield return ("Tipo",
            new MarcacionDelDia(Timestamp, "SALIDA", CodigoSede, NombreSede, CentroDeCostos));
        yield return ("Tipo (nulo)",
            new MarcacionDelDia(Timestamp, null, CodigoSede, NombreSede, CentroDeCostos));
        yield return ("CodigoSede",
            new MarcacionDelDia(Timestamp, "ENTRADA", "002", NombreSede, CentroDeCostos));
        yield return ("CodigoSede (nulo)",
            new MarcacionDelDia(Timestamp, "ENTRADA", null, NombreSede, CentroDeCostos));
        yield return ("NombreSede",
            new MarcacionDelDia(Timestamp, "ENTRADA", CodigoSede, "Otra Sede", CentroDeCostos));
        yield return ("CentroDeCostos",
            new MarcacionDelDia(Timestamp, "ENTRADA", CodigoSede, NombreSede, "CC-200"));
        yield return ("CentroDeCostos (nulo)",
            new MarcacionDelDia(Timestamp, "ENTRADA", CodigoSede, NombreSede, null));
    }
}
