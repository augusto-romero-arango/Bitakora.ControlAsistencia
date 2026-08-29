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

    private const string CodigoSede = "001";
    private const string NombreSede = "Sede Principal";
    private const string CentroDeCostos = "CC-100";

    protected override FranjaDepurada CrearInstancia() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
            CodigoSede, NombreSede, CentroDeCostos);

    protected override FranjaDepurada CrearInstanciaCopia() =>
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
            CodigoSede, NombreSede, CentroDeCostos);

    protected override IEnumerable<(string, FranjaDepurada)> CrearInstanciasDiferentes()
    {
        yield return ("HoraInicioProgramada",
            new FranjaDepurada(new TimeOnly(7, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("HoraFinProgramada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(15, 0), 0, Entrada, Salida, false,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("DiaOffsetFin",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 1, Entrada, Salida, false,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("Entrada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, null, Salida, false,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("Salida",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, null, false,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("EsAnomala",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, true,
                CodigoSede, NombreSede, CentroDeCostos));
        yield return ("CodigoSedeProgramada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                "002", NombreSede, CentroDeCostos));
        yield return ("CodigoSedeProgramada (nulo)",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                null, NombreSede, CentroDeCostos));
        yield return ("NombreSedeProgramada",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                CodigoSede, "Otra Sede", CentroDeCostos));
        yield return ("CentroDeCostosProgramado",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                CodigoSede, NombreSede, "CC-200"));
        yield return ("CentroDeCostosProgramado (nulo)",
            new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Entrada, Salida, false,
                CodigoSede, NombreSede, null));
    }
}
