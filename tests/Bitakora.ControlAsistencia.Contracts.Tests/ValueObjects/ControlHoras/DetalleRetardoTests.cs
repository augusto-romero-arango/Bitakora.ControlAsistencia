// HU-114: Crear enum Concepto y value objects primitivos del desglose
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de DetalleRetardo - detalle del retardo de una franja con invariante de dominio.
/// Interfaz publica: Crear(), Vacio, TiempoRetardado, TiempoCompensado,
///                   MinutosRetardados, MinutosCompensados, RetardoNeto.
/// CA-3: Crear calcula minutos a partir de las listas.
/// CA-4: Crear lanza ArgumentException cuando compensados exceden retardados.
/// CA-5: RetardoNeto = 0 cuando sumas son iguales.
/// CA-6: Vacio tiene listas vacias y totales en cero.
/// </summary>
public class DetalleRetardoTests
{
    // Intervalos reutilizados en multiples tests
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // ---------- CA-3: Crear calcula MinutosRetardados y MinutosCompensados ----------

    [Fact]
    public void Crear_CalculaMinutosRetardados_SumandoTodosLosIntervalosDeRetardo()
    {
        // 30 min + 15 min = 45 min retardados
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30)),
            CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 15))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 20))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.MinutosRetardados.Should().Be(45);
    }

    [Fact]
    public void Crear_CalculaMinutosCompensados_SumandoTodosLosIntervalosDeCompensacion()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 10)),  // 10 min
            CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 10))   // 10 min
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.MinutosCompensados.Should().Be(20);
    }

    [Fact]
    public void Crear_CalculaRetardoNeto_ComoMinutosRetardadosMenosCompensados()
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
    public void Crear_PreservaTiempoRetardado_CuandoListaTieneUnIntervalo()
    {
        var intervaloRetardo = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var retardados = new List<IntervaloTemporal> { intervaloRetardo };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 10))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.TiempoRetardado.Should().HaveCount(1);
        detalle.TiempoRetardado[0].Should().Be(intervaloRetardo);
    }

    [Fact]
    public void Crear_PreservaTiempoCompensado_CuandoListaTieneVariosIntervalos()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 40))  // 40 min
        };
        var comp1 = CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 15));
        var comp2 = CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 15));
        var compensados = new List<IntervaloTemporal> { comp1, comp2 };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.TiempoCompensado.Should().HaveCount(2);
        detalle.TiempoCompensado[0].Should().Be(comp1);
        detalle.TiempoCompensado[1].Should().Be(comp2);
    }

    // ---------- CA-4: Crear lanza ArgumentException cuando compensados > retardados ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoCompensadosExcedenRetardados()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 20))  // 20 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))  // 30 min > 20 min
        };

        var act = () => DetalleRetardo.Crear(retardados, compensados);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DetalleRetardo.Mensajes.CompensadosExcedenRetardados}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoCompensadosSuperanRetardadosConMultiplesIntervalos()
    {
        // 10 min retardados, 15 + 10 = 25 min compensados
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 10))
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 15)),
            CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 10))
        };

        var act = () => DetalleRetardo.Crear(retardados, compensados);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{DetalleRetardo.Mensajes.CompensadosExcedenRetardados}*");
    }

    // ---------- CA-5: RetardoNeto = 0 cuando suma de compensados iguala retardados ----------

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoCompensadosIgualanExactamenteRetardados()
    {
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))  // 30 min
        };
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))  // 30 min = 30 min
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void Crear_RetardoNetoEsCero_CuandoMultiplesIntervalosCompensanExactamente()
    {
        // 45 min retardados: 30 + 15
        var retardados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30)),
            CrearIntervalo(new TimeOnly(9, 0), new TimeOnly(9, 15))
        };
        // 45 min compensados: 25 + 20
        var compensados = new List<IntervaloTemporal>
        {
            CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 25)),
            CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(13, 20))
        };

        var detalle = DetalleRetardo.Crear(retardados, compensados);

        detalle.RetardoNeto.Should().Be(0);
    }

    // ---------- CA-6: Vacio tiene listas vacias y todos los totales en cero ----------

    [Fact]
    public void Vacio_TieneTiempoRetardadoVacio()
    {
        var vacio = DetalleRetardo.Vacio;

        vacio.TiempoRetardado.Should().BeEmpty();
    }

    [Fact]
    public void Vacio_TieneTiempoCompensadoVacio()
    {
        var vacio = DetalleRetardo.Vacio;

        vacio.TiempoCompensado.Should().BeEmpty();
    }

    [Fact]
    public void Vacio_TieneMinutosRetardadosEnCero()
    {
        var vacio = DetalleRetardo.Vacio;

        vacio.MinutosRetardados.Should().Be(0);
    }

    [Fact]
    public void Vacio_TieneMinutosCompensadosEnCero()
    {
        var vacio = DetalleRetardo.Vacio;

        vacio.MinutosCompensados.Should().Be(0);
    }

    [Fact]
    public void Vacio_TieneRetardoNetoEnCero()
    {
        var vacio = DetalleRetardo.Vacio;

        vacio.RetardoNeto.Should().Be(0);
    }
}
