using Bastion.BuildingBlocks.Application.Eventos;
using Bastion.BuildingBlocks.Domain.Eventos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>Registro de la bandeja de salida (R12) en el <i>composition root</i>.</summary>
public static class CableadoDeLaBandeja
{
    /// <summary>
    /// Registra el mecanismo entero: el catálogo, el interceptor que llena la cola, el despachador
    /// que la reparte y —si procede— el trabajo de fondo que la vacía.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va <b>antes</b> de los módulos, por lo mismo que el inquilinato y la auditoría: los
    /// contextos que enganchan el interceptor se registran después. Enchufarlo a cada contexto es
    /// cosa de cada módulo, con su línea a la vista en su <c>AddDbContext</c>.
    /// </para>
    /// <para>
    /// <b>El contexto de la bandeja lo registra el módulo Auditoría</b>, que es quien crea y migra
    /// las dos tablas y quien sabe contra qué base corre esto. Aquí no se elige proveedor.
    /// </para>
    /// </remarks>
    /// <param name="servicios">Colección de servicios del <i>composition root</i>.</param>
    /// <param name="publica">
    /// Si este host debe además <b>vaciar</b> la cola. Se dice que no cuando no hay base de datos a
    /// la que conectarse —los tests funcionales levantan el host entero sin dependencia ninguna—:
    /// un publicador sondeando una base que no existe sería un error por vuelta desde el arranque,
    /// y el ruido esconde los errores de verdad.
    /// </param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarBandejaDeSalida(this IServiceCollection servicios, bool publica)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.TryAddSingleton(TimeProvider.System);
        servicios.TryAddSingleton<CatalogoDeEventos>();
        servicios.TryAddSingleton<MetricasDeLaBandeja>();
        servicios.TryAddSingleton(new OpcionesDeLaBandeja());

        // De ámbito, la misma vida que el contexto que lo usa y que el inquilino del que lee: un
        // interceptor singleton se quedaría con la empresa de la primera petición y firmaría con
        // ella los eventos de todas las demás.
        servicios.TryAddScoped<InterceptorDeLaBandeja>();

        servicios.TryAddScoped<CerrojoDeLaBandeja>();
        servicios.TryAddScoped<IDespachadorDeEventos, DespachadorDeEventos>();

        if (publica)
        {
            servicios.AddHostedService<PublicadorDeLaBandeja>();
        }

        return servicios;
    }

    /// <summary>Declara cómo se llama un evento en la cola.</summary>
    /// <remarks>
    /// Lo llama <b>cada módulo</b> en su <c>Modulo…</c>, con los eventos que emite. Los bloques
    /// comunes no llevan la lista de los dieciséis módulos: eso obligaría a tocar código común
    /// para publicar un evento nuevo.
    /// </remarks>
    /// <typeparam name="T">Tipo del evento.</typeparam>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="nombre">Nombre estable, con la forma <c>modulo.hecho-ocurrido</c>.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection DeclararEvento<T>(this IServiceCollection servicios, string nombre)
        where T : EventoDeIntegracion
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        return servicios.AddSingleton(new DeclaracionDeEvento(nombre, typeof(T)));
    }

    /// <summary>Registra un manejador de eventos.</summary>
    /// <remarks>
    /// De ámbito: el publicador abre uno por vuelta, y un manejador que necesite escribir necesita
    /// el contexto de su módulo, que también lo es.
    /// </remarks>
    /// <typeparam name="T">Tipo del manejador.</typeparam>
    /// <param name="servicios">Colección de servicios.</param>
    /// <returns>La misma colección, para encadenar.</returns>
    public static IServiceCollection AgregarManejadorDeEvento<T>(this IServiceCollection servicios)
        where T : class, IManejadorDeEvento
    {
        ArgumentNullException.ThrowIfNull(servicios);

        return servicios.AddScoped<IManejadorDeEvento, T>();
    }
}
