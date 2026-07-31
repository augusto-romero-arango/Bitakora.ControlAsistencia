// HU-129: Crear estructuras agregadas DesgloseFranja y DesgloseHoras
// CA-5: DesgloseFranja sobrevive round-trip JSON con ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten()
//       preservando Programada, Intervalos, Retardo y recalculando MinutosPorConcepto con los mismos valores.
// Barrera anti-regresion: sin el registro de Retardo en el resolver, la deserializacion falla.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class DesgloseFranjaSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionCalculoHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    private static DetalleFranjaOrdinaria CrearFranjaProgramadaConDescanso() =>
        new DetalleFranjaOrdinaria(
            new TimeOnly(8, 0), new TimeOnly(17, 0), 0,
            [new DetalleSubFranja(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0)],
            []);

    private static DetalleFranjaOrdinaria CrearFranjaProgramadaSimple() =>
        new DetalleFranjaOrdinaria(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], []);

    [Fact]
    public void RoundTrip_PreservaProgramada_CuandoFranjaConDescanso()
    {
        var programada = CrearFranjaProgramadaConDescanso();
        var original = new DesgloseFranja(
            programada,
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseFranja>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Programada.Should().Be(programada);
    }

    [Fact]
    public void RoundTrip_PreservaIntervalos_CuandoVariosConceptos()
    {
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(8, 0)), Concepto.OrdinariaNocturna),
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var original = new DesgloseFranja(CrearFranjaProgramadaSimple(), intervalos, Retardo.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseFranja>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Intervalos.Should().HaveCount(2);
        restaurado.Intervalos[0].Concepto.Should().Be(Concepto.OrdinariaNocturna);
        restaurado.Intervalos[1].Concepto.Should().Be(Concepto.OrdinariaDiurna);
    }

    [Fact]
    public void RoundTrip_PreservaRetardo_CuandoConCompensacionParcial()
    {
        // 30 min retardados - 20 min compensados = 10 min neto
        var retardo = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))]);
        var original = new DesgloseFranja(
            CrearFranjaProgramadaSimple(),
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 30), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)],
            retardo);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseFranja>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Retardo.Should().Be(retardo);
        restaurado.Retardo.RetardoNeto.Should().Be(10);
    }

    [Fact]
    public void RoundTrip_PreservaRetardoVacio_CuandoSinRetardo()
    {
        var original = new DesgloseFranja(
            CrearFranjaProgramadaSimple(),
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)],
            Retardo.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseFranja>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Retardo.Should().Be(Retardo.Vacio);
        restaurado.Retardo.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void RoundTrip_RecalculaMinutosPorConcepto_TrasDeserializar()
    {
        // 240 + 240 min OrdinariaDiurna: MinutosPorConcepto debe devolver 480 tras deserializar
        var intervalos = new List<IntervaloClasificado>
        {
            new(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna),
            new(CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(17, 0)), Concepto.OrdinariaDiurna)
        };
        var original = new DesgloseFranja(CrearFranjaProgramadaSimple(), intervalos, Retardo.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DesgloseFranja>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.MinutosPorConcepto[Concepto.OrdinariaDiurna].Should().Be(480);
    }

    // Barrera anti-regresion: DesgloseFranja.Retardo (Retardo) no sobrevive sin
    // ConfigurarSerializacion registrado. Si alguien borra Retardo.ConfigurarSerializacion(resolver)
    // de ConfigurarResolver, este test falla.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeRetardo()
    {
        var original = new DesgloseFranja(
            CrearFranjaProgramadaSimple(),
            [new IntervaloClasificado(CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna)],
            Retardo.Crear(
                [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
                []));
        var opcionesCompletas = CrearOpciones();
        var json = JsonSerializer.Serialize(original, opcionesCompletas);

        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opcionesVacias = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };

        var act = () => JsonSerializer.Deserialize<DesgloseFranja>(json, opcionesVacias);

        act.Should().Throw<NotSupportedException>();
    }
}
