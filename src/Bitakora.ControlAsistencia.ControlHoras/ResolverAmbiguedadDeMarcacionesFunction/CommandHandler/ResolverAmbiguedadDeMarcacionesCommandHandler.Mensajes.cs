using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction.CommandHandler;

public partial class ResolverAmbiguedadDeMarcacionesCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction.CommandHandler.ResolverAmbiguedadDeMarcacionesCommandHandlerMensajes",
        typeof(ResolverAmbiguedadDeMarcacionesCommandHandler).Assembly);

    public static class Mensajes
    {
        public static string DiaSinDepuracion => ResourceManager.GetString(nameof(DiaSinDepuracion))!;

        public static string RepartoIncompleto => ResourceManager.GetString(nameof(RepartoIncompleto))!;

        public static string MarcacionNoAmbigua => ResourceManager.GetString(nameof(MarcacionNoAmbigua))!;

        public static string FechaNoValida => ResourceManager.GetString(nameof(FechaNoValida))!;

        public static string CambiaUsoEnDiaAprobado => ResourceManager.GetString(nameof(CambiaUsoEnDiaAprobado))!;
    }
}
