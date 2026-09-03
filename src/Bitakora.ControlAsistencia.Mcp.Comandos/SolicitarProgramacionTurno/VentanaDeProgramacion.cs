namespace Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

// Value object puro, sin dependencias de infraestructura (MEF-ADR-0012, Tell-don't-Ask): decide
// que dias de una vigencia de colaborador caen dentro de la ventana de trabajo, en vez de exponer
// Desde/Hasta para que un helper externo calcule la interseccion.
public sealed partial class VentanaDeProgramacion
{
    internal const int MaximoDias = 31;

    private readonly DateOnly _desde;
    private readonly DateOnly _hasta;

    private VentanaDeProgramacion(DateOnly desde, DateOnly hasta)
    {
        _desde = desde;
        _hasta = hasta;
    }

    public static VentanaDeProgramacion Crear(DateOnly desde, DateOnly hasta) =>
        throw new NotImplementedException();

    /// <summary>
    /// Interseccion dia a dia de la ventana con [vigenteDesde, vigenteHasta] (vigenteHasta null =
    /// vigencia abierta). Vacia si no se tocan.
    /// </summary>
    public IReadOnlyList<DateOnly> DiasCubiertosPor(DateOnly vigenteDesde, DateOnly? vigenteHasta) =>
        throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();
}
