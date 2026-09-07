using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Bastion.BuildingBlocks.Infrastructure.Entidades;

/// <summary>
/// Ninguna clave <see cref="Guid"/> de este modelo se genera al insertar: la pone el dominio en su
/// fábrica, y el modelo tiene que decirlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué corrige.</b> Por convención, EF Core mapea toda clave <see cref="Guid"/> como
/// <c>ValueGenerated.OnAdd</c>. Eso no hace que la genere el servidor —la rellena un generador del
/// lado del cliente, y si llega con valor, EF lo respeta—, así que en el <c>INSERT</c> no se nota
/// nada y durante cinco fases no pasó nada. Pero <c>OnAdd</c> es también lo que EF consulta para
/// decidir algo muy distinto: si una entidad que aparece en la colección de un padre <b>ya
/// seguido</b> es un alta o una fila que ya existía. La pregunta que se hace es «¿viene la clave
/// puesta?»: con un <c>identity</c> la respuesta distingue —vale cero hasta que la base la
/// genera—, pero con un <see cref="Guid"/> v7 que pone la fábrica del dominio la clave viene
/// <b>siempre</b> puesta, así que EF concluye «esto ya existía» y marca la entrada como
/// modificación.
/// </para>
/// <para>
/// <b>Y una modificación de una fila que no está es un 412.</b> EF emite el <c>UPDATE</c>, afecta a
/// cero filas, EF lo toma por un choque de concurrencia y la política de errores lo traduce a
/// <c>412 Precondition Failed</c> — sobre un alta correcta, en una ruta donde no había nadie
/// compitiendo. No hay excepción al colgar, ni aviso al construir el modelo, ni nada que el
/// compilador pueda mirar.
/// </para>
/// <para>
/// <b>Por qué una convención y no una línea en cada configuración.</b> Esto no es una decisión de
/// mapeo de una entidad: es una propiedad del sistema entero —aquí las claves las genera siempre el
/// dominio, nunca la base—, y una línea por configuración sería una lista de la que la próxima
/// entidad se caerá sin ruido. Se declara una vez, se aplica al finalizar el modelo, y lo que
/// comprueba que no se ha caído nadie es <c>LasClavesSeConocenAntesDeGuardarTests</c>, sobre los
/// modelos ya construidos de <b>todos</b> los módulos.
/// </para>
/// <para>
/// Solo toca claves <see cref="Guid"/>: el testigo de concurrencia del ADR-0015 es un <c>uint</c>
/// que <b>sí</b> genera PostgreSQL en cada escritura (<c>xmin</c>), no es clave, y su
/// <c>ValueGenerated.OnAddOrUpdate</c> tiene que quedarse donde está.
/// </para>
/// </remarks>
public sealed class LaClaveLaPoneElDominio : IModelFinalizingConvention
{
    /// <summary>Marca como no generada toda propiedad <see cref="Guid"/> de una clave primaria.</summary>
    /// <param name="modelBuilder">El constructor del modelo que se está cerrando.</param>
    /// <param name="context">El contexto de la convención.</param>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IConventionEntityType tipo in modelBuilder.Metadata.GetEntityTypes())
        {
            if (tipo.FindPrimaryKey() is not { } clave)
            {
                continue;
            }

            foreach (IConventionProperty propiedad in clave.Properties)
            {
                if (propiedad.ClrType == typeof(Guid) || propiedad.ClrType == typeof(Guid?))
                {
                    propiedad.Builder.ValueGenerated(ValueGenerated.Never, fromDataAnnotation: false);
                }
            }
        }
    }
}
