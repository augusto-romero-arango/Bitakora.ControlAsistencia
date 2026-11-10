using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

// Solo primitivos y sin colecciones: sin resolver custom ni ConfigurarSerializacion, y la igualdad por
// valor del record es honesta.
//
// Nombre simple distinto de MarcacionRegistrada (evento de dominio) a proposito: el Function App de
// ControlHoras referencia ambos ensamblados y un using equivocado publicaria el evento rico al bus
// sin que el compilador lo detecte (CA-ADR-0025).
//
// Nunca se persiste: no entra en ConfiguracionSerializacionControlHoras ni en
// IdentidadEventosControlHoras.TiposPersistidos.
public record RegistroDeMarcacionCreado(
    string CodigoColaborador,
    DateTime TimestampNormalizado,
    string? TipoMarcacion,
    string? DispositivoId) : IPrivateEvent;
