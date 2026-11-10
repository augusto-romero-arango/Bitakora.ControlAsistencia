// Congela verbo y ruta HTTP pactados (contrato con el cliente, MEF-ADR-0043) leyendolos del
// HttpTriggerAttribute: los tests de FunctionEndpoint llaman Run(...) sin el enrutador del host y
// pasan con cualquier Route, y el smoke solo lo detectaria tras el deploy. Ademas el literal
// distingue mayusculas, que el enrutamiento de ASP.NET Core no distingue.

using System.Reflection;
using AwesomeAssertions;
using Microsoft.Azure.Functions.Worker;
using IniciarVinculacionEndpoint =
    Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction.FunctionEndpoint;
using RegistrarColaboradorEndpoint =
    Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.FunctionEndpoint;
using AsignarSedeEndpoint =
    Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction.FunctionEndpoint;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public class ContratoHttpComandosTests
{
    private static MethodInfo Run<TEndpoint>() =>
        typeof(TEndpoint).GetMethod(nameof(IniciarVinculacionEndpoint.Run))!;

    private static HttpTriggerAttribute TriggerDe<TEndpoint>() =>
        Run<TEndpoint>().GetParameters()
            .Select(parametro => parametro.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(trigger => trigger is not null)!;

    private static string? NombreDeLaFunction<TEndpoint>() =>
        Run<TEndpoint>().GetCustomAttribute<FunctionAttribute>()?.Name;

    [Fact]
    public void IniciarVinculacion_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = TriggerDe<IniciarVinculacionEndpoint>();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("colaboradores/{id}/vinculaciones");
    }

    // El nombre de la Function es el del comando (MEF-ADR-0006) y no cambia con el feature folder.
    [Fact]
    public void IniciarVinculacion_SeRegistraConElNombreDelComando()
    {
        NombreDeLaFunction<IniciarVinculacionEndpoint>().Should().Be("IniciarVinculacion");
    }

    [Fact]
    public void RegistrarColaborador_ExponeLaRutaEnKebabCaseMinusculo()
    {
        var trigger = TriggerDe<RegistrarColaboradorEndpoint>();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("colaboradores");
    }

    [Fact]
    public void AsignarSede_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = TriggerDe<AsignarSedeEndpoint>();

        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("colaboradores/{id}/sede");
    }

    [Fact]
    public void AsignarSede_SeRegistraConElNombreDelComando()
    {
        NombreDeLaFunction<AsignarSedeEndpoint>().Should().Be("AsignarSede");
    }
}
