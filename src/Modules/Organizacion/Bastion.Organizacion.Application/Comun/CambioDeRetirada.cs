using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Domain.Retiradas;

namespace Bastion.Organizacion.Application.Comun;

/// <summary>
/// Lo que hacen igual los ocho casos de uso de retirada y reincorporación de los cuatro maestros
/// de instalación (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// Los ocho son la misma frase con otro sustantivo: buscar la fila, contestar <c>404</c> si no
/// está, exigir la versión del <c>If-Match</c>, cambiar el estado y confirmar. Ocho copias de esas
/// cinco líneas son ocho sitios donde la séptima se olvida de <c>Exigir</c> y deja una escritura
/// sin control de concurrencia, que no rompe ningún test porque la respuesta sigue siendo
/// <c>204</c>.
/// </para>
/// <para>
/// <b>Lo que NO se comparte es el error.</b> Cada maestro trae el suyo ya construido, porque el
/// <c>type</c> del ProblemDetails es contrato —el frontal escribe el texto a partir de él
/// (ADR-0030)— y un «recurso-no-encontrado» genérico dejaría a quien lo lee sin saber qué no se
/// encontró.
/// </para>
/// </remarks>
internal static class CambioDeRetirada
{
    /// <summary>Retira o reincorpora la fila, si está.</summary>
    /// <typeparam name="T">El maestro, que declara <see cref="IRetirable"/>.</typeparam>
    /// <param name="fila">La fila ya leída, o nula si no hay ninguna con ese identificador.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="retirar"><c>true</c> para retirar, <c>false</c> para reincorporar.</param>
    /// <param name="noEncontrada">El error propio del maestro cuando la fila no está.</param>
    /// <param name="versiones">Quien compara la versión pedida con la guardada.</param>
    /// <param name="unidadTrabajo">La unidad de trabajo del módulo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<Resultado> AplicarAsync<T>(
        T? fila,
        VersionDeRecurso version,
        bool retirar,
        ErrorDeOperacion noEncontrada,
        IVersionesDeOrganizacion versiones,
        IUnidadTrabajoDeOrganizacion unidadTrabajo,
        CancellationToken cancelacion)
        where T : class, IRetirable
    {
        if (fila is null)
        {
            return Resultado.Fallo(noEncontrada);
        }

        versiones.Exigir(fila, version);

        // Las dos transiciones son idempotentes en el dominio, así que repetir el verbo deja el
        // mismo estado y no hay que preguntar antes.
        if (retirar)
        {
            fila.Retirar();
        }
        else
        {
            fila.Reincorporar();
        }

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
