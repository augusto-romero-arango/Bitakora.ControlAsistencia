// Issue #270 CA-3: MarcacionRegistrada deja de implementar IPrivateEvent. Es el evento de dominio
// que se persiste en el stream de RegistroDeMarcacionAggregateRoot; ya no cruza el bus (ese rol
// ahora lo cumple RegistroDeMarcacionCreado en PrivateEvents.ControlHoras).
// Guardrail de forma: si alguien reagrega el marker por accidente (p.ej. al copiar el patron de otro
// evento privado), este test lo detecta sin depender de que algun handler realmente publique el tipo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction.Eventos;

public class MarcacionRegistradaNoEsEventoDeBusTests
{
    [Fact]
    public void MarcacionRegistrada_NoImplementaIPrivateEvent()
    {
        typeof(MarcacionRegistrada).GetInterfaces().Should().NotContain(typeof(IPrivateEvent));
    }
}
