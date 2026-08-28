// Issue #457. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (seccion 6d), nunca un
// resolver armado inline.
//
// NombreSedeModificado es un record plano (Nombre), sin VOs con ctor privado: no aplica el test
// "sin registro falla" -- un record con ctor publico se reconstruye igual con o sin resolver
// custom.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction.Eventos;

public class NombreSedeModificadoSerializacionTests
{
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionSedes.CrearOpcionesMarten();

    // CA-1: round-trip con el nombre nuevo.
    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConElNombreNuevo()
    {
        var evento = new NombreSedeModificado("Sede Renombrada");
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<NombreSedeModificado>(json, opciones);

        restaurado.Should().Be(evento);
    }
}
