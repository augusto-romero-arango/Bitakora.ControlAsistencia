// Invocacion DIRECTA de los metodos estaticos de la proyeccion, no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, que testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Oraculo independiente (MEF-ADR-0002): cada assert compara contra un valor armado a mano, sin
// reusar TokenizarNombre/NormalizarNumeroDocumento ni el centinela de produccion para construirlo.
//
// Ningun test cubre EtiquetaAsignada/EtiquetaRetirada ni el borrado: la clase companion simplemente
// no declara esos Apply ni ShouldDelete, garantia del compilador y no de un test.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Colaboradores;

public class DirectorioColaboradorProjectionTests
{
    // Literal propio del test: ningun test importa el centinela desde produccion (oraculo
    // independiente).
    private static readonly DateOnly CentinelaVigenciaAbierta = new(9999, 12, 31);

    private static Identificacion IdentificacionDePrueba() =>
        Identificacion.Crear(TipoIdentificacion.CC, "79879078");

    private static NombreColaborador NombreDePrueba() =>
        NombreColaborador.Crear("Juan", "Pablo", "Bermúdez", null);

    [Fact]
    public void Create_ProyectaIdentificacionNombreYTokens_DesdeColaboradorRegistrado()
    {
        var identificacion = IdentificacionDePrueba();
        var evento = new Event<ColaboradorRegistrado>(
            new ColaboradorRegistrado(identificacion, NombreDePrueba()))
        {
            StreamKey = identificacion.ToString(),
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = DirectorioColaboradorProjection.Create(evento);

        vista.Id.Should().Be("CC-79879078");
        vista.TipoDocumento.Should().Be("CC");
        vista.NumeroDocumento.Should().Be("79879078");
        vista.NombreCompleto.Should().Be("Juan Pablo Bermúdez");
        vista.TokensNombre.Should().BeEquivalentTo(["juan", "pablo", "bermudez"], o => o.WithStrictOrdering());
        vista.CodigoColaborador.Should().BeEmpty();
        vista.VigenteDesde.Should().Be(default(DateOnly));
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.CodigoSede.Should().BeNull();
    }

    [Fact]
    public void Apply_AsignaCodigoFechaInicioYSede_CuandoVinculacionIniciada()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            string.Empty, default, CentinelaVigenciaAbierta);
        var fechaInicio = new DateOnly(2026, 8, 1);
        var evento = new VinculacionIniciada("EMP-001", fechaInicio, "SEDE-BOG-01");

        var vista = DirectorioColaboradorProjection.Apply(evento, vistaPrevia);

        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(fechaInicio);
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.CodigoSede.Should().Be("SEDE-BOG-01");
        vista.Id.Should().Be("CC-79879078");
        vista.NombreCompleto.Should().Be("Juan Pablo Bermúdez");
    }

    [Fact]
    public void Apply_ReemplazaCodigoFechaInicioYReabreVigencia_CuandoVinculacionIniciadaEsUnReingreso()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), "SEDE-BOG-01");
        var nuevaFechaInicio = new DateOnly(2026, 3, 1);
        var evento = new VinculacionIniciada("EMP-002", nuevaFechaInicio, CodigoSede: null);

        var vista = DirectorioColaboradorProjection.Apply(evento, vistaPrevia);

        vista.CodigoColaborador.Should().Be("EMP-002");
        vista.VigenteDesde.Should().Be(nuevaFechaInicio);
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        // Reingreso nace limpio: null limpia la sede anterior, no la hereda.
        vista.CodigoSede.Should().BeNull();
    }

    [Fact]
    public void Apply_CierraLaVigencia_CuandoVinculacionTerminada()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta);
        var fechaEfectiva = new DateOnly(2026, 9, 30);

        var vista = DirectorioColaboradorProjection.Apply(new VinculacionTerminada(fechaEfectiva), vistaPrevia);

        vista.VigenteHasta.Should().Be(fechaEfectiva);
        vista.Id.Should().Be("CC-79879078");
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void Apply_ReabreLaVigencia_CuandoTerminacionAnulada()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30));

        var vista = DirectorioColaboradorProjection.Apply(new TerminacionAnulada(), vistaPrevia);

        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteDesde.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void Apply_ReemplazaVigenteDesde_CuandoFechaInicioVinculacionCorregida()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta);
        var fechaCorregida = new DateOnly(2026, 7, 15);

        var vista = DirectorioColaboradorProjection.Apply(
            new FechaInicioVinculacionCorregida(fechaCorregida), vistaPrevia);

        vista.VigenteDesde.Should().Be(fechaCorregida);
        vista.CodigoColaborador.Should().Be("EMP-001");
        vista.VigenteHasta.Should().Be(CentinelaVigenciaAbierta);
    }

    [Fact]
    public void Apply_ReemplazaNombreCompletoYRecalculaTokens_CuandoNombresCorregidos()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta);
        var nombreCorregido = NombreColaborador.Crear("Juan", "Pablo", "Bermudez", "Garcia");
        var evento = new NombresCorregidos(nombreCorregido);

        var vista = DirectorioColaboradorProjection.Apply(evento, vistaPrevia);

        vista.NombreCompleto.Should().Be("Juan Pablo Bermudez Garcia");
        vista.TokensNombre.Should().BeEquivalentTo(
            ["juan", "pablo", "bermudez", "garcia"], o => o.WithStrictOrdering());
        vista.Id.Should().Be("CC-79879078");
        vista.CodigoColaborador.Should().Be("EMP-001");
    }

    [Fact]
    public void Apply_AsignaCodigoSede_CuandoSedeAsignadaEsLaPrimeraAsignacion()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta);

        var vista = DirectorioColaboradorProjection.Apply(new SedeAsignada("SEDE-BOG-01"), vistaPrevia);

        vista.CodigoSede.Should().Be("SEDE-BOG-01");
        vista.Id.Should().Be("CC-79879078");
        vista.CodigoColaborador.Should().Be("EMP-001");
    }

    [Fact]
    public void Apply_ReemplazaCodigoSede_CuandoSedeAsignadaEsUnaReasignacion()
    {
        var vistaPrevia = new DirectorioColaborador(
            "CC-79879078", "CC", "79879078", "Juan Pablo Bermúdez", ["juan", "pablo", "bermudez"],
            "EMP-001", new DateOnly(2026, 8, 1), CentinelaVigenciaAbierta, CodigoSede: "SEDE-BOG-01");

        var vista = DirectorioColaboradorProjection.Apply(new SedeAsignada("SEDE-MED-02"), vistaPrevia);

        vista.CodigoSede.Should().Be("SEDE-MED-02");
    }
}
