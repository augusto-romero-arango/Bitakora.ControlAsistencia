using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #425: aggregate root del expediente de aprobacion de un dia de trabajo. Recibe cada foto
// de DiaDepurado (traducida a tipos de dominio por el handler) y mantiene los valores
// provisionales del dia -- el juicio humano del Aprobador vive separado del calculo automatico
// de ControlDiario (sesion 2026-08-17). Vivira el ciclo Provisional -> Aprobado; este issue
// construye unicamente el receptor.
// CA-ADR-0031: prefijo "dc" (iniciales de DiaCalculado) disjunta este stream de ControlDiario
// ("cd"), que comparte la identidad logica colaborador+fecha en el mismo store. La fecha va en
// ISO 8601 basico por el mismo motivo que ControlDiarioAggregateRoot: no aporta ':' propios.
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere.
public partial class DiaCalculadoAggregateRoot : AggregateRoot
{
    // Ciclo de vida del aggregate, no campo informativo (issue #425) -- se expone como propiedad
    // publica de solo lectura, a diferencia de los valores provisionales de la foto (privados, mas
    // abajo): sin ninguna propiedad publica el aggregate no seria verificable por el harness
    // (CommandHandlerTestBase.And<>), y es consistente con el resto del dominio
    // (ControlDiarioAggregateRoot expone Fecha/DetalleTurno/InformacionColaborador del mismo modo).
    public EstadoDiaCalculado Estado { get; private set; }

    // Valores provisionales de la ultima foto recibida -- NUNCA expuestos como propiedades sueltas
    // (MEF-ADR-0012, Tell-don't-Ask): el aggregate no da estado crudo para calculo externo. Solo
    // Apply los reemplaza; ningun consumidor externo los lee campo a campo.
    private string _codigoColaborador = string.Empty;
    private DateOnly _fecha;
    private ResumenColaborador? _colaborador;
    private string? _nombreTurno;
    private IReadOnlyList<FranjaDepurada> _franjas = [];
    private IReadOnlyList<MarcacionDelDia> _marcaciones = [];
    private HorasDiscriminadas? _horasDiscriminadas;

    private const string PrefijoStreamId = "dc";
    private const char SeparadorStreamId = ':';
    private const string FormatoFechaStreamId = "yyyyMMdd";

    // Dos mensajes con el mismo CodigoColaborador+Fecha convergen sobre el mismo stream
    // (MEF-ADR-0026: convergencia de eventos, riesgo de escritura concurrente declarado en el issue).
    public static string ComputarStreamId(string codigoColaborador, DateOnly fecha)
    {
        var fechaBasica = fecha.ToString(FormatoFechaStreamId, CultureInfo.InvariantCulture);
        return $"{PrefijoStreamId}{SeparadorStreamId}{codigoColaborador}{SeparadorStreamId}{fechaBasica}";
    }

    // MEF-ADR-0004: Apply no lanza -- recibir una depuracion no falla por regla de negocio.
    // Siempre reemplaza los valores provisionales con la ultima foto, sin comparar contra el
    // estado previo (sin deduplicacion de ningun tipo, decision de sesion 2026-08-24).
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

    // Factory: crea el aggregate con el evento en _uncommittedEvents para StartStream.
    // CA-1/CA-3: todo dia que llega nace, anomalo incluido -- sin comparacion previa de ningun tipo.
    internal static DiaCalculadoAggregateRoot Iniciar(DepuracionDiaRecibida evento)
    {
        var dia = new DiaCalculadoAggregateRoot();
        dia._uncommittedEvents.Add(evento);
        dia.Apply(evento);
        return dia;
    }

    // CA-2/CA-4: toda foto que llega a un dia ya existente se agrega al mismo stream, siempre --
    // sin comparar contra el estado previo. Convergencia de eventos sobre el mismo aggregate
    // (MEF-ADR-0026): la topologia de fan-in, si aplica, la resuelve la Function que invoca esto.
    internal void RecibirDepuracion(DepuracionDiaRecibida evento)
    {
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }
}
