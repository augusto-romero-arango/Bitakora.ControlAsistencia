using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarColaboradores;

public class DescripcionListarColaboradoresTests
{
    private static readonly MethodInfo Run = typeof(ListarColaboradoresTool).GetMethod(nameof(ListarColaboradoresTool.Run))!;

    [Fact]
    public void ListarColaboradores_AgregaLaFraseDeVigenciaAlFinal_CuandoSeInspeccionaLaDescripcion()
    {
        var descripcion = Run.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolTriggerAttribute>())
            .Single(a => a is not null)!.Description;

        descripcion.Should().Be(
            "Lista los colaboradores vinculados: identificacion, codigo, nombre, sede y etiquetas de cada uno. "
            + "Filtra opcionalmente por sede o por etiquetas (pares categoria:valor, AND entre ellos). "
            + "Si envias identificacion consultas la ficha puntual de ese colaborador y los demas filtros no aplican. "
            + "Sin fecha_referencia se usa el dia de hoy. "
            + "Para ver que turno le rige a alguno, pasa su codigoColaborador a consultar_programacion. "
            + "Cada colaborador trae vigenteDesde, y vigenteHasta si su vinculacion termino: "
            + "con ellos confirmas desde y hasta cuando esta vinculado.");
    }

    [Fact]
    public void ListarColaboradores_AclaraQueFechaReferenciaIncluyeIngresosFuturos_CuandoSeInspeccionaLaPropiedad()
    {
        var descripcion = Run.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Single(a => a is not null && a.PropertyName == "fecha_referencia")!.Description;

        descripcion.Should().Be(
            "Fecha desde la que se evalua la vinculacion, formato yyyy-MM-dd: devuelve a quienes no han terminado "
            + "su vinculacion en esa fecha, incluidos los que ingresan despues. No responde quien estaba activo ese dia: "
            + "para eso revisa vigenteDesde y vigenteHasta de cada colaborador. "
            + "Si se omite se usa hoy en America/Bogota.");
    }
}
