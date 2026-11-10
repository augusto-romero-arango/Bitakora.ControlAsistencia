// MarcacionRegistrada se persiste en el stream de RegistroDeMarcacionAggregateRoot y no cruza el bus
// (ese rol es de RegistroDeMarcacionCreado): este test detecta si alguien le reagrega IPrivateEvent.

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
