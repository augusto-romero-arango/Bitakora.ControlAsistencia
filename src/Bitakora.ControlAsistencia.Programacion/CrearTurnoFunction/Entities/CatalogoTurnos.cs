using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Entities;

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
    public void Apply(TurnoCreado evento)
    {
        Id = evento.TurnoId.ToString();
        _nombre = evento.Nombre;
        _franjasOrdinarias = evento.FranjasOrdinarias.ToList();
        _estaActivo = true;
    }

    // CA-2: formato "{nombre} (06:00-12:00)(14:00-16:00)"
    // Nombre seguido de las ordinarias usando su ToString()
    public override string ToString() =>
        $"{_nombre} {string.Join("", _franjasOrdinarias)}";

    // Factory interno: crea el aggregate con el evento en _uncommittedEvents
    // Usado por el handler para StartStream -- no es parte de la interfaz publica del dominio
    internal static CatalogoTurnos Iniciar(TurnoCreado evento)
    {
        var catalogo = new CatalogoTurnos();
        catalogo._uncommittedEvents.Add(evento);
        catalogo.Apply(evento);
        return catalogo;
    }
}
