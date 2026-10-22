using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoVentanaDeEntradaTests
{
    private static HorasYMinutos Horas(int h, int m = 0) => HorasYMinutos.Crear(h, m);

    private static FranjaOrdinaria Franja(int hi, int mi, int hf, int mf) =>
        FranjaOrdinaria.Crear(new TimeOnly(hi, mi), new TimeOnly(hf, mf));

    private static Turno Fijo() => Turno.Crear("Turno", false, [Franja(7, 0, 16, 0)]);

    [Fact]
    public void TieneVentanaDeEntrada_EsVerdaderoParaCero_CuandoElTurnoEsRecienCreado()
    {
        Fijo().TieneVentanaDeEntrada(Horas(0)).Should().BeTrue();
        Fijo().TieneVentanaDeEntrada(Horas(1)).Should().BeFalse();
    }

    [Fact]
    public void ConVentanaDeEntrada_ConservaLaAmplitud_CuandoSeTransformaElTurno()
    {
        var turno = Fijo().ConVentanaDeEntrada(Horas(3));

        turno.ConFranja(Franja(18, 0, 20, 0)).TieneVentanaDeEntrada(Horas(3)).Should().BeTrue();
        turno.SinFranjaQueEmpiezaA(Franja(7, 0, 16, 0)).TieneVentanaDeEntrada(Horas(3)).Should().BeTrue();
        turno.ConFranjaReemplazada(Franja(7, 0, 15, 0)).TieneVentanaDeEntrada(Horas(3)).Should().BeTrue();
    }

    [Fact]
    public void ToString_NoCambia_CuandoLaAmplitudEsMayorACero()
    {
        var turno = Fijo();

        turno.ConVentanaDeEntrada(Horas(3)).ToString().Should().Be(turno.ToString());
    }

    [Fact]
    public void Equals_DistingueTurnos_CuandoSoloDifierenEnLaAmplitud()
    {
        var conVentana = Fijo().ConVentanaDeEntrada(Horas(3));

        conVentana.Equals(Fijo()).Should().BeFalse();
        conVentana.Should().Be(Fijo().ConVentanaDeEntrada(Horas(3)));
        conVentana.GetHashCode().Should().Be(Fijo().ConVentanaDeEntrada(Horas(3)).GetHashCode());
    }

    [Fact]
    public void EvaluarVentanaDeEntrada_EsValida_CuandoLaAmplitudEsCeroEnCualquierTurno()
    {
        Fijo().EvaluarVentanaDeEntrada(Horas(0)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
        Turno.Crear("Libre", true, []).EvaluarVentanaDeEntrada(Horas(0)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
        Turno.Crear("Nuevo", false, []).EvaluarVentanaDeEntrada(Horas(0)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
    }

    [Fact]
    public void EvaluarVentanaDeEntrada_RetornaTurnoSinFranjas_CuandoEsDescansoOIncompletoConAmplitud()
    {
        Turno.Crear("Libre", true, []).EvaluarVentanaDeEntrada(Horas(1)).Should().Be(EvaluacionVentanaDeEntrada.TurnoSinFranjas);
        Turno.Crear("Nuevo", false, []).EvaluarVentanaDeEntrada(Horas(1)).Should().Be(EvaluacionVentanaDeEntrada.TurnoSinFranjas);
    }

    [Fact]
    public void EvaluarVentanaDeEntrada_EsValida_CuandoLaPrimeraFranjaEmpiezaDeMadrugada()
    {
        var turno = Turno.Crear("Madrugada", false, [Franja(3, 30, 12, 0)]);

        turno.EvaluarVentanaDeEntrada(Horas(1)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
    }

    [Fact]
    public void EvaluarVentanaDeEntrada_RetornaCierrePasaMedianoche_CuandoElTurnoEmpiezaDeMadrugadaYLaUltimaFranjaLlegaALas2400()
    {
        var turno = Turno.Crear("Madrugada partido", false, [Franja(3, 0, 11, 0), Franja(21, 0, 23, 0)]);

        turno.EvaluarVentanaDeEntrada(Horas(3)).Should().Be(EvaluacionVentanaDeEntrada.CierrePasaMedianoche);
        turno.EvaluarVentanaDeEntrada(Horas(2, 59)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
    }

    [Fact]
    public void EvaluarVentanaDeEntrada_RetornaCierrePasaMedianoche_CuandoLaUltimaFranjaMasLaAmplitudLlegaALas2400()
    {
        var turno = Turno.Crear("Partido", false, [Franja(6, 0, 10, 0), Franja(20, 0, 23, 0)]);

        turno.EvaluarVentanaDeEntrada(Horas(4)).Should().Be(EvaluacionVentanaDeEntrada.CierrePasaMedianoche);
        turno.EvaluarVentanaDeEntrada(Horas(3, 59)).Should().Be(EvaluacionVentanaDeEntrada.Valida);
    }

    [Fact]
    public void RoundTrip_ConservaLaAmplitud_CuandoElTurnoTieneVentana()
    {
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();
        var turno = Fijo().ConVentanaDeEntrada(Horas(3, 15));

        var json = JsonSerializer.Serialize(turno, opciones);
        var restaurado = JsonSerializer.Deserialize<Turno>(json, opciones)!;

        json.Should().Contain("amplitudVentanaEntrada");
        restaurado.Should().Be(turno);
        restaurado.TieneVentanaDeEntrada(Horas(3, 15)).Should().BeTrue();
    }

    [Fact]
    public void Deserializar_LeeAmplitudCero_CuandoLaCopiaNoTraeLaClave()
    {
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();
        var nodo = JsonNode.Parse(JsonSerializer.Serialize(Fijo(), opciones))!.AsObject();
        nodo.Remove("amplitudVentanaEntrada");

        var restaurado = JsonSerializer.Deserialize<Turno>(nodo.ToJsonString(), opciones)!;

        restaurado.TieneVentanaDeEntrada(Horas(0)).Should().BeTrue();
        restaurado.Should().Be(Fijo());
    }
}
