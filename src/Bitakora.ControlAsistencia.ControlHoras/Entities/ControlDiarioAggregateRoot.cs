using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-12: Aggregate root del dia de trabajo de un empleado
// Identidad: EmpleadoId + Fecha como stream ID determinista (CA-7)
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class ControlDiarioAggregateRoot : AggregateRoot
{
    // CA-6: estado que actualiza al aplicar TurnoDiarioAsignado
    public InformacionEmpleado? InformacionEmpleado { get; private set; }
    public DateOnly Fecha { get; private set; }
    public DetalleTurno? DetalleTurno { get; private set; }

    // Trazabilidad: id de la ultima solicitud que asigno un turno (CA-5)
    public Guid UltimaSolicitudId { get; private set; }

    // HU-106: lista de marcaciones adicionadas al control diario
    // CA-3: crece al adicionar una marcacion nueva
    // CA-4: idempotencia nivel 2 - duplicado por minuto normalizado se ignora
    // Expuesta como IReadOnlyList para evitar mutaciones externas; el aggregate
    // es el unico que puede agregar marcaciones via Apply.
    public IReadOnlyList<MarcacionNormalizada> Marcaciones => _marcaciones;
    private readonly List<MarcacionNormalizada> _marcaciones = [];

    // HU-123: resultado del depurador de marcaciones aplicado reactivamente en cada Apply.
    // Se recalcula completo en cada Apply(MarcacionAdicionada) y Apply(TurnoDiarioAsignado).
    // Expuesta como IReadOnlyList para que los handlers y tests externos puedan consultar.
    public IReadOnlyList<ControlFranja> ControlesDeFranja => _controlesDeFranja;
    private readonly List<ControlFranja> _controlesDeFranja = [];

    // HU-139: desglose de horas del dia, estado derivado recalculado reactivamente al final
    // de cada Apply (despues de Depurar). Sin snapshots (ADR-0021): se reconstruye aplicando
    // eventos en cada rehidratacion. Solo lectura para que los tests lo verifiquen y para que
    // el handler que publica DiaCalculado (#108) pueda leerlo.
    private DesgloseHoras _desgloseHoras = DesgloseHoras.Vacio;
    public DesgloseHoras DesgloseHoras => _desgloseHoras;

    // HU-123: recalculo reactivo invocado al final de cada Apply
    // Reemplaza completamente el contenido de _controlesDeFranja con el resultado del depurador.
    // Si DetalleTurno es null (marcacion llego antes que el turno), el depurador retorna lista vacia.
    private void Depurar()
    {
        _controlesDeFranja.Clear();
        var resultado = DepuradorDeMarcaciones.Depurar(DetalleTurno, Fecha, _marcaciones);
        _controlesDeFranja.AddRange(resultado);
    }

    // HU-139: recalculo reactivo invocado al final de cada Apply, despues de Depurar().
    // Orden obligatorio: Depurar() puebla _controlesDeFranja; este metodo consolida el
    // desglose del dia a partir de esas franjas ya depuradas.
    // Calcula el DesgloseFranja de cada franja no anomala (CalcularDesglose retorna null
    // para las anomalas) y cuenta las anomalas para reportarlas en el consolidado.
    private void RecalcularDesgloseHoras()
    {
        var desgloses = _controlesDeFranja
            .Select(cf => cf.CalcularDesglose(Fecha, CalendarioFestivosColombia.EsFestivo))
            .Where(d => d is not null)
            .Cast<DesgloseFranja>()
            .ToList();
        var anomalas = _controlesDeFranja.Count(cf => cf.EsAnomala);
        _desgloseHoras = ConsolidadorDesgloseHoras.Consolidar(desgloses, anomalas);
    }

    // CA-7: stream ID determinista: "{EmpleadoId}:{Fecha:yyyy-MM-dd}"
    // CA-8: dos mensajes con mismo EmpleadoId+Fecha comparten el mismo stream
    public static string ComputarStreamId(string empleadoId, DateOnly fecha) =>
        $"{empleadoId}:{fecha:yyyy-MM-dd}";

    // CA-6: actualiza estado interno al aplicar el evento
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(TurnoDiarioAsignado e)
    {
        Id = e.Id;
        InformacionEmpleado = e.InformacionEmpleado;
        Fecha = e.Fecha;
        DetalleTurno = e.DetalleTurno;
        UltimaSolicitudId = e.SolicitudId;
        Depurar();
        RecalcularDesgloseHoras();
    }

    // Factory: crea el aggregate con el evento en _uncommittedEvents para StartStream
    internal static ControlDiarioAggregateRoot Iniciar(TurnoDiarioAsignado evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // Agrega un nuevo turno al aggregate existente (caso CA-4)
    internal void AsignarTurno(TurnoDiarioAsignado evento)
    {
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // HU-106: Apply que agrega la marcacion a la lista
    // Apply solo proyecta estado; la deteccion de duplicado vive en AdicionarMarcacion (CA-4).
    // Cuando el aggregate nace desde Iniciar(MarcacionAdicionada), este Apply asigna el Id del stream.
    // HU-108: Fecha se deriva del stream ID (CA-7 codifica EmpleadoId+Fecha) para que el
    //          DiaCalculado emitido tras el recalculo lleve la fecha correcta incluso cuando
    //          el ControlDiario nace solo por marcacion (sin TurnoDiarioAsignado previo).
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionAdicionada e)
    {
        Id = e.Id;
        Fecha = ExtraerFechaDeStreamId(e.Id);
        _marcaciones.Add(new MarcacionNormalizada(e.TimestampNormalizado, e.TipoMarcacion));
        Depurar();
        RecalcularDesgloseHoras();
    }

    // HU-108: stream ID tiene formato "{EmpleadoId}:{Fecha:yyyy-MM-dd}" (CA-7).
    // Parsea la porcion final como DateOnly para hidratar Fecha cuando no hay TurnoDiarioAsignado.
    private static DateOnly ExtraerFechaDeStreamId(string streamId)
    {
        var separador = streamId.LastIndexOf(':');
        var fechaTexto = streamId[(separador + 1)..];
        return DateOnly.ParseExact(fechaTexto, "yyyy-MM-dd");
    }

    // HU-106: segundo camino de creacion del ControlDiario, sin turno asignado
    // CA-5: si no existe ControlDiario para la fecha, se crea con este factory
    // CA-6: InformacionEmpleado y DetalleTurno quedan null
    internal static ControlDiarioAggregateRoot Iniciar(MarcacionAdicionada evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // HU-106: agrega una marcacion al aggregate existente
    // CA-4: idempotencia nivel 2 - si ya existe una marcacion con el mismo minuto
    //        normalizado, se ignora silenciosamente sin emitir evento ni excepcion
    internal void AdicionarMarcacion(MarcacionAdicionada evento)
    {
        var yaExiste = _marcaciones.Any(m => m.TimestampNormalizado == evento.TimestampNormalizado);
        if (yaExiste) return;

        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // HU-108: construye el evento DiaCalculado desde el estado actual del aggregate.
    // Tell-don't-Ask: el aggregate es duenio del estado y entrega el evento ya empaquetado al handler.
    // Issue #183: el payload viaja plano (HorasDiscriminadas). El desglose rico consolidado por
    //         RecalcularDesgloseHoras() al final de cada Apply (_desgloseHoras, fresco aqui porque los
    //         handlers invocan CrearDiaCalculado() despues del Apply) se discrimina a si mismo via
    //         Discriminar(). Ya no se mapean los ControlFranja a un DTO rico: el contrato pierde la
    //         senal de anomalia a proposito (riesgo aceptado, diferido al flujo de aprobacion).
    // Stub: lo implementa la fase verde -> new DiaCalculado(InformacionEmpleado, Fecha, _desgloseHoras.Discriminar()).
    public DiaCalculado CrearDiaCalculado() => throw new NotImplementedException();
}
