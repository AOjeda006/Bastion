using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Bastion.Api.FunctionalTests.Errores;

/// <summary>
/// Añade al host de pruebas unas rutas que fallan a propósito, una por clase de error.
/// </summary>
/// <remarks>
/// <para>
/// Van en el proyecto de tests y no en la API porque la política de errores es middleware
/// central: es independiente de la ruta, y publicar rutas de diagnóstico en producción para
/// poder probarla sería pagar en superficie real lo que aquí no cuesta nada.
/// </para>
/// <para>
/// El filtro registra su middleware DESPUÉS de <c>next</c>, es decir, en el punto más interno
/// de la tubería: por dentro del manejador de excepciones, que es lo que se quiere ejercitar.
/// Las peticiones que sí casan con un endpoint real (las sondas) ni lo rozan.
/// </para>
/// </remarks>
internal sealed class RutasQueFallan : IStartupFilter
{
    // Texto que solo existe DENTRO del sistema. Si aparece en una respuesta, la política de
    // errores está componiendo la respuesta con el texto de la excepción.
    internal const string RastroInterno =
        "consulta SELECT * FROM organizacion.usuario en C:\\bastion\\secretos\\clave.pem";

    internal const string Estalla = "/pruebas/errores/estalla";
    internal const string PeticionMala = "/pruebas/errores/peticion-mala";
    internal const string ReglaDeNegocio = "/pruebas/errores/regla-de-negocio";
    internal const string NoEncontrado = "/pruebas/errores/no-encontrado";
    internal const string Conflicto = "/pruebas/errores/conflicto";
    internal const string Permiso = "/pruebas/errores/permiso";
    internal const string Validacion = "/pruebas/errores/validacion";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => aplicacion =>
    {
        next(aplicacion);

        // `Use` y no `Run`: lo que no sea una ruta de prueba tiene que seguir cayendo al final
        // de la tubería, que es quien pone el 404 que también hay que comprobar.
        aplicacion.Use(async (contexto, siguiente) =>
        {
            if (!EsRutaDePrueba(contexto.Request.Path.Value ?? string.Empty))
            {
                await siguiente(contexto);
                return;
            }

            await Atender(contexto);
        });
    };

    private static bool EsRutaDePrueba(string ruta) => ruta.StartsWith("/pruebas/", StringComparison.Ordinal);

    private static Task Atender(HttpContext contexto)
    {
        string ruta = contexto.Request.Path.Value ?? string.Empty;

        return ruta switch
        {
            Estalla => throw new InvalidOperationException(
                $"Fallo al procesar {contexto.Request.Query["veneno"]}: {RastroInterno}"),

            // Es el tipo y la forma exacta con que ASP.NET Core señala un cuerpo que no parsea.
            PeticionMala => throw new BadHttpRequestException(
                $"Failed to read parameter \"Pedido pedido\" from the request body as JSON: {RastroInterno}",
                StatusCodes.Status400BadRequest),

            ReglaDeNegocio => Responder(contexto, ErrorDeOperacion.ReglaDeNegocio(
                "stock-insuficiente", "No hay bastante stock disponible para servir la línea.")),
            NoEncontrado => Responder(contexto, ErrorDeOperacion.NoEncontrado(
                "articulo-no-encontrado", "No existe el artículo indicado.")),
            Conflicto => Responder(contexto, ErrorDeOperacion.Conflicto(
                "pedido-ya-confirmado", "El pedido ya estaba confirmado.")),
            Permiso => Responder(contexto, ErrorDeOperacion.PermisoDenegado(
                "sin-permiso-de-facturacion", "Su perfil no permite emitir facturas.")),
            Validacion => Responder(contexto, ErrorDeOperacion.Validacion(
                "fecha-fuera-de-ejercicio", "La fecha no cae dentro de un ejercicio abierto.")),

            _ => Task.CompletedTask,
        };
    }

    private static Task Responder(HttpContext contexto, ErrorDeOperacion error) =>
        error.ARespuesta().ExecuteAsync(contexto);
}
