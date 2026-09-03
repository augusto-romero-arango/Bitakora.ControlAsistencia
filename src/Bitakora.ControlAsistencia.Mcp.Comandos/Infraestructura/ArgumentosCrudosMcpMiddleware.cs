using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

// Temporal mientras upstream coercione strings; ver Azure/azure-functions-mcp-extension#129 y
// DictionaryStringObjectJsonConverter.ReadString (issue #586).
public sealed class ArgumentosCrudosMcpMiddleware : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next) =>
        throw new NotImplementedException();

    internal static ToolInvocationContext RestaurarTextoOriginal(
        ToolInvocationContext bindeado, string jsonCrudo) =>
        throw new NotImplementedException();
}
