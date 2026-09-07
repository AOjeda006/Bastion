using System.Reflection;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Retiradas;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Retiradas;

/// <summary>
/// Un maestro retirable no se borra: no publica <c>DELETE /{recurso}/{id}</c>, y sí publica las
/// dos puertas de su retirada (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// <b>«Ningún DELETE, para ninguno de los cuatro, nunca» es una frase del ADR, y hasta aquí no la
/// miraba nada.</b> Que hoy no exista ese verbo no protege de nada: es exactamente la forma que
/// tiene un hueco de parecer una decisión. Borrar una divisa deja sin resolver la moneda de todas
/// las facturas emitidas en ella, y no da error — devuelve una factura que ya no sabe decir en qué
/// se emitió. La retirada existe precisamente para que no haga falta borrar.
/// </para>
/// <para>
/// <b>Los dos lados se descubren, y ninguno es una lista.</b> El del dominio, de
/// <see cref="IRetirable"/>: la marca que hace de lista, como <c>IBloqueable</c>. El del contrato,
/// de lo que la API publica —un listado cuyo elemento tiene <c>Retirada</c>—, que es lo que ve un
/// cliente. Un quinto maestro retirable entra en los dos a la vez sin que nadie lo apunte aquí, y
/// entra con su prohibición puesta.
/// </para>
/// <para>
/// <b>Por qué se comparan por número y no por nombre.</b> No hay forma honrada de casar la entidad
/// <c>Divisa</c> con la ruta <c>/divisas</c> sin inventarse una convención de nombres, y una
/// convención de nombres es lo que el ítem 1.2 tumbó: se le escapa el primero que no la siga, y se
/// le escapa en silencio. El número sí dice lo que hay que saber: que ningún maestro retirable se
/// ha quedado sin publicar su retirada, y que ninguna ruta publica una sin entidad detrás. Lo que
/// esta comparación no promete —cuál es cuál— no hace falta, porque la prohibición de abajo se
/// aplica a TODAS las rutas del conjunto.
/// </para>
/// </remarks>
public sealed class NingunMaestroRetirableSeBorraTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    /// <summary>Que haya dos universos que comparar: sin esto, todo lo demás sale verde vacío.</summary>
    [Fact]
    public void Los_dos_universos_no_estan_vacios_y_tienen_el_mismo_tamano()
    {
        IReadOnlyList<Type> entidades = EntidadesRetirables();
        IReadOnlyList<string> recursos = RecursosRetirables();

        entidades.ShouldNotBeEmpty(
            "no se ha encontrado ni una entidad que implemente `IRetirable` en los ensamblados " +
            "de dominio que hay junto al binario. O la marca ha cambiado de sitio, o se está " +
            "barriendo donde no es — y las dos cosas dejan estas reglas sin universo");

        recursos.ShouldNotBeEmpty(
            "la API no publica ni un listado cuyo elemento lleve `Retirada`, así que la " +
            "prohibición de abajo recorrería una lista vacía y diría que no hay ningún DELETE " +
            "prohibido porque no hay ningún recurso que mirar");

        recursos.Count.ShouldBe(
            entidades.Count,
            "no hay tantos recursos retirables publicados como entidades retirables hay. Sobra " +
            "una entidad: alguien puso `IRetirable` y no publicó la retirada, y entonces la fila " +
            "solo se puede quitar de en medio borrándola. O sobra un recurso: hay un `Retirada` " +
            "en un DTO que no responde a ninguna entidad marcada, y ese campo miente. " +
            $"Entidades: {string.Join(", ", entidades.Select(tipo => tipo.Name))}. " +
            $"Recursos: {string.Join(", ", recursos)}");
    }

    /// <summary>La prohibición del ADR, literal.</summary>
    [Fact]
    public void Ningun_recurso_retirable_publica_el_borrado_de_una_fila()
    {
        IReadOnlyList<string> borrados =
        [
            .. from recurso in RecursosRetirables()
               from descripcion in Publicadas()
               where EsDelRecurso(descripcion, recurso)
               where string.Equals(descripcion.HttpMethod, "DELETE", StringComparison.Ordinal)
               where !EsLaRetirada(descripcion)
               select $"DELETE /{descripcion.RelativePath}",
        ];

        // Se excluye la puerta de la retirada y NADA más: `DELETE .../{id}/retirada` borra la
        // retirada, que es un sub-recurso, igual que `DELETE /ejercicios/{id}/cierre` reabre sin
        // borrar el ejercicio. Lo prohibido es el que se lleva la fila.
        borrados.ShouldBeEmpty(
            "estos recursos son maestros retirables y publican un borrado: " +
            string.Join(", ", borrados) + ". El ADR-0023 no admite ninguno para ninguno de " +
            "ellos, nunca: son maestros de instalación (R8) a los que apunta todo lo emitido " +
            "bajo ellos, y borrar la fila deja esos documentos sin poder decir en qué divisa o " +
            "en qué unidad se emitieron. Para quitar una de en medio está la retirada");
    }

    /// <summary>Y las dos puertas de la retirada existen, en los dos sentidos.</summary>
    /// <remarks>
    /// Sin esto, la prohibición de arriba se cumpliría perfectamente borrando las cuatro rutas de
    /// retirada: cero borrados y cero maneras de retirar. La regla diría que todo está bien y el
    /// único camino que quedaría para quitar una divisa de en medio sería volver a pedir el
    /// <c>DELETE</c> que acaba de prohibirse.
    /// </remarks>
    [Fact]
    public void Cada_recurso_retirable_publica_las_dos_puertas_y_el_GET_por_identificador()
    {
        IReadOnlyList<string> faltan =
        [
            .. from recurso in RecursosRetirables()
               from exigida in Exigidas(recurso)
               where !Publicadas().Any(descripcion =>
                   string.Equals(descripcion.HttpMethod, exigida.Metodo, StringComparison.Ordinal)
                   && string.Equals(descripcion.RelativePath, exigida.Ruta, StringComparison.Ordinal))
               select $"{exigida.Metodo} /{exigida.Ruta}",
        ];

        faltan.ShouldBeEmpty(
            "a estos recursos retirables les falta una ruta: " + string.Join(", ", faltan) +
            ". Las dos primeras son poner y quitar la retirada —y quitarla tiene que existir " +
            "porque retirar por error deja a TODAS las empresas sin esa fila (R8)—. La tercera " +
            "es la que separa la retirada del bloqueo: una fila retirada se sigue devolviendo " +
            "por su identificador, mientras que una bloqueada responde como si no existiera");
    }

    private static IEnumerable<(string Metodo, string Ruta)> Exigidas(string recurso) =>
    [
        ("POST", recurso + "/{id}/retirada"),
        ("DELETE", recurso + "/{id}/retirada"),
        ("GET", recurso + "/{id}"),
    ];

    /// <summary>Las entidades marcadas, en cualquier módulo que haya junto al binario.</summary>
    /// <remarks>
    /// Por los ensamblados del disco y no por una lista de <c>typeof</c>: un maestro retirable de
    /// Catálogo entra aquí el día que Catálogo se monte, sin que nadie se acuerde de este fichero.
    /// </remarks>
    private static IReadOnlyList<Type> EntidadesRetirables() =>
        [.. from fichero in Directory.EnumerateFiles(
                AppContext.BaseDirectory, "Bastion.*.Domain.dll")
            from tipo in Assembly.LoadFrom(fichero).GetTypes()
            where tipo is { IsClass: true, IsAbstract: false }
            where typeof(IRetirable).IsAssignableFrom(tipo)
            orderby tipo.FullName, StringComparer.Ordinal
            select tipo];

    /// <summary>
    /// Los recursos que publican un listado de algo retirable, por su ruta de colección.
    /// </summary>
    /// <remarks>
    /// Se reconocen por lo que DEVUELVEN —una página cuyo elemento tiene <c>Retirada</c>— y no por
    /// cómo se llaman, por lo mismo que en <c>NingunCriterioSensibleViajaEnLaUrl</c>: el tipo de
    /// la respuesta es un hecho del contrato, y el nombre de un método es una convención que el
    /// primero que no la siga se salta sin ponerse rojo.
    /// </remarks>
    private IReadOnlyList<string> RecursosRetirables() =>
        [.. (from descripcion in Publicadas()
             where descripcion.SupportedResponseTypes.Any(respuesta =>
                 EsPaginaDeAlgoRetirable(respuesta.Type))
             select descripcion.RelativePath ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ruta => ruta, StringComparer.Ordinal)];

    private static bool EsPaginaDeAlgoRetirable(Type? tipo) =>
        tipo is { IsGenericType: true }
        && tipo.GetGenericTypeDefinition() == typeof(PaginaDe<>)
        && tipo.GetGenericArguments()[0].GetProperty("Retirada") is not null;

    // La colección y lo que cuelga de ella, y nada más: `/divisas` no puede confundirse con un
    // hipotético `/divisas-historicas` porque se exige la barra o el final exacto.
    private static bool EsDelRecurso(ApiDescription descripcion, string recurso)
    {
        string ruta = descripcion.RelativePath ?? string.Empty;

        return string.Equals(ruta, recurso, StringComparison.Ordinal)
            || ruta.StartsWith(recurso + "/", StringComparison.Ordinal);
    }

    private static bool EsLaRetirada(ApiDescription descripcion) =>
        (descripcion.RelativePath ?? string.Empty)
            .EndsWith("/retirada", StringComparison.Ordinal);

    private IEnumerable<ApiDescription> Publicadas()
    {
        IApiDescriptionGroupCollectionProvider explorador =
            _api.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

        return from grupo in explorador.ApiDescriptionGroups.Items
               from descripcion in grupo.Items
               select descripcion;
    }
}
