using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class PropagadorIdentidadTenantHandlerTests
{
    private static readonly IdentidadTenant Identidad = new("tenant-fijo-01", "usuario-mcp");

    private static void AserirHeadersDeIdentidad(HandlerEnlatado handler)
    {
        handler.UltimaRequest!.Headers.GetValues(PropagadorIdentidadTenantHandler.HeaderTenantId)
            .Should().ContainSingle().Which.Should().Be(Identidad.TenantId);
        handler.UltimaRequest.Headers.GetValues(PropagadorIdentidadTenantHandler.HeaderUserId)
            .Should().ContainSingle().Which.Should().Be(Identidad.UserId);
    }

    [Fact]
    public async Task PropagadorIdentidadTenant_AgregaAmbosHeaders_CuandoEnviaUnaRequestCualquiera()
    {
        var (cliente, handler) = ClienteFalso.ConIdentidadTenant("{}", Identidad);

        await cliente.GetAsync("api/cualquier-ruta", TestContext.Current.CancellationToken);

        AserirHeadersDeIdentidad(handler);
    }

    [Fact]
    public async Task ProgramacionApi_EnviaLaIdentidadDeTenant_CuandoListaTurnos()
    {
        var (cliente, handler) = ClienteFalso.ConIdentidadTenant("[]", Identidad);
        var api = new ProgramacionApi(cliente);

        await api.ListarTurnos(TestContext.Current.CancellationToken);

        AserirHeadersDeIdentidad(handler);
    }

    [Fact]
    public async Task SedesApi_EnviaLaIdentidadDeTenant_CuandoListaFichasActivas()
    {
        var (cliente, handler) = ClienteFalso.ConIdentidadTenant("[]", Identidad);
        var api = new SedesApi(cliente);

        await api.ListarFichasActivas(TestContext.Current.CancellationToken);

        AserirHeadersDeIdentidad(handler);
    }

    [Fact]
    public async Task ControlHorasApi_EnviaLaIdentidadDeTenant_CuandoConsultaTurnosVigentes()
    {
        var (cliente, handler) = ClienteFalso.ConIdentidadTenant("[]", Identidad);
        var api = new ControlHorasApi(cliente);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        await api.ConsultarTurnosVigentes(hoy, hoy, null, null, TestContext.Current.CancellationToken);

        AserirHeadersDeIdentidad(handler);
    }

    [Fact]
    public async Task ColaboradoresApi_EnviaLaIdentidadDeTenant_CuandoListaFichas()
    {
        var (cliente, handler) = ClienteFalso.ConIdentidadTenant("[]", Identidad);
        var api = new ColaboradoresApi(cliente);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        await api.ListarFichas(hoy, null, [], 10, TestContext.Current.CancellationToken);

        AserirHeadersDeIdentidad(handler);
    }
}
