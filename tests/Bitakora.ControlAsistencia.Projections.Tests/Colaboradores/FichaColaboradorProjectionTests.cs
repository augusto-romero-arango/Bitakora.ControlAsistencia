// Issue #356: primera proyeccion concreta del dominio Colaboradores. Invocacion
// DIRECTA de los metodos estaticos de FichaColaboradorProjection (N1, MEF-ADR-0035) -- no el DSL
// Given/When/Then de CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event
// store): aqui se testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply bajo prueba para construir el valor esperado -- ni siquiera el
// centinela de vigencia abierta se referencia desde produccion, se escribe el literal
// new DateOnly(9999, 12, 31) en cada test.
//
// Dos ejes cohesivos (issue #356, "Nota de complejidad"): CA-1..CA-5 son variaciones del mismo eje
// (materializacion Create/Apply); CA-6 (el endpoint) no se prueba aqui -- ver el test de
// composicion en Colaboradores.Tests.
//
// Dato de vistaPrevia en los tests de Apply: se construye a mano, simulando el documento que Marten
// ya habria materializado -- nunca se obtiene invocando Create/Apply, para no encadenar el oraculo
// de un test con la logica bajo prueba de otro.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class FichaColaboradorProjectionTests
{
    // Centinela de vigencia abierta (issue #356, "Vista a materializar"): estructura interna de
    // filtrado, jamas expuesta por la API (eso es CA-6, responsabilidad del endpoint). Se repite
    // como literal en cada test -- ninguno lo importa desde produccion (oraculo independiente).
    private static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    private static Identificacion IdentificacionDePrueba() =>
        Identificacion.Crear(TipoIdentificacion.CC, "123456");

    private static NombreColaborador NombreDePrueba() =>
        NombreColaborador.Crear("Ana", null, "Ramirez", null);

    // --- CA-1 (primera mitad): Create nace la ficha con Id y NombreCompleto ---

    // Create toma IEvent<ColaboradorRegistrado>, no el evento a secas: la identidad del documento
    // es el StreamKey del stream de ColaboradorAggregateRoot ("CC-123456"), nunca recomputada a
    // mano desde el payload (skills/projections/modelos-marten.md, ejemplo SeguimientoTurnoProjection).
    [Fact]
    public void Create_ProyectaIdYNombreCompleto_DesdeColaboradorRegistrado()
    {
        var identificacion = IdentificacionDePrueba();
        var evento = new Event<ColaboradorRegistrado>(
            new ColaboradorRegistrado(identificacion, NombreDePrueba()))
        {
            StreamKey = identificacion.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaColaboradorProjection.Create(evento);

        vista.Id.Should().Be("CC-123456");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
    }

    // --- CA-1 (segunda mitad): Apply(VinculacionIniciada) completa codigo, fechas y deja las
    // etiquetas vacias sobre la ficha recien nacida (mismo commit que ColaboradorRegistrado) ---

    [Fact]
    public void Apply_AsignaCodigoYFechaInicioYAbreLaVigencia_CuandoVinculacionIniciada()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", string.Empty, default, CentinelaVigenciaAbierta,
            [], new Dictionary<string, string>());
        var fechaInicio = new DateOnly(2026, 8, 1);
        var evento = new VinculacionIniciada("EMP-001", fechaInicio);

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.Id.Should().Be("CC-123456");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(fechaInicio);
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.Etiquetas.Should().BeEmpty();
        vista.EtiquetasNormalizadas.Should().BeEmpty();
    }

    // --- CA-2 (primera mitad): VinculacionTerminada cierra la vigencia ---

    [Fact]
    public void Apply_CierraLaVigencia_CuandoVinculacionTerminada()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [], new Dictionary<string, string>());
        var fechaEfectiva = new DateOnly(2026, 9, 30);
        var evento = new VinculacionTerminada(fechaEfectiva);

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.VigenteHasta.Should().Be(fechaEfectiva);
        // El resto de la ficha (identidad, codigo y fecha de inicio) no cambia con la terminacion.
        vista.Id.Should().Be("CC-123456");
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
    }

    // --- CA-2 (segunda mitad): TerminacionAnulada reabre -- vuelve al centinela ---

    [Fact]
    public void Apply_ReabreLaVigencia_CuandoTerminacionAnulada()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30),
            [], new Dictionary<string, string>());
        var evento = new TerminacionAnulada();

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
    }

    // --- CA-3 (primera mitad): NombresCorregidos reemplaza NombreCompleto ---

    [Fact]
    public void Apply_ReemplazaElNombreCompleto_CuandoNombresCorregidos()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [], new Dictionary<string, string>());
        var nombreCorregido = NombreColaborador.Crear("Ana Maria", null, "Ramirez", "Solano");
        var evento = new NombresCorregidos(nombreCorregido);

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.NombreCompleto.Should().Be("Ana Maria Ramirez Solano");
        vista.Id.Should().Be("CC-123456");
        vista.CodigoColaborador.Should().Be("EMP-001");
    }

    // --- CA-3 (segunda mitad): FechaInicioVinculacionCorregida reemplaza VigenteDesde ---

    [Fact]
    public void Apply_ReemplazaVigenteDesde_CuandoFechaInicioVinculacionCorregida()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [], new Dictionary<string, string>());
        var fechaCorregida = new DateOnly(2026, 7, 15);
        var evento = new FechaInicioVinculacionCorregida(fechaCorregida);

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.VigenteDesde.Should().Be(fechaCorregida);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
    }

    // --- CA-4 (primera mitad): EtiquetaAsignada agrega en AMBAS estructuras, sin tocar categorias
    // existentes (oraculo mas fuerte que "agregar sobre lista vacia": descarta una implementacion
    // que sobrescriba todo el listado en vez de agregar) ---

    [Fact]
    public void Apply_AgregaLaEtiquetaEnAmbasEstructuras_CuandoEtiquetaAsignadaEsUnaCategoriaNueva()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [new EtiquetaFicha("sede", "Suba")],
            new Dictionary<string, string> { ["sede"] = "suba" });
        var evento = new EtiquetaAsignada(Etiqueta.Crear("Área", "Tecnología"));

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.Etiquetas.Should().BeEquivalentTo(
        [
            new EtiquetaFicha("sede", "Suba"),
            new EtiquetaFicha("Área", "Tecnología")
        ]);
        vista.EtiquetasNormalizadas.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["sede"] = "suba",
            ["area"] = "tecnologia"
        });
    }

    // --- CA-4 (segunda mitad): upsert -- reasignar la MISMA categoria normalizada (distinta forma
    // original, "area" en vez de "Área") deja UNA sola entrada con el valor nuevo, no dos ---

    [Fact]
    public void Apply_SobrescribeElValorDeLaCategoria_CuandoEtiquetaAsignadaReutilizaLaMismaCategoriaNormalizada()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [new EtiquetaFicha("Área", "Tecnología")],
            new Dictionary<string, string> { ["area"] = "tecnologia" });
        var evento = new EtiquetaAsignada(Etiqueta.Crear("area", "Sistemas"));

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.Etiquetas.Should().BeEquivalentTo([new EtiquetaFicha("area", "Sistemas")]);
        vista.EtiquetasNormalizadas.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["area"] = "sistemas" });
    }

    // --- CA-4 (tercera mitad): EtiquetaRetirada remueve SOLO la categoria indicada de ambas
    // estructuras, dejando intactas las demas ---

    [Fact]
    public void Apply_RemueveSoloLaCategoriaIndicada_CuandoEtiquetaRetirada()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta,
            [new EtiquetaFicha("area", "Sistemas"), new EtiquetaFicha("sede", "Suba")],
            new Dictionary<string, string> { ["area"] = "sistemas", ["sede"] = "suba" });
        var evento = new EtiquetaRetirada("area");

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.Etiquetas.Should().BeEquivalentTo([new EtiquetaFicha("sede", "Suba")]);
        vista.EtiquetasNormalizadas.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["sede"] = "suba" });
    }

    // --- CA-5: reingreso nace limpio -- un colaborador terminado, CON etiquetas, que reingresa
    // (VinculacionIniciada de nuevo sobre el mismo stream) recibe codigo y VigenteDesde nuevos,
    // VigenteHasta vuelve al centinela y AMBAS estructuras de etiquetas se vacian. Espejo de
    // Apply(VinculacionIniciada) en ColaboradorAggregateRoot (#355 CA-6). ---

    [Fact]
    public void Apply_NaceLimpio_CuandoVinculacionIniciadaEsUnReingresoConEtiquetasPrevias()
    {
        var vistaPrevia = new FichaColaborador(
            "CC-123456", "Ana Ramirez", "EMP-001", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            [new EtiquetaFicha("area", "Sistemas")],
            new Dictionary<string, string> { ["area"] = "sistemas" });
        var nuevaFechaInicio = new DateOnly(2026, 3, 1);
        var evento = new VinculacionIniciada("EMP-002", nuevaFechaInicio);

        var vista = FichaColaboradorProjection.Apply(evento, vistaPrevia);

        vista.Id.Should().Be("CC-123456");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.CodigoColaborador.Should().Be("EMP-002");
        vista.VigenteDesde.Should().Be(nuevaFechaInicio);
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.Etiquetas.Should().BeEmpty();
        vista.EtiquetasNormalizadas.Should().BeEmpty();
    }
}
