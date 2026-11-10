// "El centinela jamas aparece en la API" vive en DesdeVista, una funcion pura: sin estas guardas solo
// el smoke test, despues del deploy, detectaria que 9999-12-31 se filtra a los clientes.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ObtenerFichaColaborador;

public class FichaColaboradorRespuestaTests
{
    // Oraculo independiente: literal, nunca importado de FichaColaborador.CentinelaVigenciaAbierta --
    // si esa constante cambiara, estos tests deben ponerse rojos, no seguirla en silencio.
    private static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    private static FichaColaborador FichaConVigenteHasta(DateOnly vigenteHasta) =>
        new(
            "CC-123456",
            "Ana Ramirez",
            "EMP-001",
            new DateOnly(2026, 8, 1),
            vigenteHasta,
            [new EtiquetaFicha("Área", "Tecnología")],
            new Dictionary<string, string> { ["area"] = "tecnologia" });

    private static FichaColaborador FichaConCodigoSede(string? codigoSede) =>
        FichaConVigenteHasta(CentinelaVigenciaAbierta) with { CodigoSede = codigoSede };

    [Fact]
    public void DesdeVista_DejaVigenteHastaVacio_CuandoLaVinculacionEstaAbierta()
    {
        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConVigenteHasta(CentinelaVigenciaAbierta));

        respuesta.VigenteHasta.Should().BeNull();
    }

    // Contracara: la consulta puntual incluye no-vigentes con su fecha real de terminacion.
    [Fact]
    public void DesdeVista_ConservaLaFechaDeTerminacion_CuandoLaVinculacionEstaTerminada()
    {
        var fechaEfectiva = new DateOnly(2026, 9, 30);

        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConVigenteHasta(fechaEfectiva));

        respuesta.VigenteHasta.Should().Be(fechaEfectiva);
    }

    // El DTO existe solo para ocultar el centinela, no para remodelar la ficha.
    [Fact]
    public void DesdeVista_CopiaElRestoDeLaVistaSinTransformar_CuandoTraduceLaFicha()
    {
        var ficha = FichaConVigenteHasta(CentinelaVigenciaAbierta);

        var respuesta = FichaColaboradorRespuesta.DesdeVista(ficha);

        respuesta.Id.Should().Be("CC-123456");
        respuesta.NombreCompleto.Should().Be("Ana Ramirez");
        respuesta.CodigoColaborador.Should().Be("EMP-001");
        respuesta.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
        respuesta.Etiquetas.Should().BeEquivalentTo([new EtiquetaFicha("Área", "Tecnología")]);
        respuesta.EtiquetasNormalizadas.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["area"] = "tecnologia" });
    }

    [Fact]
    public void DesdeVista_CopiaElCodigoSede_CuandoLaFichaTieneSede()
    {
        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConCodigoSede("SEDE-BOG-01"));

        respuesta.CodigoSede.Should().Be("SEDE-BOG-01");
    }

    [Fact]
    public void DesdeVista_CopiaJornadaId_CuandoLaFichaTieneJornadaAsignada()
    {
        var j1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ficha = FichaConVigenteHasta(CentinelaVigenciaAbierta) with { JornadaId = j1 };

        var respuesta = FichaColaboradorRespuesta.DesdeVista(ficha);

        respuesta.JornadaId.Should().Be(j1);
    }

    [Fact]
    public void DesdeVista_DejaJornadaIdNulo_CuandoLaFichaNoTieneJornadaAsignada()
    {
        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConVigenteHasta(CentinelaVigenciaAbierta));

        respuesta.JornadaId.Should().BeNull();
    }
}
