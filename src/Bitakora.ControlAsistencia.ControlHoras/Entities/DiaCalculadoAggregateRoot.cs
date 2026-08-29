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

    // Issue #482: senal publica derivada de la ultima foto -- la consumira la invariante de
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
    internal void RecibirDepuracion(DepuracionDiaRecibida evento)
    {
        _uncommittedEvents.Add(evento);
        Apply(evento);
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
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null)
        };

    // Igualdad EXACTA de Timestamp, derivada una sola vez aqui: ningun cliente la recalcula.
    private static bool EsUsada(MarcacionDelDia marcacion, IReadOnlyList<FranjaDepurada> franjas) =>
        franjas.Any(franja => franja.Entrada == marcacion.Timestamp || franja.Salida == marcacion.Timestamp);

    // Issue #482: correlacion marcacion <-> franja para el conflicto de sede -- misma igualdad
    // EXACTA de Timestamp que EsUsada, pero acotada a las marcaciones de ESTA franja (entrada y
    // salida pueden venir de dispositivos de sedes distintas, CA-3).
    private static IEnumerable<MarcacionDelDia> MarcacionesUsadasPor(
        FranjaDepurada franja, IReadOnlyList<MarcacionDelDia> marcaciones) =>
        marcaciones.Where(marcacion =>
            marcacion.Timestamp == franja.Entrada || marcacion.Timestamp == franja.Salida);

    // Politica en firme (glosario "Conflicto de sede"): SIN DEFAULT. Candidatas = sede programada
    // (si existe) + sedes marcadas de las marcaciones usadas por esta franja (si estan estampadas),
    // deduplicadas por Codigo. Una unica sede entre las fuentes -> sede efectiva, con el CC
    // estampado en la marcacion prevaleciendo sobre el programado cuando ambos coinciden en sede
    // (CA-5: la ultima fuente agregada -- marcada -- gana el CC, nunca un lookup al maestro). Dos o
    // mas codigos distintos -> conflicto, sin sede efectiva, todas las candidatas expuestas (CA-2/CA-3).
    private static (SedeDeFranja? Efectiva, bool EnConflicto, IReadOnlyList<SedeDeFranja> Candidatas)
        DerivarSedeDeFranja(FranjaDepurada franja, IReadOnlyList<MarcacionDelDia> marcaciones)
    {
        var fuentes = new List<SedeDeFranja>();
        if (franja.CodigoSedeProgramada is not null)
            fuentes.Add(new SedeDeFranja(
                franja.CodigoSedeProgramada, franja.NombreSedeProgramada!, franja.CentroDeCostosProgramado));

        fuentes.AddRange(MarcacionesUsadasPor(franja, marcaciones)
            .Where(marcacion => marcacion.CodigoSede is not null)
            .Select(marcacion => new SedeDeFranja(
                marcacion.CodigoSede!, marcacion.NombreSede!, marcacion.CentroDeCostos)));

        var codigosDistintos = fuentes.Select(fuente => fuente.Codigo).Distinct().ToList();

        if (codigosDistintos.Count == 0)
            return (null, false, []);

        if (codigosDistintos.Count == 1)
            return (fuentes.Last(fuente => fuente.Codigo == codigosDistintos[0]), false, []);

        return (null, true, fuentes.DistinctBy(fuente => fuente.Codigo).ToList());
    }
}
