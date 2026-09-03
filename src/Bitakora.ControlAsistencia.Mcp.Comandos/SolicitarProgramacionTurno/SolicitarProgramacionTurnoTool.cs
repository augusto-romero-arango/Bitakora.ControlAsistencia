using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

// Cliente HTTP puro que envuelve N veces el comando SolicitarProgramacionTurno (POST
// programacion/solicitudes): un lote no es concepto del dominio (glosario: Ventana de trabajo es
// efimera, nunca se persiste) y el marco no ofrece atomicidad entre streams -- por eso esta tool
// re-verifica sede/turno/directorio por su cuenta y arma una solicitud por colaborador (MEF-ADR-0047
// decision 4). Los rechazos del dominio en cada POST se traducen a texto (CA-ADR-0030) y no
// detienen al resto del lote: el resto ya pudo haberse programado.
public partial class SolicitarProgramacionTurnoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "solicitar_programacion_turno";
    internal const int MaximoIdentificaciones = 200;
    internal const int PostsSimultaneos = 8;

    [Function("SolicitarProgramacionTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a una lista de colaboradores en una sede, para todos los dias de "
            + "una ventana de trabajo de maximo 31 dias. Recibe la ventana (desde, hasta), el "
            + "nombre exacto del turno del catalogo (miralo con listar_turnos), el codigo de la "
            + "sede donde se registrara la programacion -- sede de programacion, distinta de la "
            + "sede de trabajo de cada colaborador; pidesela al usuario, nunca la asumas -- y las "
            + "identificaciones completas de los colaboradores, separadas por coma, tal como las "
            + "devuelven buscar_colaboradores o listar_colaboradores: no las inventes ni pases "
            + "numeros sin tipo. A cada colaborador le programa solo los dias de la ventana que su "
            + "vinculacion cubre; los que no cubren ninguno o no se encuentran se omiten sin "
            + "detalle. Responde quienes quedaron programados y con que fechas. La programacion "
            + "aparece en consultar_programacion unos segundos despues.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "desde",
            "Primer dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 31 dias.",
            isRequired: true)]
        string desde,
        [McpToolProperty(
            "hasta",
            "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 31 dias.",
            isRequired: true)]
        string hasta,
        [McpToolProperty(
            "turno",
            "Nombre exacto del turno del catalogo (ej. 'Cocina manana'); miralo con listar_turnos.",
            isRequired: true)]
        string turno,
        [McpToolProperty(
            "sede_de_programacion",
            "Codigo de la sede donde se registra la programacion. Pidesela al usuario; no es la "
            + "sede de trabajo del colaborador.",
            isRequired: true)]
        string sedeDeProgramacion,
        [McpToolProperty(
            "identificaciones",
            "Identificaciones completas separadas por coma ('CC-79879078, CE-887766'), tal como "
            + "las devuelve buscar_colaboradores; maximo 200.",
            isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Eco compacto de solicitar_programacion_turno hacia el asistente: cada solicitud del dominio
/// responde 202 sin body, asi que el hecho programado se reconstruye con lo que entro a la tool y
/// lo que devolvio el directorio.
/// </summary>
public sealed record ProgramacionSolicitadaResumen(
    string Resultado,
    string Turno,
    SedeResumen Sede,
    string Ventana,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota);

public sealed record SedeResumen(string Codigo, string Nombre);

public sealed record ColaboradorProgramadoResumen(
    string Identificacion, string Nombre, string CodigoColaborador, DateOnly Desde, DateOnly Hasta, int Dias);

public sealed record ColaboradorFallidoResumen(string Identificacion, string Motivo);
