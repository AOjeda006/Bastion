using Bastion.BuildingBlocks.Infrastructure.Multiempresa;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Api.FunctionalTests.Persistencia;

/// <summary>
/// De dónde salen los modelos sobre los que se comprueban las reglas que miran el modelo entero.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se descubren, no se enumeran</b>, y el ítem 1.6 explica por qué. Hasta él, cada regla de
/// modelo —inquilinato, auditoría, fechas, claves— traía su propia lista escrita a mano con dos o
/// tres contextos dentro. Terceros entró en la fase 1 y <b>no se añadió a ninguna</b>: durante todo
/// el módulo, cuatro reglas que dicen «cada entidad declara…» estuvieron diciéndolo de dos módulos
/// de cinco, en verde, sin que nada avisara. No es un olvido puntual: es el modo de fallo de
/// cualquier universo escrito a mano, y el mismo que el artículo 32 tenía en este mismo ítem —una
/// obligación transversal montada dentro de un módulo, ciega a los demás—.
/// </para>
/// <para>
/// El universo es «toda clase concreta que hereda de <see cref="ContextoDeModulo"/>», que es
/// exactamente la definición de «un contexto de este sistema»: quien escriba el contexto del
/// módulo de Inventario entrará solo, y si no lo registra en el contenedor, esto <b>lanza</b> en
/// vez de saltárselo. Un contexto que desaparece del universo lo caza
/// <c>El_universo_de_modelos_es_el_declarado</c>, que compara la lista entera.
/// </para>
/// </remarks>
internal static class LosModelosDeCadaModulo
{
    /// <summary>
    /// Los contextos que este sistema tiene, por nombre y enteros.
    /// </summary>
    /// <remarks>
    /// Escrita a propósito además del descubrimiento: sin ella, un descubrimiento que dejara de
    /// encontrar a nadie dejaría todas las reglas de modelo recorriendo una lista vacía y en
    /// verde, que es la forma de falso verde que este fichero existe para quitar.
    /// </remarks>
    internal static readonly string[] Declarados =
    [
        "AuditoriaDbContext",
        "ContextoDeLaBandeja",
        "IdentidadDbContext",
        "OrganizacionDbContext",
        "TercerosDbContext",
    ];

    /// <summary>Los tipos de contexto, descubiertos por herencia.</summary>
    internal static IReadOnlyList<Type> Contextos() =>
        [.. typeof(ContextoDeModulo).Assembly.GetTypes()
            .Concat(AppDomain.CurrentDomain.GetAssemblies()
                .Where(ensamblado => ensamblado.FullName?.StartsWith("Bastion.", StringComparison.Ordinal) == true)
                .SelectMany(ensamblado => ensamblado.GetTypes()))
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false }
                && typeof(ContextoDeModulo).IsAssignableFrom(tipo))
            .Distinct()
            .OrderBy(tipo => tipo.Name, StringComparer.Ordinal)];

    /// <summary>Los modelos ya construidos de todos los contextos, resueltos del contenedor.</summary>
    /// <param name="servicios">El contenedor de la API levantada.</param>
    internal static IReadOnlyList<IModel> De(IServiceProvider servicios)
    {
        using IServiceScope alcance = servicios.CreateScope();

        return [.. Contextos().Select(tipo =>
            ((DbContext)alcance.ServiceProvider.GetRequiredService(tipo)).Model)];
    }

    /// <summary>Todas las entidades de todos los modelos.</summary>
    /// <param name="servicios">El contenedor de la API levantada.</param>
    internal static IReadOnlyList<IEntityType> Entidades(IServiceProvider servicios) =>
        [.. De(servicios).SelectMany(modelo => modelo.GetEntityTypes())];
}
