using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.BuildingBlocks.Infrastructure.Numeracion;
using Bastion.Organizacion.Domain.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Api.IntegrationTests.Numeracion;

/// <summary>
/// El mecanismo de numeración sobre el contexto que se le dé, para poder ejercerlo desde un test.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es un doble: es el mecanismo de verdad.</b> <see cref="NumeradorDeSerie{TDocumento}"/> es
/// abstracta porque cada módulo deriva la suya sobre su propio <c>DbContext</c> —así el número cae
/// en la transacción del documento y no en otra—, y aquí se deriva una más para poder numerar
/// desde donde no hay módulo: la puerta de atrás de los tests. Lo que se ejecuta es la misma
/// sentencia, con el mismo <c>WHERE</c> y la misma guarda de transacción.
/// </para>
/// <para>
/// <b>Sus documentos son los tipos de serie mismos</b>, sin traducir: un módulo nombra los suyos y
/// dice en qué series numeran, y aquí no hay módulo que nombre nada. Así se puede pedir un número
/// para cualquier tipo, incluido uno que no es el de la serie.
/// </para>
/// <para>
/// Existe porque desde el ítem 2.4 <b>no hay forma de subir un contador desde C#</b>: el método que
/// lo hacía se borró, y un test que necesite una serie que ya ha numerado tiene que numerar de
/// verdad. Subir la columna a mano en la base probaría un estado que el sistema no sabe producir.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto sobre el que corre la sentencia.</param>
/// <param name="inquilino">La empresa que la sentencia comprueba.</param>
internal sealed class NumeradorDePruebas(DbContext contexto, IInquilinoActual inquilino)
    : NumeradorDeSerie<TipoDeDocumento>(contexto, inquilino)
{
    /// <summary>
    /// Numera una vez, en su propia transacción, y devuelve el número que ha tocado.
    /// </summary>
    /// <remarks>
    /// La transacción se abre aquí porque el mecanismo <b>exige</b> una abierta y revienta si no la
    /// hay. En una petición de verdad la abre el filtro de idempotencia; en un test, esto.
    /// </remarks>
    /// <param name="contexto">Contexto del módulo desde el que se numera.</param>
    /// <param name="empresaId">Empresa que confirma.</param>
    /// <param name="serieId">Serie de la que se toma el número.</param>
    /// <param name="tipo">De qué tipo tiene que ser la serie.</param>
    /// <param name="fecha">La fecha que tiene que caer en el ejercicio de la serie.</param>
    /// <returns>El número tomado, o el fallo de una serie que no numera.</returns>
    public static async Task<Resultado<long>> NumerarAsync(
        DbContext contexto, Guid empresaId, Guid serieId, TipoDeDocumento tipo, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        NumeradorDePruebas numerador = new(contexto, new InquilinoFijo(empresaId));

        await using IDbContextTransaction transaccion =
            await contexto.Database.BeginTransactionAsync();

        Resultado<long> numero = await numerador.TomarNumeroAsync(
            serieId, tipo, fecha, CancellationToken.None);

        // Solo se confirma lo que sale bien, igual que hace el filtro de idempotencia: dejar el
        // incremento confirmado tras un fallo gastaría un número que nadie lleva.
        if (numero.EsCorrecto)
        {
            await transaccion.CommitAsync();
        }
        else
        {
            await transaccion.RollbackAsync();
        }

        return numero;
    }

    /// <inheritdoc />
    protected override string TipoDeSerieQueNumera(TipoDeDocumento documento) => documento.ToString();
}
