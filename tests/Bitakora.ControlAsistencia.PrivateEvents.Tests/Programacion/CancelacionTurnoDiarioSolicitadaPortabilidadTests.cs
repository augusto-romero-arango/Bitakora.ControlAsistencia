// Issue #498 CA-4: CancelacionTurnoDiarioSolicitada es IPrivateEvent (cruza el ASB interno del BC).
// Todo su payload es plano (Guid, ResumenColaborador, DateOnly) -- portable por el serializador POR
// DEFECTO del bus, sin resolver custom. Vive en PrivateEvents.Tests (no en el proyecto de tests del
// dominio ni en el del futuro consumidor): este proyecto solo referencia PrivateEvents (MEF-ADR-0039
// decision 7), asi que el test replica exactamente lo que vera cualquier consumidor futuro.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Programacion;

public class CancelacionTurnoDiarioSolicitadaPortabilidadTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600f0-0000-7000-8000-000000000090");

    private static readonly ResumenColaborador Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static JsonSerializerOptions CrearOpcionesBus() => new(JsonSerializerDefaults.Web);

    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var evento = new CancelacionTurnoDiarioSolicitada(SolicitudId, Colaborador, Fecha);
        var opciones = CrearOpcionesBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<CancelacionTurnoDiarioSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.SolicitudId.Should().Be(evento.SolicitudId);
        restaurado.Colaborador.Should().Be(evento.Colaborador);
        restaurado.Fecha.Should().Be(evento.Fecha);
    }
}
