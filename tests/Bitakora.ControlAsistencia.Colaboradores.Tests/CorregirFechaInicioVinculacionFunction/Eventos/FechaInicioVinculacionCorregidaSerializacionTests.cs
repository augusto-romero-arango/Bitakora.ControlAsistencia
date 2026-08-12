// Issue #352. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d). Sin marker de
// bus (issue #352 "Consumidores: ninguno") -- no aplica el test de portabilidad de la regla 21
// (seccion 6e).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirFechaInicioVinculacionFunction.Eventos;

/// <summary>
/// Verifica que FechaInicioVinculacionCorregida (payload plano: DateOnly, sin VOs anidados)
/// sobrevive un roundtrip de serializacion STJ con las opciones reales de Marten del dominio.
/// Igual que VinculacionTerminada/VinculacionIniciada, este evento NO necesita
/// ConfigurarSerializacion propio (ctor publico, tipo primitivo) -- no hay un test "sin registro
/// falla": no hay ningun registro que proteger.
/// </summary>
public class FechaInicioVinculacionCorregidaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-1: FechaInicioVinculacionCorregida persiste FechaInicio tal como llego (sin default del
    // servidor) y sobrevive el roundtrip completo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new FechaInicioVinculacionCorregida(new DateOnly(2026, 1, 10));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FechaInicioVinculacionCorregida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaInicio.Should().Be(new DateOnly(2026, 1, 10));
    }

    // CA-1 (variante): una fecha corregida distante en el pasado sobrevive el roundtrip igual.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConFechaDistanteEnElPasado()
    {
        var evento = new FechaInicioVinculacionCorregida(new DateOnly(2015, 3, 1));
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<FechaInicioVinculacionCorregida>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.FechaInicio.Should().Be(new DateOnly(2015, 3, 1));
    }
}
