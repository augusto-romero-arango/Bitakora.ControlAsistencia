namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

/// <summary>
/// TimeProvider fake manual (nunca NSubstitute) que fija "ahora" a un instante conocido, para
/// verificar la resolucion de fecha_referencia = "hoy" en America/Bogota sin depender del reloj
/// real de la maquina que corre el test.
/// </summary>
public sealed class RelojFalso(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
