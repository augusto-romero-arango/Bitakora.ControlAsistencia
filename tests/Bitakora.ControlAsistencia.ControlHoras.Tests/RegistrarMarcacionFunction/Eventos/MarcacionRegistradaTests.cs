// Issue #275: Proteger el evento de dominio MarcacionRegistrada con factory y ctor privado.
// Interfaz publica: Crear(...), CodigoColaborador, TimestampNormalizado, TipoMarcacion, DispositivoId.
// El evento nunca cruzo el bus como razon para tener ctor publico (#270 ya lo saco del canal);
// este issue le da, por primera vez, la proteccion que ese rol impedia (ver contexto del issue).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction.Eventos;

public class MarcacionRegistradaTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateTime TimestampConSegundos = new(2026, 3, 15, 8, 9, 59);
    private static readonly DateTime TimestampNormalizadoEsperado = new(2026, 3, 15, 8, 9, 0);

    // ---------- CA-1: unica via de construccion es Crear(...); ningun ctor publico ----------

    [Fact]
    public void MarcacionRegistrada_NoExponeConstructorPublico()
    {
        typeof(MarcacionRegistrada).GetConstructors().Should().BeEmpty();
    }

    [Fact]
    public void Crear_RetornaMarcacionRegistrada_CuandoDatosSonValidos()
    {
        var evento = MarcacionRegistrada.Crear(CodigoColaborador, TimestampConSegundos, "ENTRADA", "DEV-001");

        evento.CodigoColaborador.Should().Be(CodigoColaborador);
        evento.TipoMarcacion.Should().Be("ENTRADA");
        evento.DispositivoId.Should().Be("DEV-001");
    }

    // ---------- CA-2: Crear trunca los segundos del timestamp recibido (floor al minuto) ----------

    [Fact]
    public void Crear_TruncaSegundosAlMinuto_CuandoTimestampTraeSegundos()
    {
        var evento = MarcacionRegistrada.Crear(CodigoColaborador, TimestampConSegundos, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
    }

    [Fact]
    public void Crear_ConservaTimestamp_CuandoYaVieneSinSegundos()
    {
        var evento = MarcacionRegistrada.Crear(
            CodigoColaborador, TimestampNormalizadoEsperado, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
    }

    // Un dispositivo puede reportar sub-segundos: el floor tambien los descarta, no solo los
    // segundos enteros. Sin esto, dos marcaciones del mismo minuto podrian diferir en ticks.
    [Fact]
    public void Crear_TruncaFraccionesDeSegundo_CuandoTimestampTraeTicks()
    {
        var conTicks = TimestampConSegundos.AddTicks(1234567);

        var evento = MarcacionRegistrada.Crear(CodigoColaborador, conTicks, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
    }

    // El truncamiento no puede reinterpretar la zona horaria: el evento persiste el mismo Kind que
    // recibio (el endpoint entrega Utc). Fijarlo protege el cambio de implementacion del floor.
    [Fact]
    public void Crear_PreservaElKindDelTimestamp_CuandoTimestampEsUtc()
    {
        var utcConSegundos = new DateTime(2026, 3, 15, 8, 9, 59, DateTimeKind.Utc);

        var evento = MarcacionRegistrada.Crear(CodigoColaborador, utcConSegundos, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Kind.Should().Be(DateTimeKind.Utc);
        evento.TimestampNormalizado.Should().Be(
            new DateTime(2026, 3, 15, 8, 9, 0, DateTimeKind.Utc));
    }

    // ---------- CA-3: CodigoColaborador nulo, vacio o solo espacios es rechazado ----------

    [Fact]
    public void Crear_LanzaArgumentException_CuandoCodigoColaboradorEsNulo()
    {
        var act = () => MarcacionRegistrada.Crear(null!, TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.CodigoColaboradorVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoCodigoColaboradorEsVacio()
    {
        var act = () => MarcacionRegistrada.Crear(string.Empty, TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.CodigoColaboradorVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoCodigoColaboradorEsSoloEspacios()
    {
        var act = () => MarcacionRegistrada.Crear("   ", TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.CodigoColaboradorVacio}*");
    }

    // ---------- TipoMarcacion y DispositivoId son opcionales (nullable) ----------

    [Fact]
    public void Crear_AceptaCamposOpcionalesNulos()
    {
        var evento = MarcacionRegistrada.Crear(CodigoColaborador, TimestampConSegundos, null, null);

        evento.TipoMarcacion.Should().BeNull();
        evento.DispositivoId.Should().BeNull();
    }
}
