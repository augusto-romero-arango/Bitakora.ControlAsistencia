using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

// Temporal mientras upstream coercione strings; ver Azure/azure-functions-mcp-extension#129 y
// DictionaryStringObjectJsonConverter.ReadString (issue #586).
public sealed class ArgumentosCrudosMcpMiddleware : IFunctionsWorkerMiddleware
{
    // Replica de Microsoft.Azure.Functions.Worker.Extensions.Mcp.Constants.ToolInvocationContextKey,
    // internal a ese ensamblado: es la clave bajo la que FunctionsMcpContextMiddleware deja el
    // ToolInvocationContext ya bindeado (y coercionado) en context.Items.
    internal const string ClaveContextoTool = "ToolInvocationContext";

    // Valor de Microsoft.Azure.Functions.Worker.Extensions.Mcp.Constants.McpToolTriggerBindingType,
    // internal a ese ensamblado: se replica el literal porque no hay forma de referenciarlo (mismo
    // criterio que IdentidadTenantMcpMiddleware).
    private const string McpToolTriggerBindingType = "mcpToolTrigger";

    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.FunctionDefinition.InputBindings.Values
                .FirstOrDefault(b => b.Type == McpToolTriggerBindingType) is { } bindingMcp &&
            context.BindingContext.BindingData.TryGetValue(bindingMcp.Name, out var jsonCrudo) &&
            jsonCrudo is not null &&
            context.Items.TryGetValue(ClaveContextoTool, out var contextoObj) &&
            contextoObj is ToolInvocationContext bindeado)
        {
            context.Items[ClaveContextoTool] = RestaurarTextoOriginal(bindeado, jsonCrudo.ToString()!);
        }

        return next(context);
    }

    // Nucleo puro y testable: no interpreta el texto (sin DateTimeOffset.UtcDateTime ni
    // conversiones de zona), solo lo devuelve tal como llego en el JSON crudo.
    internal static ToolInvocationContext RestaurarTextoOriginal(
        ToolInvocationContext bindeado, string jsonCrudo)
    {
        if (bindeado.Arguments is null)
            return bindeado;

        using var documento = JsonDocument.Parse(jsonCrudo);
        if (!documento.RootElement.TryGetProperty("arguments", out var argumentos) ||
            argumentos.ValueKind != JsonValueKind.Object)
            return bindeado;

        var copia = new Dictionary<string, object>(bindeado.Arguments, StringComparer.OrdinalIgnoreCase);
        foreach (var propiedad in argumentos.EnumerateObject())
            if (propiedad.Value.ValueKind == JsonValueKind.String && copia.ContainsKey(propiedad.Name))
                copia[propiedad.Name] = propiedad.Value.GetString()!;

        return new ToolInvocationContext
        {
            Name = bindeado.Name,
            SessionId = bindeado.SessionId,
            Transport = bindeado.Transport,
            Arguments = copia,
        };
    }
}
