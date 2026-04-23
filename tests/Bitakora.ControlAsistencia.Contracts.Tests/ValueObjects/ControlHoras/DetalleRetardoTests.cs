// HU-114: Crear enum Concepto y value objects primitivos del desglose
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de DetalleRetardo - detalle del retardo de una franja.
/// Interfaz publica: Crear(), Vacio, RetardoNeto, ToString(), Equals/GetHashCode.
/// Los minutos crudos y los intervalos son privados por diseño — nadie externo opera sobre ellos,
/// solo se leen via ToString() para trazabilidad.
/// CA-3: Crear produce un detalle con totales coherentes con las listas (verificado via ToString).
/// CA-4/CA-5: RetardoNeto saturado en 0 cuando compensacion >= retardo.
/// CA-6: Vacio tiene ToString "Sin retardo" y RetardoNeto = 0.
/// </summary>
public class DetalleRetardoTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // ---------- RetardoNeto: unico observable numerico ----------

    [Fact]
    public void Crear_CalculaRetardoNeto_ComoDiferenciaCuandoCompensacionParcial()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 45))  // 45 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))  // 20 min
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(25);
    }

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoCompensadosIgualanExactamenteRetardados()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))  // 30 min
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoMultiplesIntervalosCompensanExactamente()
    {
        // 45 min retardados (30 + 15) = 45 min compensados (25 + 20)
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30)),
            CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 15))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 25)),
            CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 20))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoCompensadosExcedenRetardados()
    {
        // 20 min retardados, 30 min compensados => exceso 10 min no cuenta aqui, neto = 0
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoCompensadosSuperanRetardadosConMultiplesIntervalos()
    {
        // 10 min retardados, 25 min compensados (15 + 10) => neto = 0
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 10))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 15)),
            CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 10))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    // ---------- ToString: unica ventana a los datos internos ----------

    [Fact]
    public void ToString_MuestraIntervalosYTotales_CuandoCompensacionParcial()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 45))  // 45 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))  // 20 min
        };
        var detalle = DetalleRetardo.Crear(retardados, compensados);

        var texto = detalle.ToString();

        texto.Should().Contain(retardados[0].ToString());
        texto.Should().Contain(compensados[0].ToString());
        texto.Should().Contain("(45min)");
        texto.Should().Contain("(20min)");
        texto.Should().Contain("25min");  // neto
    }

    [Fact]
    public void ToString_IncluyeCadaIntervalo_CuandoVariosRetardos()
    {
        var r1 = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20));
        var r2 = CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 25));
        var detalle = DetalleRetardo.Crear(
            [r1, r2],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);

        var texto = detalle.ToString();

        texto.Should().Contain(r1.ToString());
        texto.Should().Contain(r2.ToString());
    }

    [Fact]
    public void ToString_IncluyeCadaIntervalo_CuandoVariasCompensaciones()
    {
        var c1 = CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 15));
        var c2 = CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 15));
        var detalle = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 40))],
            [c1, c2]);

        var texto = detalle.ToString();

        texto.Should().Contain(c1.ToString());
        texto.Should().Contain(c2.ToString());
    }

    [Fact]
    public void ToString_MuestraNetoEnCeroYCompensacionCruda_CuandoCompensacionExcedeRetardo()
    {
        // 20 min retardado, 30 min compensado (excedente 10 que vive fuera de este VO).
        var detalle = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);

        var texto = detalle.ToString();

        texto.Should().Contain("(20min)");  // retardo crudo
        texto.Should().Contain("(30min)");  // compensado crudo (preserva trazabilidad)
        texto.Should().Contain("0min");     // neto saturado
    }

    // ---------- Vacio ----------

    [Fact]
    public void Vacio_TieneRetardoNetoEnCero()
    {
        DetalleRetardo.Vacio.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Vacio_ToStringEsSinRetardo()
    {
        DetalleRetardo.Vacio.ToString().Should().Be(DetalleRetardo.Mensajes.SinRetardo);
    }
}
