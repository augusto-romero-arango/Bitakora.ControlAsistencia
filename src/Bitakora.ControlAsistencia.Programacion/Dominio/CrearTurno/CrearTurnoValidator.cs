using FluentValidation;

using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.Dominio.Comandos.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.Dominio.CrearTurno;

// HU-4: Validacion de estructura del request antes del command handler
// CA-5: TurnoId no vacio, Nombre no vacio, Ordinarias no vacia
// CA-6: se auto-registra via AddValidatorsFromAssemblyContaining (configurado en Program.cs)
public class CrearTurnoValidator : AbstractValidator<ComandoCrearTurno>
{
    public CrearTurnoValidator() => throw new NotImplementedException();
}
