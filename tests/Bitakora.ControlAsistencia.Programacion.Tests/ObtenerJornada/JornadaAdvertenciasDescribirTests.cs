using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.ListarLimitesDeJornada;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class JornadaAdvertenciasDescribirTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001438");

    private static Jornada Crear() =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(0, 0), 1)));

    private const string DescripcionTope = "El tope diario (9:00) supera en 0:36 el de la modalidad Ordinaria (8:24)";

    [Fact]
    public void Describir_IncluyeAdvertenciaDelTope_CuandoLaModalidadEsOrdinariaYElTopeEsNueveHoras()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.Ordinaria));

        var respuesta = jornada.Describir();

        respuesta.Advertencias.Should().ContainSingle().Which.Should().Be(new AdvertenciaJornadaRespuesta(
            "SuperaTopeDiarioDeLaModalidad", new HorasYMinutosRespuesta(0, 36),
            new HorasYMinutosRespuesta(8, 24), DescripcionTope));
    }

    [Fact]
    public void Describir_RetornaAdvertenciasVacias_CuandoLaJornadaNoTieneModalidad()
    {
        var respuesta = Crear().Describir();

        respuesta.Advertencias.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Describir_RetornaAdvertenciasVacias_CuandoSeQuitoLaModalidad()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.Ordinaria));
        jornada.Apply(ModalidadDeJornadaQuitada.Crear(JornadaId));

        var respuesta = jornada.Describir();

        respuesta.Advertencias.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Describir_RetornaAdvertenciasVacias_CuandoLaModalidadEsExcluida()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.ExcluidaDeJornadaMaxima));

        var respuesta = jornada.Describir();

        respuesta.Advertencias.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void DesdeVista_IncluyeLasMismasAdvertencias_CuandoLaVistaTieneModalidadOrdinariaYTopeNueveHoras()
    {
        var vista = new LimitesDeJornada(JornadaId.ToString(), 2520, 540, 0, 1, "42 h", Modalidad: "Ordinaria");

        var respuesta = LimitesDeJornadaRespuesta.DesdeVista(vista);

        respuesta.Advertencias.Should().ContainSingle().Which.Should().Be(new AdvertenciaJornadaRespuesta(
            "SuperaTopeDiarioDeLaModalidad", new HorasYMinutosRespuesta(0, 36),
            new HorasYMinutosRespuesta(8, 24), DescripcionTope));
    }

    [Fact]
    public void DesdeVista_RetornaAdvertenciasVacias_CuandoLaVistaNoTieneModalidad()
    {
        var vista = new LimitesDeJornada(JornadaId.ToString(), 2520, 540, 0, 1, "42 h");

        var respuesta = LimitesDeJornadaRespuesta.DesdeVista(vista);

        respuesta.Advertencias.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Componer_IncluyeAdvertenciasEnCadaElemento_CuandoLaVistaTieneModalidadFlexibleSinMinimo()
    {
        var vista = new LimitesDeJornada(JornadaId.ToString(), 2520, 540, 0, 1, "42 h", Modalidad: "Flexible");

        var respuesta = ComposicionListadoLimites.Componer([vista], null, null, null);

        respuesta.Elementos.Single().Advertencias!.Select(a => a.Tipo)
            .Should().Equal("PorDebajoDelMinimoDiarioDeLaModalidad");
    }
}
