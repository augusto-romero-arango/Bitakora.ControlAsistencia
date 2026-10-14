using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarSedes;

public partial class BuscarSedesTool(SedesApi api)
{
    internal const string NombreTool = "buscar_sedes";
    internal const int MaximoSedes = 20;

    [Function("BuscarSedes")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca sedes activas para saber a cual se refiere el usuario cuando la nombra de forma "
            + "aproximada. Por nombre o codigo, por ciudad o por direccion: palabras en cualquier "
            + "orden, sin importar acentos ni mayusculas, tambien incompletas ('chapi') o con una "
            + "letra de diferencia. Combina los criterios. Devuelve hasta 20 candidatas con su "
            + "codigo y el motivo de la coincidencia; si hay mas de una plausible, pregunta al "
            + "usuario antes de actuar. Para el catalogo completo usa listar_sedes.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o codigo de la sede, o parte de ellos, en cualquier orden.")]
        string? nombre,
        [McpToolProperty("ciudad", "Ciudad de la sede, o parte de ella.")]
        string? ciudad,
        [McpToolProperty("direccion", "Direccion de la sede, o parte de ella.")]
        string? direccion,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record CoincidenciasDeSedes(
    int Mostrando,
    IReadOnlyList<SedeEncontrada> Sedes,
    string? Nota);

public sealed record SedeEncontrada(
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    IReadOnlyList<string> CoincidePor);
