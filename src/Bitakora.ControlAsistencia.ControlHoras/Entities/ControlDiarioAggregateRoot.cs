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

    // Recalculo reactivo al final de cada Apply. Sin DetalleTurno el depurador retorna lista vacia.
    private void Depurar()
    {
        _controlesDeFranja.Clear();
        if (_ausenciaId is not null) return;
        var resultado = DepuradorDeMarcaciones.Depurar(DetalleTurno, Fecha, _marcaciones);
        _controlesDeFranja.AddRange(resultado);
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
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Fecha sale del stream ID: el ControlDiario puede nacer solo por marcacion, sin turno que la traiga.
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionAdicionada e)
    {
        Id = e.Id;
        Fecha = ExtraerFechaDeStreamId(e.Id);
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
        var yaExiste = _marcaciones.Any(m => m.TimestampNormalizado == evento.TimestampNormalizado);
        if (yaExiste) return;

        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

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
                m.CentroDeCostos))
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
