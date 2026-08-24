// Funcion pura documentos+rango+codigos pedidos -> filas del resumen (issue #428): se invoca
// directamente, sin QuerySession/Marten y sin el DSL Given/When/Then de CommandHandlerTestBase,
// reservado a command handlers contra el event store (MEF-ADR-0002). Cada oraculo se arma a mano,
// campo por campo: nunca se reusa el mapeo bajo prueba para construir el esperado.
//
// TotalHorasPorConcepto se verifica con BeEquivalentTo y nunca comparando la fila entera con
// Should().Be(...): IReadOnlyDictionary<string, decimal> no recibe equality estructural del
// compilador de records (mismo gotcha que SintesisCalendarioAsistenciaTests, #427).
//
// CA-1/CA-2/CA-3 del issue #428 -- el recorte de rango (CA-5) vive en RangoConsultaTests.cs, hermano
// de este archivo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarResumenesAsistencia;

public class AgregadorResumenAsistenciaTests
{
    private static AsistenciaDiaria DocumentoDePrueba(
        string codigoColaborador,
        DateOnly fecha,
        EstadoAsistencia estado = EstadoAsistencia.Provisional,
        PlanDelDia plan = PlanDelDia.ConJornada,
        bool noSePresento = false,
        bool franjasIncompletas = false,
        bool vinoEnDescanso = false,
        bool trabajoSinProgramacion = false,
        IReadOnlyDictionary<string, decimal>? horasPorConcepto = null) =>
        new(
            DiaCalculadoAggregateRoot.ComputarStreamId(codigoColaborador, fecha),
            codigoColaborador,
            fecha,
            estado,
            plan,
            "Turno Manana",
            noSePresento,
            franjasIncompletas,
            vinoEnDescanso,
            trabajoSinProgramacion,
            horasPorConcepto ?? new Dictionary<string, decimal>());

    // --- CA-1: los tres ejes cierran contra los dias del rango aplicado ---

    [Fact]
    public void Agregar_ProduceUnaFilaConLosTresEjesCerrandoContraLosDiasDelRango_CuandoTodosLosDiasTienenDocumento()
    {
        const string codigo = "EMP-001";
        var dia1 = new DateOnly(2026, 8, 1);
        var dia2 = new DateOnly(2026, 8, 2);
        var dia3 = new DateOnly(2026, 8, 3);
        var documentos = new[]
        {
            DocumentoDePrueba(codigo, dia1, EstadoAsistencia.Provisional, PlanDelDia.ConJornada,
                horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }),
            DocumentoDePrueba(codigo, dia2, EstadoAsistencia.Aprobado, PlanDelDia.Descanso),
            DocumentoDePrueba(codigo, dia3, EstadoAsistencia.Provisional, PlanDelDia.ConJornada,
                noSePresento: true),
        };

        var filas = AgregadorResumenAsistencia.Agregar(dia1, dia3, null, documentos);

        var fila = filas.Should().ContainSingle().Which;
        fila.CodigoColaborador.Should().Be(codigo);
        (fila.DiasConTurno + fila.DiasConDescanso + fila.DiasSinProgramar).Should().Be(3);
        fila.DiasConTurno.Should().Be(2);
        fila.DiasConDescanso.Should().Be(1);
        fila.DiasSinProgramar.Should().Be(0);
        (fila.Aprobados + fila.Pendientes + fila.SinDatos).Should().Be(3);
        fila.Aprobados.Should().Be(1);
        fila.Pendientes.Should().Be(2);
        fila.SinDatos.Should().Be(0);
        fila.NoSePresento.Should().Be(1);
        fila.FranjasIncompletas.Should().Be(0);
        fila.VinoEnDescanso.Should().Be(0);
        fila.TrabajoSinProgramacion.Should().Be(0);
        fila.TotalHorasPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });
    }

    // --- CA-2: los dias sin fila cuentan como SinDatos/sin programar y no aportan anomalias ---

    [Fact]
    public void Agregar_CuentaLosDiasSinFilaComoSinDatosYSinProgramar_SinAportarAnomalias()
    {
        const string codigo = "EMP-001";
        var dia1 = new DateOnly(2026, 8, 1);
        var dia2 = new DateOnly(2026, 8, 2);
        var dia3 = new DateOnly(2026, 8, 3);
        var documentos = new[] { DocumentoDePrueba(codigo, dia2, franjasIncompletas: true) };

        var filas = AgregadorResumenAsistencia.Agregar(dia1, dia3, null, documentos);

        var fila = filas.Should().ContainSingle().Which;
        fila.DiasSinProgramar.Should().Be(2);
        fila.DiasConTurno.Should().Be(1);
        fila.DiasConDescanso.Should().Be(0);
        fila.SinDatos.Should().Be(2);
        fila.Pendientes.Should().Be(1);
        fila.Aprobados.Should().Be(0);
        fila.FranjasIncompletas.Should().Be(1);
        fila.NoSePresento.Should().Be(0);
        fila.VinoEnDescanso.Should().Be(0);
        fila.TrabajoSinProgramacion.Should().Be(0);
    }

    [Fact]
    public void Agregar_ProduceTodosLosDiasSinDatos_CuandoNingunDocumentoExisteParaElCodigoPedido()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 3);

        var filas = AgregadorResumenAsistencia.Agregar(desde, hasta, ["EMP-Z"], []);

        var fila = filas.Should().ContainSingle().Which;
        fila.CodigoColaborador.Should().Be("EMP-Z");
        fila.DiasSinProgramar.Should().Be(3);
        fila.SinDatos.Should().Be(3);
        fila.Aprobados.Should().Be(0);
        fila.Pendientes.Should().Be(0);
        fila.DiasConTurno.Should().Be(0);
        fila.DiasConDescanso.Should().Be(0);
        fila.NoSePresento.Should().Be(0);
        fila.FranjasIncompletas.Should().Be(0);
        fila.VinoEnDescanso.Should().Be(0);
        fila.TrabajoSinProgramacion.Should().Be(0);
        fila.TotalHorasPorConcepto.Should().BeEmpty();
    }

    // --- CA-3: universo con CodigosColaborador explicito (fila sintetica incluida) vs descubierto ---

    [Fact]
    public void Agregar_ProduceUnaFilaSinteticaConTodoSinDatosYCeros_ParaElCodigoPedidoSinDocumentos()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 2);
        var documentos = new[] { DocumentoDePrueba("EMP-A", desde) };

        var filas = AgregadorResumenAsistencia.Agregar(desde, hasta, ["EMP-A", "EMP-B"], documentos);

        filas.Should().HaveCount(2);
        var filaB = filas.Should().ContainSingle(f => f.CodigoColaborador == "EMP-B").Which;
        filaB.DiasSinProgramar.Should().Be(2);
        filaB.DiasConTurno.Should().Be(0);
        filaB.DiasConDescanso.Should().Be(0);
        filaB.SinDatos.Should().Be(2);
        filaB.Aprobados.Should().Be(0);
        filaB.Pendientes.Should().Be(0);
        filaB.NoSePresento.Should().Be(0);
        filaB.FranjasIncompletas.Should().Be(0);
        filaB.VinoEnDescanso.Should().Be(0);
        filaB.TrabajoSinProgramacion.Should().Be(0);
        filaB.TotalHorasPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Agregar_DevuelveUnaFilaPorCadaCodigoPedido_EnElOrdenDeLaListaPedida()
    {
        var desde = new DateOnly(2026, 8, 1);
        var documentos = new[] { DocumentoDePrueba("EMP-A", desde), DocumentoDePrueba("EMP-B", desde) };

        var filas = AgregadorResumenAsistencia.Agregar(desde, desde, ["EMP-B", "EMP-A"], documentos);

        filas.Select(f => f.CodigoColaborador).Should().Equal("EMP-B", "EMP-A");
    }

    [Fact]
    public void Agregar_DevuelveSoloColaboradoresConAlMenosUnaFilaEnElRango_CuandoNoHayCodigosPedidos()
    {
        var desde = new DateOnly(2026, 8, 1);
        var documentos = new[] { DocumentoDePrueba("EMP-A", desde), DocumentoDePrueba("EMP-C", desde) };

        var filas = AgregadorResumenAsistencia.Agregar(desde, desde, null, documentos);

        filas.Select(f => f.CodigoColaborador).Should().BeEquivalentTo(["EMP-A", "EMP-C"]);
    }

    [Fact]
    public void Agregar_OrdenaAscendentePorCodigoColaborador_CuandoNoHayCodigosPedidos()
    {
        var desde = new DateOnly(2026, 8, 1);
        var documentos = new[] { DocumentoDePrueba("EMP-C", desde), DocumentoDePrueba("EMP-A", desde) };

        var filas = AgregadorResumenAsistencia.Agregar(desde, desde, null, documentos);

        filas.Select(f => f.CodigoColaborador).Should().Equal("EMP-A", "EMP-C");
    }

    [Fact]
    public void Agregar_NoProduceNingunaFila_CuandoNoHayCodigosPedidosYNingunDocumentoCaeDentroDelRango()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 2);
        var documentoFueraDeRango = DocumentoDePrueba("EMP-001", new DateOnly(2026, 8, 10));

        var filas = AgregadorResumenAsistencia.Agregar(desde, hasta, null, [documentoFueraDeRango]);

        filas.Should().BeEmpty();
    }

    // --- Totales de horas por concepto: suma sparse, union de claves ---

    [Fact]
    public void Agregar_SumaHorasPorConceptoConUnionDeClaves_CuandoLosDocumentosTraenConceptosDistintos()
    {
        const string codigo = "EMP-001";
        var dia1 = new DateOnly(2026, 8, 1);
        var dia2 = new DateOnly(2026, 8, 2);
        var documentos = new[]
        {
            DocumentoDePrueba(codigo, dia1,
                horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }),
            DocumentoDePrueba(codigo, dia2,
                horasPorConcepto: new Dictionary<string, decimal> { ["Retardo"] = 0.5m }),
        };

        var filas = AgregadorResumenAsistencia.Agregar(dia1, dia2, null, documentos);

        filas.Should().ContainSingle().Which.TotalHorasPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m, ["Retardo"] = 0.5m });
    }

    [Fact]
    public void Agregar_SumaLosValoresDeUnaMismaClave_CuandoVariosDiasAportanElMismoConcepto()
    {
        const string codigo = "EMP-001";
        var dia1 = new DateOnly(2026, 8, 1);
        var dia2 = new DateOnly(2026, 8, 2);
        var documentos = new[]
        {
            DocumentoDePrueba(codigo, dia1,
                horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }),
            DocumentoDePrueba(codigo, dia2,
                horasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 4.00m }),
        };

        var filas = AgregadorResumenAsistencia.Agregar(dia1, dia2, null, documentos);

        filas.Should().ContainSingle().Which.TotalHorasPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 12.00m });
    }
}
