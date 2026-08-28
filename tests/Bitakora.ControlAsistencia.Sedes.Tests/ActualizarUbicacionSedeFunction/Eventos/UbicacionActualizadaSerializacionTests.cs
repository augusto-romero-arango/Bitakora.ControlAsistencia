// Issue #457. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (seccion 6d), nunca un
// resolver armado inline.
//
// UbicacionActualizada es un record plano (Ciudad?, Direccion?), sin VOs con ctor privado: no
// aplica el test "sin registro falla" -- un record con ctor publico se reconstruye igual con o sin
// resolver custom. Este archivo cubre unicamente el round-trip con datos completos y con ambos
// opcionales ausentes (CA-3).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction.Eventos;

public class UbicacionActualizadaSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    // CA-3: round-trip con Ciudad y Direccion presentes.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new UbicacionActualizada("Medellin", "Carrera 50 # 20-30");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<UbicacionActualizada>(json, opciones);

        restaurado.Should().Be(evento);
    }

    // CA-3: round-trip con ambos opcionales ausentes (Ciudad/Direccion null).
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConCiudadYDireccionNulas()
    {
        var evento = new UbicacionActualizada(null, null);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<UbicacionActualizada>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
