using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Cosmos.EventSourcing.Abstractions;
// Alias, no using de namespace: ReadModels.ControlHoras.FranjaDepurada/MarcacionDelDia son el
// tercer espejo del mismo termino (MEF-ADR-0039 decision 6) y colisionarian (CS0104) con los
// homonimos de DomainEvents que ya tipan _franjas/_marcaciones en este archivo.
using DepuracionDelDia = Bitakora.ControlAsistencia.ReadModels.ControlHoras.DepuracionDelDia;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Expediente de aprobacion de un dia de trabajo: recibe cada foto de DiaDepurado (ya traducida a
// tipos de dominio por el handler) y mantiene los valores provisionales del dia. Vive separado de
// ControlDiario a proposito -- el juicio humano del Aprobador no comparte aggregate con el calculo
// automatico. El ciclo Provisional -> Aprobado llega con el issue de acciones del Aprobador.
public partial class DiaCalculadoAggregateRoot : AggregateRoot
{
    public EstadoDiaCalculado Estado { get; private set; }

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

    // Issue #429: metodo generador -- Tell-don't-Ask (MEF-ADR-0012). El aggregate cuenta su propio
    // estado privado en la forma que la pantalla de investigacion del Aprobador necesita
    // (DepuracionDelDia, via (b1) de skills/projections/read-apis.md); ninguna propiedad nueva se
    // expone. Deriva Plan por la senal estructural del contrato #424 (NombreTurno null ->
    // SinProgramar; nombre + cero franjas -> Descanso; nombre + franjas -> ConJornada), mapea
    // EstadoDiaCalculado -> EstadoAsistencia, aplana la terna del colaborador (null si el dia nacio
    // solo por marcacion, CA-4) y deriva Usada por marcacion -- igualdad EXACTA de Timestamp contra
    // la Entrada o Salida de alguna franja (CA-2) -- una sola vez, aqui, nunca en la UI.
    public DepuracionDelDia GenerarDepuracionDelDia() => throw new NotImplementedException();
}
