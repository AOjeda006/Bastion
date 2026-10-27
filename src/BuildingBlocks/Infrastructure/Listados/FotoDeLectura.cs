using System.Data;
using Bastion.BuildingBlocks.Application.Listados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.BuildingBlocks.Infrastructure.Listados;

/// <summary>
/// Cómo se abre una <see cref="IFotoDeLectura"/> sobre el contexto de un módulo: una transacción
/// en <c>REPEATABLE READ</c> y de solo lectura (ADR-0073).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué <c>REPEATABLE READ</c></b>: en PostgreSQL, esa transacción toma su foto con la
/// primera orden y la usa en todas las demás. En <c>READ COMMITTED</c>, que es el nivel por
/// omisión, cada orden toma la suya, y dos páginas de un mismo listado podrían ver dos tablas
/// distintas.
/// </para>
/// <para>
/// <b>Por qué de solo lectura</b>: lo que se haga dentro es leer, y si algún día alguien escribe
/// algo en el camino de una exportación, la base lo rechaza con un <c>25006</c> en vez de dejar
/// que lo escriba una transacción cuyo nivel nadie eligió para escribir. El <c>SET</c> va antes
/// de la primera lectura, que es cuando PostgreSQL todavía deja cambiarlo.
/// </para>
/// </remarks>
public static class FotoDeLectura
{
    private const string SoloLectura = "SET TRANSACTION READ ONLY";

    /// <summary>Abre la foto sobre <paramref name="contexto"/>.</summary>
    /// <param name="contexto">El contexto del módulo, el mismo con el que leen sus listados.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La foto abierta, que se deshace al liberarla: no hay nada que confirmar.</returns>
    /// <exception cref="InvalidOperationException">El contexto ya está dentro de una transacción.</exception>
    public static async Task<IAsyncDisposable> AbrirAsync(DbContext contexto, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        // Una foto dentro de otra transacción no sería una foto: heredaría el nivel y el instante
        // de la que ya está abierta, y lo que hubiera escrito esa.
        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "El contexto ya tiene una transacción abierta, y una foto tiene que ser la suya.");
        }

        IDbContextTransaction transaccion = await contexto.Database
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancelacion)
            .ConfigureAwait(false);

        try
        {
            await contexto.Database.ExecuteSqlRawAsync(SoloLectura, cancelacion).ConfigureAwait(false);
        }
        catch
        {
            await transaccion.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return transaccion;
    }
}
