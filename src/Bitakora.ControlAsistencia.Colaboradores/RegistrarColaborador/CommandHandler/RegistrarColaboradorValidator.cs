using FluentValidation;
using ComandoRegistrarColaborador = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.RegistrarColaborador;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.CommandHandler;

// Issue #330: validacion de forma del comando RegistrarColaborador en el borde (MEF-ADR-0004 capa 1).
// CA-3: TipoIdentificacion (requerido + en la lista cerrada -- normalizar trim+MAYUSCULAS antes de
// intentar TipoIdentificacion.Desde, mismo criterio de normalizacion de entrada que el handler),
// NumeroIdentificacion, PrimerNombre, PrimerApellido, CodigoColaborador y FechaInicio requeridos.
// SegundoNombre/SegundoApellido son opcionales (NombreColaborador.Crear ya los normaliza).
// STUB (fase roja, issue #330): sin reglas todavia -- el implementer las agrega.
// CA-6 (auto-registro): se descubre via AddValidatorsFromAssemblyContaining ya configurado en
// ComposicionServicios (no requiere tocar el wiring de DI).
public class RegistrarColaboradorValidator : AbstractValidator<ComandoRegistrarColaborador>
{
}
