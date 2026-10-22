using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado.Eventos;

public class PertenenciaSerializacionTests
{
    private const string StreamId = "dc:EMP-001:20260315";
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly MarcacionDelDia Marcacion =
        new(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua");

    private static HorasDiscriminadas Horas() => new(new Dictionary<string, decimal>(), []);

    [Fact]
    public void Deserializar_ConservaLaPertenencia_EnDepuracionDiaRecibida()
    {
        var evento = new DepuracionDiaRecibida(StreamId, "EMP-001", Fecha, null, "Turno", [], [Marcacion], Horas());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado!.Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().Be("Ambigua");
    }

    [Fact]
    public void Desde_CopiaLaPertenencia_EnDepuracionPosAprobacionRecibida()
    {
        var foto = new DepuracionDiaRecibida(StreamId, "EMP-001", Fecha, null, "Turno", [], [Marcacion], Horas());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var copia = DepuracionPosAprobacionRecibida.Desde(foto);
        var json = JsonSerializer.Serialize(copia, opciones);
        var deserializado = JsonSerializer.Deserialize<DepuracionPosAprobacionRecibida>(json, opciones);

        deserializado!.Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().Be("Ambigua");
    }

    [Fact]
    public void Deserializar_DejaPertenenciaNula_CuandoElJsonLegadoNoTraeLaClave()
    {
        var evento = new DepuracionDiaRecibida(
            StreamId, "EMP-001", Fecha, null, "Turno", [],
            [new MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA")], Horas());
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var json = JsonSerializer.Serialize(evento, opciones).Replace(",\"Pertenencia\":null", "");

        var deserializado = JsonSerializer.Deserialize<DepuracionDiaRecibida>(json, opciones);

        deserializado!.Marcaciones.Should().ContainSingle().Which.Pertenencia.Should().BeNull();
    }
}
