using Bastion.BuildingBlocks.Domain.Retiradas;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bastion.BuildingBlocks.Infrastructure.Retiradas;

/// <summary>
/// Mapea la columna de la retirada de un maestro de instalación (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// Escrito una vez y no cuatro. Son tres líneas, y cuatro copias de tres líneas son cuatro sitios
/// donde el cuarto se olvida del <c>IsRequired</c> y deja una columna anulable en la que
/// <c>NULL</c> significaría un tercer estado que el dominio no tiene.
/// </para>
/// <para>
/// <b>Se audita.</b> Retirar un maestro de instalación afecta a todas las empresas de la
/// instalación —es el motivo entero por el que la retirada existe—, así que quién lo hizo y cuándo
/// tiene que quedar contado. Es además donde vive esa información: la decisión 3 del ítem 1.7
/// renuncia a guardar fecha y motivo en la propia columna precisamente porque la traza ya los
/// lleva, y esa frase solo es cierta si la columna entra en la traza.
/// </para>
/// <para>
/// <b>No lleva <c>HasDefaultValue</c>.</b> El valor de una fila nueva lo pone el dominio —nace sin
/// retirar— y un valor por omisión en la base sería una segunda fuente que diría lo mismo hasta el
/// día que dijera otra cosa. Lo que sí pone la migración es el relleno de las filas que ya
/// existían, que es un hecho de la migración y no del modelo.
/// </para>
/// </remarks>
public static class ConfiguracionDeRetirada
{
    /// <summary>Aplica el mapeo de la retirada.</summary>
    /// <typeparam name="T">El maestro, que declara <see cref="IRetirable"/>.</typeparam>
    /// <param name="entidad">El constructor de la entidad.</param>
    public static void Mapear<T>(EntityTypeBuilder<T> entidad)
        where T : class, IRetirable
    {
        ArgumentNullException.ThrowIfNull(entidad);

        // Por nombre y no con una lambda: `T` aquí solo se conoce como `IRetirable`, así que
        // `fila => fila.EstaRetirada` sería un acceso a la propiedad de la INTERFAZ y no a la de
        // la entidad, que es la que EF Core mapea.
        entidad.Property<bool>(nameof(IRetirable.EstaRetirada))
            .HasColumnName("retirada")
            .IsRequired()
            .SeAudita();
    }
}
