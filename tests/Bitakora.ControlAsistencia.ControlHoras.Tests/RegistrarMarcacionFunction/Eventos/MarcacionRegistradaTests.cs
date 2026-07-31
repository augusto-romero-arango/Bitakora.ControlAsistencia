// Issue #275: Proteger el evento de dominio MarcacionRegistrada con factory y ctor privado.
// Interfaz publica: Crear(...), EmpleadoId, TimestampNormalizado, TipoMarcacion, DispositivoId.
// El evento nunca cruzo el bus como razon para tener ctor publico (#270 ya lo saco del canal);
// este issue le da, por primera vez, la proteccion que ese rol impedia (ver contexto del issue).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction.Eventos;

public class MarcacionRegistradaTests
{
    private const string EmpleadoId = "EMP-001";
    private static readonly DateTime TimestampConSegundos = new(2026, 3, 15, 8, 9, 59);
    private static readonly DateTime TimestampNormalizadoEsperado = new(2026, 3, 15, 8, 9, 0);

    // ---------- CA-1: unica via de construccion es Crear(...); ningun ctor publico ----------

    [Fact]
    public void MarcacionRegistrada_NoExponeConstructorPublico()
    {
        typeof(MarcacionRegistrada).GetConstructors().Should().BeEmpty();
    }

    [Fact]
    public void Crear_RetornaMarcacionRegistrada_ConDatosValidos()
    {
        var evento = MarcacionRegistrada.Crear(EmpleadoId, TimestampConSegundos, "ENTRADA", "DEV-001");

        evento.EmpleadoId.Should().Be(EmpleadoId);
        evento.TipoMarcacion.Should().Be("ENTRADA");
        evento.DispositivoId.Should().Be("DEV-001");
    }

    // ---------- CA-2: Crear trunca los segundos del timestamp recibido (floor al minuto) ----------

    [Fact]
    public void Crear_TruncaSegundosAlMinuto_CuandoTimestampTraeSegundos()
    {
        var evento = MarcacionRegistrada.Crear(EmpleadoId, TimestampConSegundos, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
    }

    [Fact]
    public void Crear_ConservaTimestamp_CuandoYaVieneSinSegundos()
    {
        var evento = MarcacionRegistrada.Crear(
            EmpleadoId, TimestampNormalizadoEsperado, "ENTRADA", "DEV-001");

        evento.TimestampNormalizado.Should().Be(TimestampNormalizadoEsperado);
    }

    // ---------- CA-3: EmpleadoId nulo, vacio o solo espacios es rechazado ----------

    [Fact]
    public void Crear_LanzaArgumentException_CuandoEmpleadoIdEsNulo()
    {
        var act = () => MarcacionRegistrada.Crear(null!, TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.EmpleadoIdVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoEmpleadoIdEsVacio()
    {
        var act = () => MarcacionRegistrada.Crear(string.Empty, TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.EmpleadoIdVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoEmpleadoIdEsSoloEspacios()
    {
        var act = () => MarcacionRegistrada.Crear("   ", TimestampConSegundos, "ENTRADA", "DEV-001");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.EmpleadoIdVacio}*");
    }

    // ---------- TipoMarcacion y DispositivoId son opcionales (nullable) ----------

    [Fact]
    public void Crear_AceptaCamposOpcionalesNulos()
    {
        var evento = MarcacionRegistrada.Crear(EmpleadoId, TimestampConSegundos, null, null);

        evento.TipoMarcacion.Should().BeNull();
        evento.DispositivoId.Should().BeNull();
    }
}
