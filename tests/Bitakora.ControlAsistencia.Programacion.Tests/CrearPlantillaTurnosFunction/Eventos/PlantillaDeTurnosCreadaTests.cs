using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction.Eventos;

public class PlantillaDeTurnosCreadaTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001280");
    private const string NombreValido = "Ciclo 8 dias";

    [Fact]
    public void Crear_RetornaEvento_CuandoDatosSonValidos()
    {
        var evento = PlantillaDeTurnosCreada.Crear(PlantillaId, NombreValido, 8);

        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Nombre.Should().Be(NombreValido);
        evento.DiasDelCiclo.Should().Be(8);
    }

    [Fact]
    public void Crear_RetornaEvento_CuandoDiasDelCicloEsUno()
    {
        PlantillaDeTurnosCreada.Crear(PlantillaId, NombreValido, 1).DiasDelCiclo.Should().Be(1);
    }

    [Fact]
    public void Crear_RetornaEvento_CuandoDiasDelCicloEsCuarentaYDos()
    {
        PlantillaDeTurnosCreada.Crear(PlantillaId, NombreValido, 42).DiasDelCiclo.Should().Be(42);
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoDiasDelCicloEsCero()
    {
        var act = () => PlantillaDeTurnosCreada.Crear(PlantillaId, NombreValido, 0);

        act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions
            .Should().ContainSingle(e => e.Message.Contains(PlantillaDeTurnosCreada.Mensajes.DiasDelCicloFueraDeRango));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoDiasDelCicloEsCuarentaYTres()
    {
        var act = () => PlantillaDeTurnosCreada.Crear(PlantillaId, NombreValido, 43);

        act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions
            .Should().ContainSingle(e => e.Message.Contains(PlantillaDeTurnosCreada.Mensajes.DiasDelCicloFueraDeRango));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEsSoloEspacios()
    {
        var act = () => PlantillaDeTurnosCreada.Crear(PlantillaId, "   ", 8);

        act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions
            .Should().ContainSingle(e => e.Message.Contains(PlantillaDeTurnosCreada.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_AcumulaAmbosMensajes_CuandoNombreEnBlancoYDiasFueraDeRango()
    {
        var act = () => PlantillaDeTurnosCreada.Crear(PlantillaId, "   ", 43);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(PlantillaDeTurnosCreada.Mensajes.NombreVacio));
        ex.InnerExceptions.Should().Contain(e =>
            e.Message.Contains(PlantillaDeTurnosCreada.Mensajes.DiasDelCicloFueraDeRango));
    }
}
