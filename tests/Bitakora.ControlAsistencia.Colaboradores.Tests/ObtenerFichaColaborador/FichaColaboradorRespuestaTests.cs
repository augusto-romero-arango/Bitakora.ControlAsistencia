// Issue #356 CA-6: la regla mas distintiva del contrato HTTP de ObtenerFichaColaborador -- "el
// centinela jamas aparece en la API" -- vive en la traduccion vista -> respuesta
// (FichaColaboradorRespuesta.DesdeVista), una funcion pura. Sin estas guardas su unica verificacion
// seria el smoke test contra dev, que solo emite veredicto DESPUES del deploy: una regresion aqui
// (quitar la traduccion, comparar contra otra fecha) compilaria, pasaria el resto de la suite y
// filtraria 9999-12-31 a los clientes.
//
// El test de composicion hermano (ComposicionServiciosTests
// .AgregarServiciosColaboradores_ResuelveElEndpointDeObtenerFichaColaborador_...) cubre el wiring
// del endpoint; el resto de Run (parseo con 400, LoadAsync, 200/404) es black-box del smoke test.
//
// Oraculo independiente (MEF-ADR-0002, no-tautologia): el centinela se escribe como literal aqui,
// nunca se importa de FichaColaborador.CentinelaVigenciaAbierta -- si alguien cambiara esa
// constante en produccion, estos tests deben ponerse rojos, no seguirla en silencio.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ObtenerFichaColaborador;

public class FichaColaboradorRespuestaTests
{
    private static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    private static FichaColaborador FichaConVigenteHasta(DateOnly vigenteHasta) =>
        new(
            "CC:123456",
            "Ana Ramirez",
            "EMP-001",
            new DateOnly(2026, 8, 1),
            vigenteHasta,
            [new EtiquetaFicha("Área", "Tecnología")],
            new Dictionary<string, string> { ["area"] = "tecnologia" });

    [Fact]
    public void DesdeVista_DejaVigenteHastaVacio_CuandoLaVinculacionEstaAbierta()
    {
        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConVigenteHasta(CentinelaVigenciaAbierta));

        respuesta.VigenteHasta.Should().BeNull();
    }

    // Contracara del anterior: la traduccion no puede degenerar en "VigenteHasta siempre vacio" --
    // CA-6 exige que la consulta puntual INCLUYA no-vigentes mostrando su fecha real de terminacion.
    [Fact]
    public void DesdeVista_ConservaLaFechaDeTerminacion_CuandoLaVinculacionEstaTerminada()
    {
        var fechaEfectiva = new DateOnly(2026, 9, 30);

        var respuesta = FichaColaboradorRespuesta.DesdeVista(FichaConVigenteHasta(fechaEfectiva));

        respuesta.VigenteHasta.Should().Be(fechaEfectiva);
    }

    // El resto de la vista viaja sin transformacion: el DTO existe UNICAMENTE para ocultar el
    // centinela (MEF-ADR-0041 decision 4, excepcion bajo Rule of Three), no para remodelar la ficha.
    [Fact]
    public void DesdeVista_CopiaElRestoDeLaVistaSinTransformar_CuandoTraduceLaFicha()
    {
        var ficha = FichaConVigenteHasta(CentinelaVigenciaAbierta);

        var respuesta = FichaColaboradorRespuesta.DesdeVista(ficha);

        respuesta.Id.Should().Be("CC:123456");
        respuesta.NombreCompleto.Should().Be("Ana Ramirez");
        respuesta.CodigoColaborador.Should().Be("EMP-001");
        respuesta.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
        respuesta.Etiquetas.Should().BeEquivalentTo([new EtiquetaFicha("Área", "Tecnología")]);
        respuesta.EtiquetasNormalizadas.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["area"] = "tecnologia" });
    }
}
