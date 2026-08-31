using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Errores;
using Microsoft.AspNetCore.Authorization;
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
/// Cada ruta se publica como un <see cref="Endpoint"/> de verdad, con
/// <see cref="AllowAnonymousAttribute"/> entre sus metadatos, y lo ejecuta el middleware de
/// endpoints del propio host. Eso importa: desde el ítem 0.5 la política de respaldo exige
/// autenticación a TODO lo que no diga lo contrario —incluidas las peticiones que no casan con
/// ningún endpoint—, así que un middleware suelto al final de la tubería ya no se alcanza: la
/// autorización responde 401 mucho antes. Publicándolas como endpoints, lo que se ejercita
/// vuelve a ser el manejador de excepciones y no la puerta.
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
    internal const string NoAutenticado = "/pruebas/errores/no-autenticado";
    internal const string VersionObsoleta = "/pruebas/errores/version-obsoleta";
    internal const string FaltaLaVersion = "/pruebas/errores/falta-la-version";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => aplicacion =>
    {
        // ANTES de `next`, porque el endpoint hay que dejarlo puesto antes de que el enrutado y la
        // autorización miren si hay uno. Este middleware no responde ni falla: solo etiqueta la
        // petición y sigue. Lo que estalla es el delegado del endpoint, ya por dentro del
        // manejador de excepciones, que es lo que se quiere ejercitar.
        aplicacion.Use(async (contexto, siguiente) =>
        {
            if (EsRutaDePrueba(contexto.Request.Path.Value ?? string.Empty))
            {
                contexto.SetEndpoint(new Endpoint(
                    Atender,
                    new EndpointMetadataCollection(new AllowAnonymousAttribute()),
                    "rutas que fallan a propósito"));
            }

            await siguiente(contexto);
        });

        next(aplicacion);
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
            NoAutenticado => Responder(contexto, ErrorDeOperacion.NoAutenticado(
                "sesion-caducada", "Su sesión ha caducado. Vuelva a iniciarla.")),

            // Las dos de concurrencia se sirven por sus fábricas de verdad, y no por un
            // ErrorDeOperacion escrito aquí a mano: así lo que se ejercita es el código que
            // publican los 412 y los 428 reales, y cambiarlo rompe aquí.
            VersionObsoleta => Responder(
                contexto, ErroresDeConcurrencia.Obsoleta(new VersionDeRecurso(756))),
            FaltaLaVersion => Responder(contexto, ErroresDeConcurrencia.FaltaLaCabecera()),

            _ => Task.CompletedTask,
        };
    }

    private static Task Responder(HttpContext contexto, ErrorDeOperacion error) =>
        error.ARespuesta().ExecuteAsync(contexto);
}
