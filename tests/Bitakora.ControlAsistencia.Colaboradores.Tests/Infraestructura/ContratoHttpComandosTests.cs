// Issue #378 (hermana de AliasEventosColaboradoresTests, misma tecnica sobre otro contrato):
// congela el verbo y la ruta HTTP que el issue pacto para los dos comandos que toca, leyendolos del
// HttpTriggerAttribute real por reflection. MEF-ADR-0043 fija el contrato HTTP (verbo + ruta) como
// campo Critico del DoR (seccion 6, enmienda a MEF-ADR-0011): una vez pactado, es un contrato con
// el cliente, no un literal de estilo.
//
// Por que existe: el valor de Route no lo ejercita NINGUN otro test local. Los tests de
// FunctionEndpoint instancian la clase y llaman Run(...) directamente, sin pasar por el enrutador
// del host, asi que pasan en verde con cualquier Route -- incluso con el atributo borrado. La unica
// otra verificacion es el smoke test, que corre contra dev y solo despues del deploy: es decir, el
// dia que alguien revierta la ruta, el fallo aparece en el entorno desplegado, no en el build.
// Este test lo adelanta al build, que es donde se paga barato.
//
// CA-5 en particular no tiene NINGUNA otra red: el smoke test de RegistrarColaborador declina
// afirmar que la ruta PascalCase vieja desaparecio, porque el enrutamiento de ASP.NET Core es
// case-insensitive y /api/Colaboradores seguiria resolviendo igual (documentado en ese archivo).
// Aqui el literal si distingue mayusculas.
//
// No necesita host ni Postgres: leer un atributo es calculo puro en memoria.

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

    // CA-1/CA-6 (MEF-ADR-0043 paso 1 -- create disfrazado: POST a la sub-coleccion que el evento
    // realmente instancia). La ruta vieja Colaboradores/Reingresos no puede sobrevivir a esta
    // asercion: es el mismo y unico Route de esta Function.
    [Fact]
    public void IniciarVinculacion_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = TriggerDe<IniciarVinculacionEndpoint>();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("colaboradores/{id}/vinculaciones");
    }

    // MEF-ADR-0006: el nombre de la Function es el verbo infinitivo + sustantivo del comando, y
    // sobrevive al rename del feature folder (CA-4).
    [Fact]
    public void IniciarVinculacion_SeRegistraConElNombreDelComando()
    {
        NombreDeLaFunction<IniciarVinculacionEndpoint>().Should().Be("IniciarVinculacion");
    }

    // CA-5: "colaboradores" en kebab-case minusculo (MEF-ADR-0043 seccion 3); la ruta PascalCase
    // deja de existir, sin ningun otro cambio en ese endpoint (mismo verbo, misma forma).
    [Fact]
    public void RegistrarColaborador_ExponeLaRutaEnKebabCaseMinusculo()
    {
        var trigger = TriggerDe<RegistrarColaboradorEndpoint>();

        trigger.Methods.Should().Equal("post");
        trigger.Route.Should().Be("colaboradores");
    }

    // Issue #465 (MEF-ADR-0043 paso 2): PUT, reemplazo completo del VO atomico "sede del
    // colaborador" -- precedente exacto AsignarEtiqueta, simplificado (el codigo viaja en el body,
    // sin segmento adicional de ruta).
    [Fact]
    public void AsignarSede_ExponeElVerboYLaRutaPactadosEnElIssue()
    {
        var trigger = TriggerDe<AsignarSedeEndpoint>();

        trigger.Methods.Should().Equal("put");
        trigger.Route.Should().Be("colaboradores/{id}/sede");
    }

    // MEF-ADR-0006: el nombre de la Function es el verbo infinitivo + sustantivo del comando.
    [Fact]
    public void AsignarSede_SeRegistraConElNombreDelComando()
    {
        NombreDeLaFunction<AsignarSedeEndpoint>().Should().Be("AsignarSede");
    }
}
