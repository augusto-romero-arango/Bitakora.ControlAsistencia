// InformacionColaborador es el unico tipo que queda en PublicEvents tras el issue #421 (DiaCalculado
// y HorasDiscriminadas se mudaron a PrivateEvents). Sin este test el proyecto se queda sin ninguno y
// Microsoft.Testing.Platform devuelve exit code 8 (ZeroTestsRan), que tumba la suite completa.
//
// Todos sus campos son string: la igualdad por valor del record por defecto ya es correcta y no
// necesita Equals/GetHashCode propios (MEF-ADR-0012). Estos tests congelan esa premisa: si el record
// gana una coleccion, la compararia por referencia y el test rojo lo delata.

using Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.PublicEvents.Tests.Colaboradores;

public class InformacionColaboradorIgualdadTests : IgualdadTestBase<InformacionColaborador>
{
    protected override InformacionColaborador CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override InformacionColaborador CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, InformacionColaborador)> CrearInstanciasDiferentes()
    {
        yield return ("CodigoColaborador",
            new InformacionColaborador("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new InformacionColaborador("EMP-001", "TI", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new InformacionColaborador("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new InformacionColaborador("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new InformacionColaborador("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
