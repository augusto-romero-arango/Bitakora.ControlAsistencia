using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction.Eventos;

public class PlantillaSemanalCreadaTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000620");
    private const string NombreValido = "Semana Cocina";

    [Fact]
    public void Crear_RetornaPlantillaSemanalCreada_CuandoDatosSonValidos()
    {
        var evento = PlantillaSemanalCreada.Crear(PlantillaId, NombreValido, 2);

        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Nombre.Should().Be(NombreValido);
        evento.Semanas.Should().Be(2);
    }

    [Fact]
    public void Crear_RetornaPlantillaSemanalCreada_CuandoSemanasEsUno()
    {
        var evento = PlantillaSemanalCreada.Crear(PlantillaId, NombreValido, 1);

        evento.Semanas.Should().Be(1);
    }

    [Fact]
    public void Crear_RetornaPlantillaSemanalCreada_CuandoSemanasEsSeis()
    {
        var evento = PlantillaSemanalCreada.Crear(PlantillaId, NombreValido, PlantillaSemanalCreada.MaximoSemanas);

        evento.Semanas.Should().Be(6);
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEstaVacio()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, "", 2);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(PlantillaSemanalCreada.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEsSoloEspaciosEnBlanco()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, "   ", 2);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(PlantillaSemanalCreada.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoSemanasEsCero()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, NombreValido, 0);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(PlantillaSemanalCreada.Mensajes.SemanasFueraDeRango));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoSemanasEsSiete()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, NombreValido, 7);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(PlantillaSemanalCreada.Mensajes.SemanasFueraDeRango));
    }

    [Fact]
    public void Crear_AcumulaAmbosMensajes_CuandoNombreEsEnBlancoYSemanasEsCero()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, "", 0);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(PlantillaSemanalCreada.Mensajes.NombreVacio));
        ex.InnerExceptions.Should().Contain(e =>
            e.Message.Contains(PlantillaSemanalCreada.Mensajes.SemanasFueraDeRango));
    }

    [Fact]
    public void Crear_SoloLanzaArgumentExceptions_CuandoHayErroresDeValidacion()
    {
        var act = () => PlantillaSemanalCreada.Crear(PlantillaId, "", 0);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().AllBeAssignableTo<ArgumentException>();
    }
}
