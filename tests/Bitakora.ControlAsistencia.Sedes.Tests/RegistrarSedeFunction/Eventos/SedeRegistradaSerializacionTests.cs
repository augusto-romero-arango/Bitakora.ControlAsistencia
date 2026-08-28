// Issue #456. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (seccion 6d), nunca un
// resolver armado inline.
//
// SedeRegistrada es un record plano (Codigo, Nombre, Ciudad?, Direccion?), sin VOs con ctor
// privado: no aplica el test "sin registro falla" que ColaboradorRegistradoSerializacionTests
// necesita para sus VOs -- un record con ctor publico se reconstruye igual con o sin resolver
// custom. Este archivo cubre unicamente el round-trip con datos completos y con los opcionales
// ausentes (CA-1/CA-2).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction.Eventos;

public class SedeRegistradaSerializacionTests
{
    // Usa las opciones REALES de Marten del dominio (regla 6d) -- no un resolver armado inline.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    // CA-1: round-trip con datos completos (Ciudad y Direccion presentes).
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new SedeRegistrada("SEDE-001", "Sede Principal", "Bogota", "Calle 100 # 10-20");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<SedeRegistrada>(json, opciones);

        restaurado.Should().Be(evento);
    }

    // CA-2: round-trip con los opcionales ausentes (Ciudad/Direccion null).
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCiudadYDireccionNulas()
    {
        var evento = new SedeRegistrada("SEDE-002", "Sede Secundaria", null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<SedeRegistrada>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
