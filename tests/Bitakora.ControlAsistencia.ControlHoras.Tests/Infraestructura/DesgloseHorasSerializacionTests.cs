// HU-129: Crear estructuras agregadas DesgloseFranja y DesgloseHoras
// CA-6: DesgloseHoras sobrevive round-trip JSON con ConfiguracionSerializacionControlHoras.CrearOpcionesMarten()
//       preservando DesglosePorFranja, RetardoTotal, FranjasAnomalas y recalculando TotalMinutosPorConcepto.
// Barrera anti-regresion: sin el registro de Retardo en el resolver, la deserializacion falla.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class DesgloseHorasSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    private static DetalleFranjaOrdinaria CrearFranjaProgramadaSimple() =>
        new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], []);

    private static DesgloseFranja CrearFranjaDiurna() =>
        new DesgloseFranja(
            CrearFranjaProgramadaSimple(),
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);

    private static DesgloseFranja CrearFranjaConRetardo() =>
        new DesgloseFranja(
            CrearFranjaProgramadaSimple(),
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 30), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)],
            Retardo.Crear(
                [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
                []));

    [Fact]
    public void RoundTrip_PreservaDesglosePorFranja_CuandoVariasFranjas()
    {
        var original = new DesgloseHoras(
            [CrearFranjaDiurna(), CrearFranjaDiurna()],
            Retardo.Vacio,
            0);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DesglosePorFranja.Should().HaveCount(2);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoTotal_CuandoConRetardo()
    {
        var retardoTotal = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            []);
        var original = new DesgloseHoras(
            [CrearFranjaDiurna()],
            retardoTotal,
            0);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.RetardoTotal.Should().Be(retardoTotal);
        restaurado.RetardoTotal.RetardoNeto.Should().Be(30);
    }

    [Fact]
    public void RoundTrip_PreservaFranjasAnomalas_CuandoConAnomalas()
    {
        var original = new DesgloseHoras(
            [CrearFranjaDiurna()],
            Retardo.Vacio,
            FranjasAnomalas: 2);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.FranjasAnomalas.Should().Be(2);
    }

    [Fact]
    public void RoundTrip_PreservaFranjasAnomalasEnCero_CuandoSinAnomalas()
    {
        var original = new DesgloseHoras(
            [CrearFranjaDiurna()],
            Retardo.Vacio,
            0);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.FranjasAnomalas.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_RecalculaTotalMinutosPorConcepto_TrasDeserializar()
    {
        // Dos franjas diurnas de 540 min cada una: total 1080 min OrdinariaDiurna
        var original = new DesgloseHoras(
            [CrearFranjaDiurna(), CrearFranjaDiurna()],
            Retardo.Vacio,
            0);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.TotalMinutosPorConcepto[Concepto.OrdinariaDiurna].Should().Be(1080);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoDeFranjaInterna_CuandoDesgloseTieneFranjaConRetardo()
    {
        // Verifica que el Retardo dentro de cada DesgloseFranja se preserva correctamente
        // tras anidar DesgloseFranja > DesgloseHoras y atravesar dos niveles de serializacion.
        var franja = CrearFranjaConRetardo();
        var original = new DesgloseHoras([franja], Retardo.Vacio, 0);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseHoras>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DesglosePorFranja[0].Retardo.RetardoNeto.Should().Be(30);
    }

    // Barrera anti-regresion: DesgloseHoras.RetardoTotal (Retardo) no sobrevive sin
    // ConfigurarSerializacion registrado. Si alguien borra Retardo.ConfigurarSerializacion(resolver)
    // de ConfigurarResolver, este test falla.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeRetardo()
    {
        var original = new DesgloseHoras(
            [CrearFranjaDiurna()],
            Retardo.Crear(
                [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
                []),
            0);
        var opcionesCompletas = CrearOpciones();
        var json = JsonSerializer.Serialize(original, opcionesCompletas);

        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opcionesVacias = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };

        var act = () => JsonSerializer.Deserialize<DesgloseHoras>(json, opcionesVacias);

        act.Should().Throw<NotSupportedException>();
    }
}
