using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ResolutorPlantillaTurnosPorIdTests
{
    private static readonly Guid PlantillaId = Guid.Parse("01a07000-1000-7000-9000-000000000001");

    private static ResolutorPlantillaTurnosPorId CrearResolutor(HttpStatusCode status, string cuerpo)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaDetalle(PlantillaId.ToString()), status, cuerpo);
        return new ResolutorPlantillaTurnosPorId(new ProgramacionApi(cliente));
    }

    [Fact]
    public async Task ResolverAsync_DevuelveLaFicha_CuandoLaPlantillaExiste()
    {
        var resolutor = CrearResolutor(HttpStatusCode.OK,
            Plantilla(PlantillaId.ToString(), "Ciclo Cocina", 7, true, [Dia(1, Turno("Cocina Manana"))]));

        var resultado = await resolutor.ResolverAsync(PlantillaId, TestContext.Current.CancellationToken);

        resultado.Ficha.Should().NotBeNull();
        resultado.Ficha!.Nombre.Should().Be("Ciclo Cocina");
        resultado.Ficha.DiasDelCiclo.Should().Be(7);
        resultado.NoExiste.Should().BeFalse();
        resultado.FalloDeLectura.Should().BeNull();
    }

    [Fact]
    public async Task ResolverAsync_MarcaNoExiste_CuandoElBackendResponde404()
    {
        var resolutor = CrearResolutor(HttpStatusCode.NotFound, "");

        var resultado = await resolutor.ResolverAsync(PlantillaId, TestContext.Current.CancellationToken);

        resultado.NoExiste.Should().BeTrue();
        resultado.Ficha.Should().BeNull();
        resultado.FalloDeLectura.Should().BeNull();
    }

    [Fact]
    public async Task ResolverAsync_DevuelveElFalloCrudo_CuandoLaLecturaFalla()
    {
        var resolutor = CrearResolutor(HttpStatusCode.InternalServerError, "detalle no disponible");

        var resultado = await resolutor.ResolverAsync(PlantillaId, TestContext.Current.CancellationToken);

        resultado.FalloDeLectura.Should().Be("detalle no disponible");
        resultado.Ficha.Should().BeNull();
        resultado.NoExiste.Should().BeFalse();
    }
}
