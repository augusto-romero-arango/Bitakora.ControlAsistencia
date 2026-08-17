// Issue #322: paridad de campos e igualdad de Colaborador (ControlHoras.DomainEvents) con
// InformacionColaborador (PublicEvents.Colaboradores) y DetalleColaborador (PrivateEvents.Programacion)
// -- payload por rol, CA-ADR-0029 decision #5.
// CA-1: todos los campos son string, asi que la igualdad por valor del record por defecto ya es
// correcta y no lleva Equals/GetHashCode custom. Este test congela ese contrato: el dia que alguien
// agregue una coleccion al record (o un Equals custom incompleto), la igualdad deja de cumplirse
// aqui. Espejo de ColaboradorProgramadoIgualdadTests (Programacion.Tests, issue #319).

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class ColaboradorProgramadoIgualdadTests : IgualdadTestBase<ColaboradorProgramado>
{
    protected override ColaboradorProgramado CrearInstancia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override ColaboradorProgramado CrearInstanciaCopia() =>
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    protected override IEnumerable<(string, ColaboradorProgramado)> CrearInstanciasDiferentes()
    {
        yield return ("CodigoColaborador",
            new ColaboradorProgramado("EMP-002", "CC", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("TipoIdentificacion",
            new ColaboradorProgramado("EMP-001", "CE", "1234567890", "Luis Augusto", "Barreto"));
        yield return ("NumeroIdentificacion",
            new ColaboradorProgramado("EMP-001", "CC", "9999999999", "Luis Augusto", "Barreto"));
        yield return ("Nombres",
            new ColaboradorProgramado("EMP-001", "CC", "1234567890", "Otro Nombre", "Barreto"));
        yield return ("Apellidos",
            new ColaboradorProgramado("EMP-001", "CC", "1234567890", "Luis Augusto", "Otro Apellido"));
    }
}
