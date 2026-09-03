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

    public static VentanaDeProgramacion Crear(DateOnly desde, DateOnly hasta)
    {
        if (desde > hasta)
            throw new ArgumentException(Mensajes.VentanaInvertida);

        var dias = (hasta.DayNumber - desde.DayNumber) + 1;
        if (dias > MaximoDias)
            throw new ArgumentException(Mensajes.VentanaExcedeMaximo);

        return new VentanaDeProgramacion(desde, hasta);
    }

    /// <summary>
    /// Interseccion dia a dia de la ventana con [vigenteDesde, vigenteHasta] (vigenteHasta null =
    /// vigencia abierta). Vacia si no se tocan.
    /// </summary>
    public IReadOnlyList<DateOnly> DiasCubiertosPor(DateOnly vigenteDesde, DateOnly? vigenteHasta)
    {
        var inicio = _desde > vigenteDesde ? _desde : vigenteDesde;
        var fin = vigenteHasta.HasValue && vigenteHasta.Value < _hasta ? vigenteHasta.Value : _hasta;

        if (inicio > fin)
            return [];

        var dias = new List<DateOnly>();
        for (var dia = inicio; dia <= fin; dia = dia.AddDays(1))
            dias.Add(dia);

        return dias;
    }

    public override string ToString() => $"{_desde:yyyy-MM-dd} a {_hasta:yyyy-MM-dd}";
}
