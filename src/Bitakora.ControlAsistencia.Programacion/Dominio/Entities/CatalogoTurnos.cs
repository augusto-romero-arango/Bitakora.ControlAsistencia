using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.Dominio.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Dominio.Entities;

// HU-4: Aggregate root del catalogo de turnos de trabajo
// ADR-0015: partial class para soportar clase Mensajes en archivo separado
// Interfaz publica: Apply(TurnoCreado), ToString()
// Estado interno (privado): nombre, franjas ordinarias, activo
public partial class CatalogoTurnos : AggregateRoot
{
    private string _nombre = string.Empty;
    private List<FranjaOrdinaria> _franjasOrdinarias = [];
    private bool _estaActivo;

    // CA-1: aplica TurnoCreado y establece estado interno del aggregate
    // Establece: Id (heredado de AggregateRoot), nombre, franjas ordinarias, activo=true
    public void Apply(TurnoCreado evento) => throw new NotImplementedException();

    // CA-2: formato "{nombre} (06:00-12:00)(14:00-16:00)"
    // Nombre seguido de las ordinarias usando su ToString()
    public override string ToString() => throw new NotImplementedException();
}
