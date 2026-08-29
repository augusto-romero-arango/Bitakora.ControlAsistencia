using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Cosmos.EventSourcing.Abstractions;
// Alias, no using de namespace: ReadModels.ControlHoras.FranjaDepurada/MarcacionDelDia son el
// tercer espejo del mismo termino (MEF-ADR-0039 decision 6) y colisionarian (CS0104) con los
// homonimos de DomainEvents que ya tipan _franjas/_marcaciones en este archivo.
using DepuracionDelDia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.DepuracionDelDia;
using VistaFranjaDepurada = Bitakora.ControlAsistencia.ReadModels.ControlHoras.FranjaDepurada;
using VistaMarcacionDelDia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.MarcacionDelDia;
using EstadoAsistencia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.EstadoAsistencia;
using PlanDelDia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.PlanDelDia;
using SedeDeFranja = Bitakora.ControlAsistencia.ReadModels.ControlHoras.SedeDeFranja;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Expediente de aprobacion de un dia de trabajo: recibe cada foto de DiaDepurado (ya traducida a
// tipos de dominio por el handler) y mantiene los valores provisionales del dia. Vive separado de
// ControlDiario a proposito -- el juicio humano del Aprobador no comparte aggregate con el calculo
// automatico. El ciclo Provisional -> Aprobado llega con el issue de acciones del Aprobador.
public partial class DiaCalculadoAggregateRoot : AggregateRoot
{
    public EstadoDiaCalculado Estado { get; private set; }

    // Sin consumidor todavia en este repo -- no es codigo muerto: la consumiran la invariante de
    // AprobarDia (aun no existe) y la guarda de resolucion del conflicto (#483).
    public bool TieneConflictoDeSedePendiente =>
        _franjas.Any(franja => DerivarSedeDeFranja(franja, _marcaciones).EnConflicto);

    // Valores provisionales de la ultima foto recibida: nunca se exponen como propiedades sueltas
    // (MEF-ADR-0012, Tell-don't-Ask). Solo Apply los reemplaza.
    private string _codigoColaborador = string.Empty;
    private DateOnly _fecha;
    private ResumenColaborador? _colaborador;
    private string? _nombreTurno;
    private IReadOnlyList<FranjaDepurada> _franjas = [];
    private IReadOnlyList<MarcacionDelDia> _marcaciones = [];
    private HorasDiscriminadas? _horasDiscriminadas;
    private IReadOnlyList<SedeDecidida> _sedesDecididas = [];

    // CA-ADR-0031: el prefijo "dc" disjunta este stream del de ControlDiario ("cd"), que comparte la
    // identidad logica colaborador+fecha en el mismo store; la fecha va en ISO 8601 basico para que
    // no aporte ':' propios. Los tres valores son el contrato de identidad de todo stream ya
    // escrito -- cambiar cualquiera exige migracion.
    private const string PrefijoStreamId = "dc";
    private const char SeparadorStreamId = ':';
    private const string FormatoFechaStreamId = "yyyyMMdd";

    // Punto unico de conversion de la identidad (MEF-ADR-0037): dos mensajes del mismo
    // colaborador+fecha convergen sobre el mismo stream. InvariantCulture: una culture con
    // calendario no gregoriano (ar-SA) rendiria otro anio en la clave.
    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
    {
        var fechaBasica = fecha.ToString(FormatoFechaStreamId, CultureInfo.InvariantCulture);
        return $"{PrefijoStreamId}{SeparadorStreamId}{codigoColaborador}{SeparadorStreamId}{fechaBasica}";
    }

    // MEF-ADR-0004: Apply no lanza. Reemplaza la foto completa sin comparar contra el estado previo
    // -- sin deduplicacion de ningun tipo (CA-4).
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(DepuracionDiaRecibida e)
    {
        Id = e.Id;
        _codigoColaborador = e.CodigoColaborador;
        _fecha = e.Fecha;
        _colaborador = e.Colaborador;
        _nombreTurno = e.NombreTurno;
        _franjas = e.Franjas;
        _marcaciones = e.Marcaciones;
        _horasDiscriminadas = e.HorasDiscriminadas;
        Estado = EstadoDiaCalculado.Provisional;
    }

    // CA-1/CA-3: todo dia que llega nace, anomalo incluido. Deja el evento en _uncommittedEvents
    // para que el llamador lo persista con StartStream.
    internal static DiaCalculadoAggregateRoot Iniciar(DepuracionDiaRecibida evento)
    {
        var dia = new DiaCalculadoAggregateRoot();
        dia._uncommittedEvents.Add(evento);
        dia.Apply(evento);
        return dia;
    }

    // CA-2/CA-4: toda foto que llega a un dia ya existente se agrega al mismo stream, siempre.
    // Un dia ya Aprobado no incorpora la foto tardia -- Apply(DepuracionDiaRecibida) lo regresaria
    // a Provisional -- pero tampoco la ignora: queda como evidencia auditable en el mismo stream.
    internal void RecibirDepuracion(DepuracionDiaRecibida evento)
    {
        if (Estado == EstadoDiaCalculado.Aprobado)
        {
            var evidencia = DepuracionPosAprobacionRecibida.Desde(evento);
            _uncommittedEvents.Add(evidencia);
            Apply(evidencia);
            return;
        }

        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // MEF-ADR-0004: Apply no lanza ni muta -- rehidratar un stream con este evento reproduce
    // exactamente el mismo dia Aprobado. public: requerido para que TestStore.ApplyEvent lo
    // encuentre via GetMethods().
    public void Apply(DepuracionPosAprobacionRecibida e) { }

    // Issue #489. MEF-ADR-0004: Apply no lanza -- reemplaza la foto completa sin comparar contra
    // el estado previo. public: requerido para que TestStore.ApplyEvent lo encuentre via
    // GetMethods(). Es tambien el primer evento del aval del vacio (CA-7): fija Id, colaborador y
    // fecha aunque nunca haya llegado una DepuracionDiaRecibida.
    public void Apply(DiaAprobado e)
    {
        Id = e.Id;
        _codigoColaborador = e.CodigoColaborador;
        _fecha = e.Fecha;
        _sedesDecididas = e.SedesDecididas;
        Estado = EstadoDiaCalculado.Aprobado;
    }

    // Issue #489: cierre del acto de aprobar. CA-ADR-0030 -- declina con resultado, nunca lanza; el
    // handler traduce cada valor distinto de Aprobado a InvalidOperationException. streamId,
    // codigoColaborador y fecha viajan como parametros (no se leen de _codigoColaborador/_fecha)
    // porque el aval del vacio (CA-7) llama este mismo metodo sobre un aggregate recien construido,
    // sin ninguna DepuracionDiaRecibida previa que los haya fijado.
    internal ResultadoAprobacion Aprobar(
        string streamId, string codigoColaborador, DateOnly fecha,
        IReadOnlyList<DecisionDeSede> decisiones)
    {
        if (Estado == EstadoDiaCalculado.Aprobado)
            return ResultadoAprobacion.DiaYaAprobado;

        var sedesDecididas = new List<SedeDecidida>();
        var horasDecididas = new HashSet<TimeOnly>();

        foreach (var decision in decisiones)
        {
            var franja = _franjas.FirstOrDefault(f => f.HoraInicioProgramada == decision.HoraInicioProgramada);
            if (franja is null)
                return ResultadoAprobacion.DecisionParaFranjaInvalida;

            var derivacion = DerivarSedeDeFranja(franja, _marcaciones);
            if (!derivacion.EnConflicto)
                return ResultadoAprobacion.DecisionParaFranjaInvalida;

            var candidata = derivacion.Candidatas.FirstOrDefault(c => c.Codigo == decision.CodigoSede);
            if (candidata is null)
                return ResultadoAprobacion.CodigoSedeNoCandidata;

            sedesDecididas.Add(new SedeDecidida(
                decision.HoraInicioProgramada, candidata.Codigo, candidata.Nombre, candidata.CentroDeCostos));
            horasDecididas.Add(decision.HoraInicioProgramada);
        }

        var quedanConflictosSinDecidir = _franjas.Any(f =>
            DerivarSedeDeFranja(f, _marcaciones).EnConflicto && !horasDecididas.Contains(f.HoraInicioProgramada));
        if (quedanConflictosSinDecidir)
            return ResultadoAprobacion.ConflictosSinDecidir;

        var evento = DiaAprobado.Crear(streamId, codigoColaborador, fecha, sedesDecididas);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAprobacion.Aprobado;
    }

    // Tell-don't-Ask (MEF-ADR-0012): el aggregate produce la vista de lectura desde su estado
    // privado -- ninguna propiedad nueva se expone. Plan sale de la senal estructural del contrato
    // de DiaDepurado (NombreTurno null -> SinProgramar; nombre + cero franjas -> Descanso), no de un
    // campo propio del evento.
    public DepuracionDelDia GenerarDepuracionDelDia()
    {
        var plan = ClasificarPlan(_nombreTurno, _franjas);
        var horas = _horasDiscriminadas;

        return new DepuracionDelDia(
            _codigoColaborador,
            _fecha,
            _colaborador?.Identificacion,
            _colaborador?.NombreCompleto,
            MapearEstado(Estado),
            plan,
            _nombreTurno,
            _franjas
                .Select(franja =>
                {
                    var (efectiva, enConflicto, candidatas) = DerivarSedeDeFranja(franja, _marcaciones);

                    // Issue #489 CA-2: el expediente aprobado se lee resuelto -- la sede que el
                    // Aprobador decidio reemplaza la derivacion de conflicto de esa franja.
                    var decidida = _sedesDecididas.FirstOrDefault(
                        sd => sd.HoraInicioProgramada == franja.HoraInicioProgramada);
                    if (decidida is not null)
                    {
                        efectiva = new SedeDeFranja(decidida.CodigoSede, decidida.NombreSede, decidida.CentroDeCostos);
                        enConflicto = false;
                        candidatas = [];
                    }

                    return new VistaFranjaDepurada(
                        franja.HoraInicioProgramada,
                        franja.HoraFinProgramada,
                        franja.DiaOffsetFin,
                        franja.Entrada,
                        franja.Salida,
                        franja.EsAnomala,
                        SedeEfectiva: efectiva,
                        EnConflictoDeSede: enConflicto,
                        CandidatasDeSede: candidatas);
                })
                .ToList(),
            _marcaciones
                .Select(marcacion => new VistaMarcacionDelDia(
                    marcacion.Timestamp,
                    marcacion.Tipo,
                    EsUsada(marcacion, _franjas),
                    marcacion.CodigoSede,
                    marcacion.NombreSede,
                    marcacion.CentroDeCostos))
                .ToList(),
            horas?.HorasPorConcepto ?? new Dictionary<string, decimal>(),
            horas?.Trazabilidad ?? []);
    }

    private static PlanDelDia ClasificarPlan(string? nombreTurno, IReadOnlyList<FranjaDepurada> franjas) =>
        nombreTurno switch
        {
            null => PlanDelDia.SinProgramar,
            _ when franjas.Count == 0 => PlanDelDia.Descanso,
            _ => PlanDelDia.ConJornada
        };

    private static EstadoAsistencia MapearEstado(EstadoDiaCalculado estado) =>
        estado switch
        {
            EstadoDiaCalculado.Provisional => EstadoAsistencia.Provisional,
            EstadoDiaCalculado.Aprobado => EstadoAsistencia.Aprobado,
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null)
        };

    // Una marcacion pertenece a una franja si su Timestamp coincide EXACTAMENTE con la entrada o la
    // salida de esa franja. Regla unica del expediente: la usan por igual la marca de uso de la
    // vista y las candidatas de sede.
    private static bool PerteneceA(MarcacionDelDia marcacion, FranjaDepurada franja) =>
        marcacion.Timestamp == franja.Entrada || marcacion.Timestamp == franja.Salida;

    // Derivada una sola vez aqui: ningun cliente la recalcula.
    private static bool EsUsada(MarcacionDelDia marcacion, IReadOnlyList<FranjaDepurada> franjas) =>
        franjas.Any(franja => PerteneceA(marcacion, franja));

    // Politica en firme (glosario "Conflicto de sede"): SIN DEFAULT -- dos o mas codigos de sede
    // distintos entre las fuentes de una franja dejan la decision al Aprobador (#483); la maquina
    // no elige por el, expone las candidatas y se abstiene de sede efectiva.
    //
    // Duplicada a proposito (MEF-ADR-0018): se re-deriva, reducida al booleano, en
    // Projections.ControlHoras.AsistenciaDiariaProjection.EsConflictoDeSedePendiente. Si cambia
    // aqui, cambia alla.
    private static (SedeDeFranja? Efectiva, bool EnConflicto, IReadOnlyList<SedeDeFranja> Candidatas)
        DerivarSedeDeFranja(FranjaDepurada franja, IReadOnlyList<MarcacionDelDia> marcaciones)
    {
        // Last() por codigo, no First(): el CentroDeCostos estampado en la marcacion prevalece
        // sobre el programado cuando ambas fuentes coinciden en sede -- tambien dentro del
        // conflicto, para que la candidata que elija el Aprobador herede el CC correcto.
        var candidatas = FuentesDeSede(franja, marcaciones)
            .GroupBy(fuente => fuente.Codigo)
            .Select(porCodigo => porCodigo.Last())
            .ToList();

        return candidatas switch
        {
            [] => (null, false, []),
            [var unica] => (unica, false, []),
            _ => (null, true, candidatas)
        };
    }

    // Orden significativo: la programada primero, las marcadas despues -- de ahi sale la precedencia
    // del CentroDeCostos al deduplicar. Los tres campos de sede son opcionales e independientes en
    // el evento: una fuente sin codigo no es candidata y una sin nombre se expone tal cual, sin
    // inventar el dato ausente (MEF-ADR-0004: derivar, nunca lanzar).
    private static IEnumerable<SedeDeFranja> FuentesDeSede(
        FranjaDepurada franja, IReadOnlyList<MarcacionDelDia> marcaciones)
    {
        if (franja.CodigoSedeProgramada is not null)
            yield return new SedeDeFranja(
                franja.CodigoSedeProgramada, franja.NombreSedeProgramada, franja.CentroDeCostosProgramado);

        foreach (var marcacion in marcaciones.Where(marcacion => PerteneceA(marcacion, franja)))
            if (marcacion.CodigoSede is not null)
                yield return new SedeDeFranja(
                    marcacion.CodigoSede, marcacion.NombreSede, marcacion.CentroDeCostos);
    }
}
