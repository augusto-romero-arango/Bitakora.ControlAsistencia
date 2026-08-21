namespace Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

// Issue #421: terna reducida de identidad del colaborador que viaja en DiaDepurado. Nombre puro
// Colaborador reservado al concepto rico del dominio Colaboradores (anti-squatting #331/#340); este
// tipo usa un nombre simple distinto de InformacionColaborador (PublicEvents)/DetalleColaborador
// (PrivateEvents.Programacion)/ColaboradorProgramado (ControlHoras.DomainEvents) para que un using
// equivocado no compile (CA-ADR-0029: tres islas sin referencias entre si).
//
// Identificacion es el stream key del maestro Colaboradores: contrato de Identificacion.ToString()
// = "{Tipo}-{Numero}" (ej. "CC-79543210"). Sin Equals custom: todos los campos son string, la
// igualdad por valor del record por defecto ya es correcta (precedente DetalleColaborador).
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
