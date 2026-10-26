using System.Collections.ObjectModel;
using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Cosmos.EventSourcing.Abstractions;
// Alias de tipo: estos nombres existen homonimos en ControlHoras.DomainEvents (payload por rol,
// MEF-ADR-0039 decision #6); este archivo produce DiaDepurado, asi que usa los del bus.
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using FranjaDepurada = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.FranjaDepurada;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// partial para admitir una clase Mensajes en archivo separado (ADR-0015).
public partial class ControlDiarioAggregateRoot : AggregateRoot
{
    public ColaboradorProgramado? InformacionColaborador { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TurnoDiario? DetalleTurno { get; private set; }

    public Guid UltimaSolicitudId { get; private set; }

    public IReadOnlyList<MarcacionNormalizada> Marcaciones => _marcaciones;
    private readonly List<MarcacionNormalizada> _marcaciones = [];

    public IReadOnlyList<ControlFranja> ControlesDeFranja => _controlesDeFranja;
    private readonly List<ControlFranja> _controlesDeFranja = [];

    // Sin snapshots (ADR-0021): se reconstruye aplicando eventos en cada rehidratacion.
    private DesgloseHoras _desgloseHoras = DesgloseHoras.Vacio;
    public DesgloseHoras DesgloseHoras => _desgloseHoras;

    // CA-ADR-0036 decision 3: la ausencia cubre el turno sin reemplazarlo; DetalleTurno se conserva.
    private Guid? _ausenciaId;
    private string? _motivoAusencia;

    // El bus no garantiza orden: la cancelacion puede llegar antes que su ausencia, que entonces se
    // ignora al llegar. Por eso el dia recuerda toda AusenciaId cancelada, este o no vigente.
    private readonly HashSet<Guid> _ausenciasCanceladas = [];

    // Limites de cobertura informados por los vecinos; null = el vecino no tiene cobertura.
    private DateTime? _finCoberturaVispera;
    private DateTime? _inicioCoberturaDiaSiguiente;
    private DateTime? _finCoberturaEfectiva;
    private IReadOnlyDictionary<DateTime, PertenenciaMarcacion> _pertenencias =
        ReadOnlyDictionary<DateTime, PertenenciaMarcacion>.Empty;

    // Recalculo reactivo al final de cada Apply.
    private void Depurar()
    {
        _controlesDeFranja.Clear();
        _pertenencias = ReadOnlyDictionary<DateTime, PertenenciaMarcacion>.Empty;
        var vecinas = new CoberturasVecinas(
            _finCoberturaVispera, _inicioCoberturaDiaSiguiente, Fecha, ResolucionesVigentes());
        if (SinJornada)
        {
            _finCoberturaEfectiva = null;
            _pertenencias = _marcaciones
                .Select(m => m.TimestampNormalizado)
                .Distinct()
                .Where(vecinas.Excluye)
                .ToDictionary(i => i, _ => PertenenciaMarcacion.DeTurnoVecino);
            return;
        }
        var resultado = DepuradorDeMarcaciones.Depurar(DetalleTurno, Fecha, _marcaciones, vecinas);
        _controlesDeFranja.AddRange(resultado);
        _pertenencias = DepuradorDeMarcaciones.Clasificar(DetalleTurno, Fecha, _marcaciones, vecinas);
        _finCoberturaEfectiva = DepuradorDeMarcaciones
            .CoberturaEfectiva(DetalleTurno, Fecha, _marcaciones, vecinas)?.Fin;
    }

    // Descanso, sin programar o con ausencia: no depura horas, solo clasifica marcaciones de vecinos.
    private bool SinJornada => _ausenciaId is not null || DetalleTurno?.CoberturaPosible(Fecha) is null;

    internal bool ActualizarCierreDeLaVispera(ControlDiarioAggregateRoot vispera) =>
        throw new NotImplementedException();

    public void Apply(CierreDeLaVisperaActualizado e) => throw new NotImplementedException();

    // true si emitio el evento.
    internal bool ActualizarCoberturaVecina(ControlDiarioAggregateRoot vecino)
    {
        var lado = LadoDe(vecino);
        var (limite, actual) = lado == DiaVecino.Vispera
            ? (vecino.FinDeSuCobertura(), _finCoberturaVispera)
            : (vecino.InicioDeSuCobertura(), _inicioCoberturaDiaSiguiente);
        if (actual == limite) return false;
        // El primer limite solo se informa si el vecino invade de verdad la cobertura de este dia (o, sin
        // jornada, su dia calendario); un limite ya informado siempre se actualiza.
        if (actual is null && !RegistraPrimerLimite(lado, limite, vecino)) return false;

        var evento = CoberturaVecinaActualizada.Crear(Id, lado, limite);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return true;
    }

    private bool RegistraPrimerLimite(DiaVecino lado, DateTime? limite, ControlDiarioAggregateRoot vecino) =>
        SinJornada ? AlcanzaSuDiaCalendario(lado, limite) : Invade(DetalleTurno!, lado, limite, vecino);

    // Sin jornada, el dia solo clasifica sus propias marcaciones: un limite fuera de su dia calendario no excluye ninguna.
    private bool AlcanzaSuDiaCalendario(DiaVecino lado, DateTime? limite)
    {
        if (limite is not { } instante) return false;

        var inicioDelDia = Fecha.ToDateTime(TimeOnly.MinValue);
        return lado == DiaVecino.Vispera ? instante > inicioDelDia : instante < inicioDelDia.AddDays(1);
    }

    private bool Invade(TurnoDiario turno, DiaVecino lado, DateTime? limite, ControlDiarioAggregateRoot vecino)
    {
        if (limite is not { } instante || turno.CoberturaPosible(Fecha) is not { } propia) return false;
        if (vecino.DetalleTurno?.SeSolapaCon(vecino.Fecha, turno, Fecha) != false) return false;

        return lado == DiaVecino.Vispera ? instante > propia.Inicio : instante < propia.Fin;
    }

    private DateTime? InicioDeSuCobertura() =>
        _ausenciaId is null ? DetalleTurno?.Cobertura(Fecha)?.Inicio : null;

    private DateTime? FinDeSuCobertura() => _ausenciaId is null ? _finCoberturaEfectiva : null;

    private readonly Dictionary<DiaVecino, IReadOnlyList<MarcacionDecidida>> _resoluciones = [];

    private IReadOnlyDictionary<DateTime, DateOnly> ResolucionesVigentes() =>
        _resoluciones.Values
            .SelectMany(r => r)
            .GroupBy(m => m.Timestamp)
            .ToDictionary(g => g.Key, g => g.Last().Fecha);

    public void Apply(AmbiguedadDeMarcacionesResuelta e)
    {
        Id = e.Id;
        _resoluciones[e.Vecino] = e.Marcaciones;
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Sin resoluciones: repetir un reparto debe seguir viendo el solape.
    internal IReadOnlyDictionary<DateTime, DiaVecino> MarcacionesEnDisputa()
    {
        if (SinJornada) return new Dictionary<DateTime, DiaVecino>();
        var naturales = new CoberturasVecinas(_finCoberturaVispera, _inicioCoberturaDiaSiguiente);
        return DepuradorDeMarcaciones.Clasificar(DetalleTurno, Fecha, _marcaciones, naturales)
            .Where(p => p.Value == PertenenciaMarcacion.Ambigua)
            .ToDictionary(
                p => p.Key,
                p => p.Key < _finCoberturaVispera ? DiaVecino.Vispera : DiaVecino.DiaSiguiente);
    }

    internal DateOnly FechaDe(DiaVecino vecino) =>
        Fecha.AddDays(vecino == DiaVecino.Vispera ? -1 : 1);

    internal ResultadoResolucionAmbiguedad ValidarReparto(IReadOnlyList<DecisionDeMarcacion> decisiones)
    {
        var disputas = MarcacionesEnDisputa();
        if (decisiones.Any(d => !disputas.ContainsKey(d.Marcacion)))
            return ResultadoResolucionAmbiguedad.MarcacionNoAmbigua;
        if (disputas.Keys.Any(m => decisiones.All(d => d.Marcacion != m)))
            return ResultadoResolucionAmbiguedad.RepartoIncompleto;

        var fechaValida = decisiones.All(d => d.Fecha == Fecha || d.Fecha == FechaDe(disputas[d.Marcacion]));
        var sinContradiccion = decisiones.GroupBy(d => d.Marcacion).All(g => g.Select(d => d.Fecha).Distinct().Count() == 1);
        return fechaValida && sinContradiccion
            ? ResultadoResolucionAmbiguedad.Valido
            : ResultadoResolucionAmbiguedad.FechaNoValida;
    }

    // true si emitio el evento; el mismo reparto ya vigente es un no-op sin evento.
    internal bool Resolver(DiaVecino vecino, IReadOnlyList<MarcacionDecidida> marcaciones)
    {
        if (_resoluciones.TryGetValue(vecino, out var vigente) && vigente.SequenceEqual(marcaciones))
            return false;

        var evento = AmbiguedadDeMarcacionesResuelta.Crear(Id, vecino, marcaciones);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return true;
    }

    public void Apply(ResolucionDeAmbiguedadDescartada e)
    {
        Id = e.Id;
        _resoluciones.Remove(e.Vecino);
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Un cambio real de plan propio descarta las resoluciones con todos los vecinos.
    private void DescartarResoluciones()
    {
        foreach (var vecino in _resoluciones.Keys.ToList())
            Descartar(vecino);
    }

    private void Descartar(DiaVecino vecino)
    {
        var evento = ResolucionDeAmbiguedadDescartada.Crear(Id, vecino);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // El vecino que cambio de plan ya descarto su mitad del par; este dia descarta la suya.
    // true si emitio el evento.
    internal bool AlinearResolucionCon(ControlDiarioAggregateRoot vecino)
    {
        var lado = LadoDe(vecino);
        if (!_resoluciones.ContainsKey(lado) || vecino._resoluciones.ContainsKey(vecino.LadoDe(this))) return false;

        Descartar(lado);
        return true;
    }

    private DiaVecino LadoDe(ControlDiarioAggregateRoot vecino) =>
        vecino.Fecha < Fecha ? DiaVecino.Vispera : DiaVecino.DiaSiguiente;

    public void Apply(CoberturaVecinaActualizada e)
    {
        if (e.Vecino == DiaVecino.Vispera) _finCoberturaVispera = e.Limite;
        else _inicioCoberturaDiaSiguiente = e.Limite;
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Orden obligatorio: va despues de Depurar(), que puebla _controlesDeFranja.
    private void RecalcularDesgloseHoras()
    {
        if (_ausenciaId is not null)
        {
            _desgloseHoras = DesgloseHoras.Vacio;
            return;
        }
        var desgloses = _controlesDeFranja
            .Select(cf => cf.CalcularDesglose(Fecha, CalendarioFestivosColombia.EsFestivo))
            .Where(d => d is not null)
            .Cast<DesgloseFranja>()
            .ToList();
        var anomalas = _controlesDeFranja.Count(cf => cf.EsAnomala);
        _desgloseHoras = ConsolidadorDesgloseHoras.Consolidar(desgloses, anomalas);
    }

    // CA-ADR-0031: prefijo por iniciales -- disjunta este stream del futuro DiaCalculado ("dc:"), que
    // comparte la identidad logica colaborador+fecha en el mismo store. La fecha va en ISO 8601 basico
    // porque no aporta ':' propios: asi Split(SeparadorStreamId) devuelve siempre los 3 componentes.
    // Los tres valores son el contrato de identidad de todo stream ya escrito -- cambiar cualquiera
    // exige migracion. InvariantCulture: una culture con calendario no gregoriano (ar-SA) rendiria
    // otro ano en la clave.
    private const string PrefijoStreamId = "cd";
    private const char SeparadorStreamId = ':';
    private const string FormatoFechaStreamId = "yyyyMMdd";

    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
    {
        var fechaBasica = fecha.ToString(FormatoFechaStreamId, CultureInfo.InvariantCulture);
        return $"{PrefijoStreamId}{SeparadorStreamId}{codigoColaborador}{SeparadorStreamId}{fechaBasica}";
    }

    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(TurnoDiarioAsignado e)
    {
        Id = e.Id;
        InformacionColaborador = e.InformacionColaborador;
        Fecha = e.Fecha;
        DetalleTurno = e.DetalleTurno;
        UltimaSolicitudId = e.SolicitudId;
        Depurar();
        RecalcularDesgloseHoras();
    }

    internal static ControlDiarioAggregateRoot Iniciar(TurnoDiarioAsignado evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    internal void AsignarTurno(TurnoDiarioAsignado evento)
    {
        var cambiaElPlan = !Equals(DetalleTurno, evento.DetalleTurno);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        if (cambiaElPlan) DescartarResoluciones();
    }

    // Fecha sale del stream ID: el ControlDiario puede nacer solo por marcacion, sin turno que la traiga.
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionAdicionada e)
    {
        Id = e.Id;
        Fecha = ExtraerFechaDeStreamId(e.Id);
        // Dos repartos concurrentes pueden persistir la misma copia: el replay conserva una sola.
        if (TieneMarcacionEn(e.TimestampNormalizado)) return;
        _marcaciones.Add(new MarcacionNormalizada(e.TimestampNormalizado, e.TipoMarcacion, e.DispositivoId));
        Depurar();
        RecalcularDesgloseHoras();
    }

    // La anatomia de CA-ADR-0031 garantiza 3 componentes y la fecha es siempre el ultimo.
    private static DateOnly ExtraerFechaDeStreamId(string streamId)
    {
        var partes = streamId.Split(SeparadorStreamId);
        return DateOnly.ParseExact(partes[^1], FormatoFechaStreamId, CultureInfo.InvariantCulture);
    }

    // Fuente unica del CodigoColaborador del aggregate: el stream ID, que ambos Apply asignan a Id.
    // InformacionColaborador NO sirve -- queda null cuando el dia nace solo por marcacion. La anatomia
    // de CA-ADR-0031 garantiza 3 componentes y el codigo es siempre el del medio.
    private static string ExtraerCodigoColaboradorDeStreamId(string streamId) =>
        streamId.Split(SeparadorStreamId)[1];

    internal static ControlDiarioAggregateRoot Iniciar(MarcacionAdicionada evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // Duplicado por minuto normalizado: no-op silencioso, sin evento ni excepcion.
    internal void AdicionarMarcacion(MarcacionAdicionada evento)
    {
        if (TieneMarcacionEn(evento.TimestampNormalizado)) return;

        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    private bool TieneMarcacionEn(DateTime timestampNormalizado) =>
        _marcaciones.Any(m => m.TimestampNormalizado == timestampNormalizado);

    // Copia (con su sede) las marcaciones del vecino que caen en la cobertura posible del turno de
    // este dia. true si emitio eventos.
    internal bool RecogerDe(ControlDiarioAggregateRoot vecino)
    {
        var cobertura = DetalleTurno?.CoberturaPosible(Fecha);
        if (cobertura is null) return false;

        var eventosAntes = _uncommittedEvents.Count;
        foreach (var origen in vecino.MarcacionesDeSuDiaCalendario()
                     .Where(m => cobertura.Contiene(m.TimestampNormalizado)))
        {
            AdicionarMarcacion(new MarcacionAdicionada(
                Id, ExtraerCodigoColaboradorDeStreamId(Id), origen.TimestampNormalizado,
                origen.TipoMarcacion, origen.DispositivoId));

            if (origen is { CodigoSede: { } codigoSede, NombreSede: { } nombreSede })
                EstamparSede(new SedeDeMarcacionIdentificada(
                    Id, origen.TimestampNormalizado, origen.DispositivoId,
                    codigoSede, nombreSede, origen.CentroDeCostos));
        }

        return _uncommittedEvents.Count > eventosAntes;
    }

    // Las copias recibidas de otro dia quedan fuera: nunca se re-reparten.
    private IEnumerable<MarcacionNormalizada> MarcacionesDeSuDiaCalendario() =>
        _marcaciones.Where(m => DateOnly.FromDateTime(m.TimestampNormalizado) == Fecha);

    // Proyecta el estampado sobre la marcacion correlacionada. Sin coincidencia no lanza
    // (MEF-ADR-0004): Apply solo proyecta estado.
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods().
    public void Apply(SedeDeMarcacionIdentificada e)
    {
        var indice = _marcaciones.FindIndex(m => CorrespondeA(m, e));
        if (indice < 0) return;

        _marcaciones[indice] = _marcaciones[indice] with
        {
            CodigoSede = e.CodigoSede,
            NombreSede = e.NombreSede,
            CentroDeCostos = e.CentroDeCostos
        };
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Declina con resultado (CA-ADR-0030) en vez de que el handler interrogue
    // Marcaciones para decidir (Tell-don't-Ask, MEF-ADR-0012). El handler traduce
    // MarcacionNoEncontrada a la excepcion que dispara el retry del bus (CA-3); SedeYaEstampada es
    // el no-op de CA-4, sin evento nuevo ni re-publicacion.
    internal ResultadoEstampadoSede EstamparSede(SedeDeMarcacionIdentificada evento)
    {
        var marcacion = _marcaciones.Find(m => CorrespondeA(m, evento));
        if (marcacion is null) return ResultadoEstampadoSede.MarcacionNoEncontrada;

        if (marcacion.CodigoSede == evento.CodigoSede
            && marcacion.NombreSede == evento.NombreSede
            && marcacion.CentroDeCostos == evento.CentroDeCostos)
            return ResultadoEstampadoSede.SedeYaEstampada;

        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoEstampadoSede.Estampada;
    }

    // Unica correlacion marcacion <-> estampado: la marcacion no tiene id propio, asi que el par
    // TimestampNormalizado + DispositivoId es la clave (MarcacionAdicionada ya guarda ambos).
    private static bool CorrespondeA(MarcacionNormalizada marcacion, SedeDeMarcacionIdentificada evento) =>
        marcacion.TimestampNormalizado == evento.TimestampNormalizado
        && marcacion.DispositivoId == evento.DispositivoId;

    // Quita el plan sin borrar marcaciones ni sedes estampadas: sin DetalleTurno, Depurar() deja
    // las franjas vacias y las marcaciones quedan crudas, sin desglose.
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods().
    public void Apply(TurnoDiarioCancelado e)
    {
        DetalleTurno = null;
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Declina con resultado (CA-ADR-0030) en vez de que el handler interrogue DetalleTurno para
    // decidir el no-op de un stream sin turno asignado (Tell-don't-Ask, MEF-ADR-0012).
    internal ResultadoCancelacionTurno CancelarTurno(TurnoDiarioCancelado evento)
    {
        if (DetalleTurno is null) return ResultadoCancelacionTurno.SinTurnoAsignado;

        _uncommittedEvents.Add(evento);
        Apply(evento);
        DescartarResoluciones();
        return ResultadoCancelacionTurno.Cancelado;
    }

    public void Apply(AusenciaDiariaAsignada e)
    {
        Id = e.Id;
        InformacionColaborador = e.Colaborador;
        Fecha = e.Fecha;
        _ausenciaId = e.AusenciaId;
        _motivoAusencia = e.Motivo;
        Depurar();
        RecalcularDesgloseHoras();
    }

    internal static ControlDiarioAggregateRoot Iniciar(AusenciaDiariaAsignada evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // Ya cancelada = llego despues de su cancelacion: se ignora. Misma AusenciaId = reentrega del
    // bus: no-op. Otra distinta reemplaza a la vigente.
    internal ResultadoAsignarAusencia AsignarAusencia(AusenciaDiariaAsignada evento)
    {
        if (_ausenciasCanceladas.Contains(evento.AusenciaId)) return ResultadoAsignarAusencia.Ignorada;
        if (_ausenciaId == evento.AusenciaId) return ResultadoAsignarAusencia.SinCambios;

        _uncommittedEvents.Add(evento);
        Apply(evento);
        DescartarResoluciones();
        return ResultadoAsignarAusencia.Asignada;
    }

    public void Apply(CancelacionAusenciaDiariaRegistrada e)
    {
        Id = e.Id;
        Fecha = e.Fecha;
        _ausenciasCanceladas.Add(e.AusenciaId);
        if (_ausenciaId == e.AusenciaId)
        {
            _ausenciaId = null;
            _motivoAusencia = null;
        }
        Depurar();
        RecalcularDesgloseHoras();
    }

    internal static ControlDiarioAggregateRoot Iniciar(CancelacionAusenciaDiariaRegistrada evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // Solo libera el dia si la cancelada es la vigente; si no, la recuerda sin cambiar el dia.
    internal ResultadoCancelarAusencia CancelarAusencia(CancelacionAusenciaDiariaRegistrada evento)
    {
        if (_ausenciasCanceladas.Contains(evento.AusenciaId)) return ResultadoCancelarAusencia.SinCambios;

        var libera = _ausenciaId == evento.AusenciaId;
        _uncommittedEvents.Add(evento);
        Apply(evento);
        if (libera) DescartarResoluciones();
        return libera ? ResultadoCancelarAusencia.Liberado : ResultadoCancelarAusencia.CancelacionRecordada;
    }

    // Tell-don't-Ask: el aggregate entrega el evento ya empaquetado al handler, que no lo arma campo
    // a campo. Debe invocarse DESPUES del Apply: lee DesgloseHoras, que RecalcularDesgloseHoras()
    // refresca al final de cada uno.
    public DiaDepurado CrearDiaDepurado() =>
        new(
            ExtraerCodigoColaboradorDeStreamId(Id),
            Fecha,
            CrearResumenColaborador(),
            _ausenciaId is null ? DetalleTurno?.Nombre : null,
            _controlesDeFranja.Select(CrearFranjaDepurada).ToList(),
            CrearMarcacionesCronologicas(),
            DesgloseHoras.Discriminar(),
            _motivoAusencia);

    // El orden ascendente es contrato del evento, no un reflejo de _marcaciones: el aggregate las
    // guarda por orden de llegada, que puede no ser cronologico.
    private List<MarcacionDelDia> CrearMarcacionesCronologicas() =>
        _marcaciones
            .OrderBy(m => m.TimestampNormalizado)
            .Select(m => new MarcacionDelDia(
                m.TimestampNormalizado,
                m.TipoMarcacion,
                m.CodigoSede,
                m.NombreSede,
                m.CentroDeCostos,
                _pertenencias.TryGetValue(m.TimestampNormalizado, out var pertenencia)
                    ? pertenencia.ToString()
                    : null))
            .ToList();

    private static FranjaDepurada CrearFranjaDepurada(ControlFranja controlFranja) =>
        new(
            controlFranja.Programada.HoraInicio,
            controlFranja.Programada.HoraFin,
            controlFranja.Programada.DiaOffsetFin,
            controlFranja.Entrada,
            controlFranja.Salida,
            controlFranja.EsAnomala,
            controlFranja.Programada.Sede?.Id,
            controlFranja.Programada.Sede?.Nombre,
            controlFranja.Programada.Sede?.CentroDeCostos);

    // Mapeo campo a campo entre dos islas: vive aqui porque el aggregate esta en el Function App, el
    // unico proyecto que ve las tres (CA-ADR-0029 decision #5). ColaboradorProgramado ya trae la
    // terna compuesta desde el origen de la cadena -- aqui no se compone nada.
    private ResumenColaborador? CrearResumenColaborador() =>
        InformacionColaborador is null
            ? null
            : new ResumenColaborador(
                InformacionColaborador.Identificacion,
                InformacionColaborador.CodigoColaborador,
                InformacionColaborador.NombreCompleto);
}
