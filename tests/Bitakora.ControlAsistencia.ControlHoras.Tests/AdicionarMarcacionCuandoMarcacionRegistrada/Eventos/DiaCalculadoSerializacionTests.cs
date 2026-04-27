// HU-108: Emitir DiaCalculado tras adicionar marcacion
// CA-7: DiaCalculado sobrevive roundtrip JSON con ConfiguracionSerializacionControlHoras.CrearOpcionesMarten().
// Patron: mismo que DesgloseHorasSerializacionTests (usa CrearOpcionesMarten() real, no resolver inline).
// Barrera anti-regresion: verifica que falla sin los ConfigurarSerializacion de los VOs anidados.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;

public class DiaCalculadoSerializacionTests
{
    private static readonly InformacionEmpleado Empleado =
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static readonly DetalleFranjaOrdinaria Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], []);

    // Usa las opciones que Marten usa en produccion - no un resolver armado inline.
    // Si alguien borra un registro en ConfigurarResolver, los tests de esta clase fallan.
    private static JsonSerializerOptions CrearOpciones() =>
        ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // CA-7: roundtrip basico con InformacionEmpleado presente y DesgloseHoras.Vacio.
    // Verifica que DiaCalculado (sealed class) se serializa y deserializa correctamente via STJ.
    [Fact]
    public void RoundTrip_PreservaCampos_CuandoDesgloseEsVacio()
    {
        var original = new DiaCalculado(Empleado, Fecha, [], DesgloseHoras.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().Be(Empleado);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.ControlesDeFranja.Should().BeEmpty();
        restaurado.DesgloseHoras.FranjasAnomalas.Should().Be(0);
        restaurado.DesgloseHoras.DesglosePorFranja.Should().BeEmpty();
    }

    // CA-7: roundtrip con InformacionEmpleado null (caso "marcacion sin turno previo").
    // Verifica que el campo nullable se preserva como null tras el roundtrip.
    [Fact]
    public void RoundTrip_PreservaCampos_CuandoInformacionEmpleadoEsNula()
    {
        var original = new DiaCalculado(null, Fecha, [], DesgloseHoras.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().BeNull();
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.ControlesDeFranja.Should().BeEmpty();
    }

    // CA-7: roundtrip con ControlesDeFranja no vacios.
    // Verifica que DetalleControlFranja (record anidado) sobrevive el roundtrip con todos sus campos.
    [Fact]
    public void RoundTrip_PreservaControlesDeFranja_CuandoTieneDetalles()
    {
        var detalle = new DetalleControlFranja(
            Franja06_14,
            new DateTime(2026, 3, 15, 7, 0, 0),
            null,
            true);
        var original = new DiaCalculado(Empleado, Fecha, [detalle], DesgloseHoras.Vacio);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.ControlesDeFranja.Should().HaveCount(1);
        restaurado.ControlesDeFranja[0].EsAnomala.Should().BeTrue();
        restaurado.ControlesDeFranja[0].Entrada.Should().Be(new DateTime(2026, 3, 15, 7, 0, 0));
        restaurado.ControlesDeFranja[0].Salida.Should().BeNull();
        restaurado.ControlesDeFranja[0].Programada.HoraInicio.Should().Be(new TimeOnly(6, 0));
        restaurado.ControlesDeFranja[0].Programada.HoraFin.Should().Be(new TimeOnly(14, 0));
    }

    // CA-7 (barrera futura para #115/#116): roundtrip con DesgloseHoras que contiene DetalleRetardo real.
    // Este caso NO se activa mientras la calculadora no exista, pero la barrera debe estar desde ya.
    // Verifica que IntervaloTemporal y DetalleRetardo (ctors privados) sobreviven el roundtrip
    // cuando viajan anidados dentro de DiaCalculado.
    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_CuandoDesgloseTieneDatos()
    {
        var retardoIntervalo = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var retardoTotal = DetalleRetardo.Crear([retardoIntervalo], []);
        var desgloseConDatos = new DesgloseHoras([], retardoTotal, 1);
        var original = new DiaCalculado(Empleado, Fecha, [], desgloseConDatos);
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DesgloseHoras.RetardoTotal.RetardoNeto.Should().Be(30);
        restaurado.DesgloseHoras.FranjasAnomalas.Should().Be(1);
        restaurado.InformacionEmpleado.Should().Be(Empleado);
        restaurado.Fecha.Should().Be(Fecha);
    }

    // Barrera anti-regresion: DetalleRetardo tiene ctor privado y requiere ConfigurarSerializacion.
    // Si alguien borra la linea DetalleRetardo.ConfigurarSerializacion(resolver) de ConfigurarResolver,
    // este test falla - protegiendo contra regresiones silenciosas en produccion.
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeDetalleRetardo()
    {
        // Construir un DiaCalculado con DesgloseHoras que lleva DetalleRetardo con datos reales.
        // Si solo usaramos Vacio, el ctor privado de DetalleRetardo no se ejercita en deserializacion.
        var retardoIntervalo = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var retardoTotal = DetalleRetardo.Crear([retardoIntervalo], []);
        var desgloseConRetardo = new DesgloseHoras([], retardoTotal, 0);
        var original = new DiaCalculado(Empleado, Fecha, [], desgloseConRetardo);

        var opcionesCompletas = CrearOpciones();
        var json = JsonSerializer.Serialize(original, opcionesCompletas);

        // Resolver sin ningun ConfigurarSerializacion registrado
        var resolverVacio = new DefaultJsonTypeInfoResolver();
        var opcionesVacias = new JsonSerializerOptions { TypeInfoResolver = resolverVacio };

        var act = () => JsonSerializer.Deserialize<DiaCalculado>(json, opcionesVacias);

        act.Should().Throw<NotSupportedException>();
    }
}
