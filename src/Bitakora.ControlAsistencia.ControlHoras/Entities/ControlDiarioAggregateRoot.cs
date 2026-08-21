using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-12: Aggregate root del dia de trabajo de un colaborador
// Identidad: CodigoColaborador + Fecha como stream ID determinista (CA-7)
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
// Issue #322: InformacionEmpleado y DetalleTurno cambiaron de tipo (ColaboradorProgramado/
// TurnoDiario, propios de ControlHoras.DomainEvents) sin tocar los nombres de las propiedades.
// Issue #401: InformacionEmpleado paso a InformacionColaborador (termino proscrito por #330).
public partial class ControlDiarioAggregateRoot : AggregateRoot
{
    // CA-6: estado que actualiza al aplicar TurnoDiarioAsignado
    public ColaboradorProgramado? InformacionColaborador { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TurnoDiario? DetalleTurno { get; private set; }

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

    // CA-ADR-0031: prefijo por iniciales -- disjunta este stream del futuro DiaCalculado ("dc:"), que
    // comparte la identidad logica colaborador+fecha en el mismo store. La fecha va en ISO 8601 basico
    // porque no aporta ':' propios: asi Split(SeparadorStreamId) devuelve siempre los 3 componentes.
    // Los tres valores son el contrato de identidad de todo stream ya escrito -- cambiar cualquiera
    // exige migracion. InvariantCulture: una culture con calendario no gregoriano (ar-SA) rendiria
    // otro ano en la clave.
    private const string PrefijoStreamId = "cd";
    private const char SeparadorStreamId = ':';
    private const string FormatoFechaStreamId = "yyyyMMdd";

    // CA-8: dos mensajes con mismo CodigoColaborador+Fecha comparten el mismo stream
    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
    {
        var fechaBasica = fecha.ToString(FormatoFechaStreamId, CultureInfo.InvariantCulture);
        return $"{PrefijoStreamId}{SeparadorStreamId}{codigoColaborador}{SeparadorStreamId}{fechaBasica}";
    }

    // CA-6: actualiza estado interno al aplicar el evento
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
    // HU-108: Fecha se deriva del stream ID (CA-7 codifica CodigoColaborador+Fecha) para que el
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

    // HU-108: hidrata Fecha cuando el ControlDiario nace solo por marcacion, sin TurnoDiarioAsignado
    // que la traiga. La anatomia de CA-ADR-0031 garantiza 3 componentes y la fecha es siempre el ultimo.
    private static DateOnly ExtraerFechaDeStreamId(string streamId)
    {
        var partes = streamId.Split(SeparadorStreamId);
        return DateOnly.ParseExact(partes[^1], FormatoFechaStreamId, CultureInfo.InvariantCulture);
    }

    // HU-106: segundo camino de creacion del ControlDiario, sin turno asignado
    // CA-5: si no existe ControlDiario para la fecha, se crea con este factory
    // CA-6: InformacionColaborador y DetalleTurno quedan null
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

    // Issue #421: renombra CrearDiaCalculado() -- reclasifica el evento como IPrivateEvent
    // (DiaDepurado, familia lexica de la maquina) y agrega CodigoColaborador top-level, siempre
    // presente aunque InformacionColaborador sea null (corrige el defecto latente que impedia al
    // consumidor de #425 construir "dc:{codigo}:{yyyyMMdd}" cuando el dia nace solo por marcacion).
    // El mapeo a ResumenColaborador (con Identificacion compuesta "{Tipo}-{Numero}" y
    // NombreCompleto = Nombres + " " + Apellidos) es la logica de negocio nueva de este issue --
    // stub a proposito para la fase roja del pipeline (test-writer nunca implementa produccion real).
    public DiaDepurado CrearDiaDepurado() => throw new NotImplementedException();
}
