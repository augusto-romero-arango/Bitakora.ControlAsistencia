using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaborador;

public partial class RegistrarColaboradorTool(ColaboradoresApi api)
{
    internal const string NombreTool = "registrar_colaborador";

    public Task<string> Run(
        ToolInvocationContext context,
        string tipoIdentificacion,
        string numeroIdentificacion,
        string primerNombre,
        string? segundoNombre,
        string primerApellido,
        string? segundoApellido,
        string codigoColaborador,
        string fechaInicio,
        string? codigoSede,
        CancellationToken ct)
        => throw new NotImplementedException();
}
