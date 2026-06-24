using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;

// HU-108: Evento publico que se publica al Service Bus via IPublicEventSender.
// Representa el resultado del calculo del dia de trabajo de un empleado tras una
// marcacion o asignacion de turno.
// Issue #183: el payload es 100% primitivo (HorasDiscriminadas con colecciones de primitivos).
// Cura de raiz del bug del smoke CA-5: el contrato ya NO lleva tipos de dominio ricos
// (IntervaloTemporal, DetalleRetardo) que solo serializaban bien con el resolver custom de Marten;
// asi sobrevive el roundtrip con el serializador por defecto del publisher (sin resolver custom).
// ADR-0015: sealed class (no record) por contener HorasDiscriminadas con colecciones
// (IReadOnlyDictionary, IReadOnlyList). Un record prometeria igualdad por valor que esas
// colecciones no cumplen. Patron DTO de integracion: ctor publico parametrizado + privado vacio.
// Sin ConfigurarSerializacion propio: STJ deserializa via el ctor publico (propiedades publicas).
// ADR-0002: vive en Contracts por ser contrato cross-domain (consumidor: sistema de nomina).
// ADR-0004: topic "dia-calculado". ADR-0005: naming kebab-case, participio pasado.
// ADR-0024: duplicacion intencional con ProgramacionTurnoDiarioSolicitada - solo dos sitios,
// los dominios pueden diverger (versionado, envelopes, campos especificos).
public sealed class DiaCalculado : IPublicEvent
{
    // Puede ser null cuando el ControlDiario nacio solo por marcacion sin turno previo.
    public InformacionEmpleado? InformacionEmpleado { get; private set; }
    public DateOnly Fecha { get; private set; }
    public HorasDiscriminadas HorasDiscriminadas { get; private set; } = null!;

    public DiaCalculado(
        InformacionEmpleado? informacionEmpleado,
        DateOnly fecha,
        HorasDiscriminadas horasDiscriminadas)
    {
        InformacionEmpleado = informacionEmpleado;
        Fecha = fecha;
        HorasDiscriminadas = horasDiscriminadas;
    }

    // Constructor privado para STJ/Marten (ADR-0015 patron DTO de integracion sin invariantes).
    // STJ usa este ctor para crear la instancia y luego asigna las propiedades via setters.
    private DiaCalculado() { }
}
