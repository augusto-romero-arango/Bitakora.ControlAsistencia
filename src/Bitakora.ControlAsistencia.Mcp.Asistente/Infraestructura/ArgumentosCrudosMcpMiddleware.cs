using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

// DictionaryStringObjectJsonConverter.ReadString (Microsoft.Azure.Functions.Worker.Extensions.Mcp)
// aplica TryGetDateTimeOffset/Guid.TryParse a TODO string de "arguments" antes de que la tool lo
// reciba; con destino string, McpInputConversionHelper.ConvertArgumentToTargetType cae en
// Convert.ToString(valor, InvariantCulture) -- "2026-09-01" llega como
// "09/01/2026 00:00:00 +00:00". Azure/azure-functions-mcp-extension#129 cerro este comportamiento
// como "completed" sin preservar el texto original. Este middleware reconstruye, desde el JSON
// crudo del binding, el ToolInvocationContext que la tool recibe.
public sealed class ArgumentosCrudosMcpMiddleware : IFunctionsWorkerMiddleware
{
    // Mismos valores que Constants.ToolInvocationContextKey/Constants.McpToolTriggerBindingType de
    // la extension -- ambas internal, sin forma de referenciarlas desde este proyecto.
    private const string ClaveContextoTool = "ToolInvocationContext";
    private const string TipoBindingMcpToolTrigger = "mcpToolTrigger";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.FunctionDefinition.InputBindings.Values
                .FirstOrDefault(b => b.Type == TipoBindingMcpToolTrigger) is { } binding &&
            context.BindingContext.BindingData.TryGetValue(binding.Name, out var valorCrudo) &&
            valorCrudo?.ToString() is { } jsonCrudo &&
            context.Items.TryGetValue(ClaveContextoTool, out var itemCrudo) &&
            itemCrudo is ToolInvocationContext bindeado)
        {
            context.Items[ClaveContextoTool] = RestaurarTextoOriginal(bindeado, jsonCrudo);
        }

        await next(context);
    }

    // internal, no private: Paso 4 lo prueba nivel 1 sin host (FunctionContext no es instanciable;
    // ToolInvocationContext si).
    internal static ToolInvocationContext RestaurarTextoOriginal(ToolInvocationContext bindeado, string jsonCrudo)
    {
        if (bindeado.Arguments is null)
            return bindeado;

        using var documento = JsonDocument.Parse(jsonCrudo);

        if (!documento.RootElement.TryGetProperty("arguments", out var argumentos) ||
            argumentos.ValueKind != JsonValueKind.Object)
            return bindeado;

        var restaurados = new Dictionary<string, object>(bindeado.Arguments, StringComparer.OrdinalIgnoreCase);

        foreach (var propiedad in argumentos.EnumerateObject())
        {
            if (propiedad.Value.ValueKind == JsonValueKind.String && restaurados.ContainsKey(propiedad.Name))
                restaurados[propiedad.Name] = propiedad.Value.GetString()!;
        }

        return new ToolInvocationContext
        {
            Name = bindeado.Name,
            Arguments = restaurados,
            SessionId = bindeado.SessionId,
            Transport = bindeado.Transport
        };
    }
}
