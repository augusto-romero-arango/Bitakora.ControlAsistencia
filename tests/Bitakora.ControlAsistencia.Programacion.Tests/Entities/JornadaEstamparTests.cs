using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class JornadaEstamparTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000862");

    // Issue #861 CA-1: la copia la entrega el aggregate (Tell-don't-Ask).
    [Fact]
    public void Estampar_DevuelveLaCopiaConIdYLimites_CuandoLaJornadaFueCreada()
    {
        var jornada = Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));

        var copia = jornada.Estampar();

        copia.Should().Be(new JornadaProgramada(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
    }

    [Fact]
    public void Estampar_DevuelveLosLimitesVigentes_CuandoLaJornadaFueModificada()
    {
        var jornada = Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
        jornada.ModificarLimites(LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1));

        var copia = jornada.Estampar();

        copia.Should().Be(new JornadaProgramada(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));
    }

    private static LimitesJornada Limites() => LimitesJornada.Crear(
        HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1);

    private static JornadaProgramada EstamparCon(ModalidadJornada? modalidad, bool descansoAcumulado = false) =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, Limites()), modalidad, descansoAcumulado).Estampar();

    [Fact]
    public void Estampar_CopiaLaFichaDeOrdinaria_CuandoLaJornadaTieneModalidadOrdinaria()
    {
        var copia = EstamparCon(ModalidadJornada.Ordinaria);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), ModalidadJornada.Ordinaria, false,
            new FichaDeLey(504, null, 2520)));
    }

    [Fact]
    public void Estampar_CopiaLaFichaDeFlexible_CuandoLaJornadaTieneModalidadFlexible()
    {
        var copia = EstamparCon(ModalidadJornada.Flexible);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), ModalidadJornada.Flexible, false,
            new FichaDeLey(540, 240, 2520)));
    }

    [Fact]
    public void Estampar_CopiaLaFichaDeTurnosSucesivos_CuandoLaJornadaTieneEsaModalidad()
    {
        var copia = EstamparCon(ModalidadJornada.TurnosSucesivos);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), ModalidadJornada.TurnosSucesivos, false,
            new FichaDeLey(360, null, 2160)));
    }

    [Fact]
    public void Estampar_CopiaLaFichaDeSinSolucionDeContinuidad_CuandoLaJornadaTieneEsaModalidad()
    {
        var copia = EstamparCon(ModalidadJornada.SinSolucionDeContinuidad);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), ModalidadJornada.SinSolucionDeContinuidad, false,
            new FichaDeLey(null, null, 3360)));
    }

    [Fact]
    public void Estampar_CopiaFichaTodaNula_CuandoLaModalidadEsExcluidaDeJornadaMaxima()
    {
        var copia = EstamparCon(ModalidadJornada.ExcluidaDeJornadaMaxima);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), ModalidadJornada.ExcluidaDeJornadaMaxima, false,
            new FichaDeLey(null, null, null)));
    }

    [Fact]
    public void Estampar_CopiaLaMarcaYDejaModalidadYFichaNulas_CuandoLaJornadaNoTieneModalidad()
    {
        var copia = EstamparCon(null, descansoAcumulado: true);

        copia.Should().Be(new JornadaProgramada(JornadaId, Limites(), null, true, null));
    }
}
