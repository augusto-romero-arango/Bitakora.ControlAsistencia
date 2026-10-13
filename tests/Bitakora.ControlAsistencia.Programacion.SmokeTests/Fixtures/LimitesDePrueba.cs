namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

public sealed record FirmaDeLimites(
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana)
{
    public int SemanalesHoras => HorasSemanalesEnMinutos / 60;
    public int SemanalesMinutos => HorasSemanalesEnMinutos % 60;
    public int TopeHoras => TopeDiarioEnMinutos / 60;
    public int TopeMinutos => TopeDiarioEnMinutos % 60;
    public int MinimoHoras => MinimoDiarioEnMinutos / 60;
    public int MinimoMinutos => MinimoDiarioEnMinutos % 60;

    public bool EsValida =>
        TopeDiarioEnMinutos is > 0 and <= 24 * 60
        && MinimoDiarioEnMinutos >= 0 && MinimoDiarioEnMinutos <= TopeDiarioEnMinutos
        && DiasDescansoPorSemana is >= 0 and <= 6
        && HorasSemanalesEnMinutos > 0
        && HorasSemanalesEnMinutos <= TopeDiarioEnMinutos * (7 - DiasDescansoPorSemana);

    public object Limites() => new
    {
        horasSemanales = HorasYMinutos(HorasSemanalesEnMinutos),
        topeDiario = HorasYMinutos(TopeDiarioEnMinutos),
        minimoDiario = HorasYMinutos(MinimoDiarioEnMinutos),
        diasDescansoPorSemana = DiasDescansoPorSemana
    };

    public object PayloadCrear(Guid jornadaId) => new
    {
        jornadaId,
        horasSemanales = HorasYMinutos(HorasSemanalesEnMinutos),
        topeDiario = HorasYMinutos(TopeDiarioEnMinutos),
        minimoDiario = HorasYMinutos(MinimoDiarioEnMinutos),
        diasDescansoPorSemana = DiasDescansoPorSemana
    };

    private static object HorasYMinutos(int totalMinutos) => new { horas = totalMinutos / 60, minutos = totalMinutos % 60 };
}

// Cada Jornada que crea un smoke de Programacion tiene aqui una firma propia: la restriccion de
// unicidad es por contenido (CA-ADR-0040). Reservada es la firma de la Jornada predeterminada que
// otros proyectos de smoke reutilizan en tenant-smoke; ningun test de Programacion la modifica.
public static class LimitesDePrueba
{
    public static readonly FirmaDeLimites Reservada = Firma(42, 0);

    public static readonly FirmaDeLimites CrearJornadaPersiste = Firma(30, 1);
    public static readonly FirmaDeLimites CrearJornadaDuplicada = Firma(30, 2);
    public static readonly FirmaDeLimites CrearJornadaIdGeneral = Firma(30, 37);
    public static readonly FirmaDeLimites ObtenerJornada = Firma(30, 3);
    public static readonly FirmaDeLimites ModificarOrigen = Firma(30, 4);
    public static readonly FirmaDeLimites ModificarDestino = Firma(31, 4);
    public static readonly FirmaDeLimites ModificarConflictoA = Firma(30, 5);
    public static readonly FirmaDeLimites ModificarConflictoB = Firma(31, 5);
    public static readonly FirmaDeLimites ModificarPublicaOrigen = Firma(30, 6);
    public static readonly FirmaDeLimites ModificarPublicaDestino = Firma(31, 6);
    public static readonly FirmaDeLimites ModificarNoPublicaOrigen = Firma(30, 7);
    public static readonly FirmaDeLimites ModificarNoPublicaDestino = Firma(31, 7);
    public static readonly FirmaDeLimites ModificarTopeInvalido = Firma(30, 8);
    public static readonly FirmaDeLimites QuitarJornadaPersiste = Firma(30, 9);
    public static readonly FirmaDeLimites QuitarJornadaSinNuevosEventos = Firma(30, 19);
    public static readonly FirmaDeLimites CrearPlantillaConJornada = Firma(30, 10);
    public static readonly FirmaDeLimites ObtenerCuadro = Firma(30, 11);
    public static readonly FirmaDeLimites ListarCuadros = Firma(30, 12);
    public static readonly FirmaDeLimites AsignarAPlantillaPersiste = Firma(30, 13);
    public static readonly FirmaDeLimites AsignarAPlantillaRepite = Firma(30, 15);
    public static readonly FirmaDeLimites AsignarAPlantillaAutocorrigeOrigen = Firma(30, 14);
    public static readonly FirmaDeLimites AsignarAPlantillaAutocorrigeDestino = Firma(33, 14);
    public static readonly FirmaDeLimites AsignarAPlantillaJornadaNoExiste = Firma(30, 16);
    public static readonly FirmaDeLimites AsignarAPlantillaPlantillaNoExiste = Firma(30, 17);
    public static readonly FirmaDeLimites AsignarAPlantillaPlantillaRetirada = Firma(30, 18);
    public static readonly FirmaDeLimites ListarIncluyeLaCreada = Firma(47, 1);
    public static readonly FirmaDeLimites ListarPaginaPrimera = Firma(47, 2);
    public static readonly FirmaDeLimites ListarPaginaSegunda = Firma(47, 3);
    public static readonly FirmaDeLimites ListarMarcaPredeterminada = Firma(47, 4);
    public static readonly FirmaDeLimites AsignarPredeterminada = Firma(30, 20);
    public static readonly FirmaDeLimites DesactivarJornadaPersiste = Firma(30, 21);
    public static readonly FirmaDeLimites DesactivarJornadaPredeterminada = Firma(30, 22);
    public static readonly FirmaDeLimites DesactivarJornadaEnPlantilla = Firma(30, 23);
    public static readonly FirmaDeLimites SolicitarProgramacion = Firma(42, 1, topeHoras: 8, topeMinutos: 30,
        minimoHoras: 4);

    static LimitesDePrueba()
    {
        var entradas = typeof(LimitesDePrueba)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(FirmaDeLimites))
            .Select(f => (f.Name, Firma: (FirmaDeLimites)f.GetValue(null)!))
            .ToList();

        var invalidas = entradas.Where(e => !e.Firma.EsValida).Select(e => e.Name).ToList();
        if (invalidas.Count > 0)
            throw new InvalidOperationException(
                $"Firmas de limites invalidas para LimitesJornada.Crear: {string.Join(", ", invalidas)}");

        var conflictos = entradas
            .GroupBy(e => e.Firma)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(e => e.Name)))
            .ToList();
        if (conflictos.Count > 0)
            throw new InvalidOperationException(
                $"Firmas de limites duplicadas en el catalogo: {string.Join("; ", conflictos)}");
    }

    private static FirmaDeLimites Firma(int horas, int minutos, int topeHoras = 8, int topeMinutos = 0,
        int minimoHoras = 0, int minimoMinutos = 0, int descansos = 1) =>
        new(horas * 60 + minutos, topeHoras * 60 + topeMinutos, minimoHoras * 60 + minimoMinutos, descansos);
}
