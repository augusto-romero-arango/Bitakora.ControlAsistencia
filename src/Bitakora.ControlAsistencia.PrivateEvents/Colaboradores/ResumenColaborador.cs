namespace Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

// Terna reducida de identidad del colaborador que viaja en DiaDepurado. Su nombre simple debe
// diferir de InformacionColaborador (PublicEvents), DetalleColaborador (PrivateEvents.Programacion)
// y ColaboradorProgramado (ControlHoras.DomainEvents) para que un using equivocado no compile
// (CA-ADR-0029: tres islas sin referencias entre si); el nombre puro Colaborador pertenece al
// concepto rico del dominio Colaboradores (anti-squatting #331/#340).
//
// Identificacion es el stream key del maestro Colaboradores, con su formato "{Tipo}-{Numero}"
// (ej. "CC-79543210"). Sin Equals custom porque todos los campos son string: si el record gana una
// coleccion, la igualdad por defecto pasa a comparar por referencia (MEF-ADR-0012).
public record ResumenColaborador(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
