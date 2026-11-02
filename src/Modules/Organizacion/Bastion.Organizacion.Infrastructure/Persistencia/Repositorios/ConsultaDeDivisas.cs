using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Divisas;
using Bastion.Organizacion.Domain.Divisas;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IConsultaDeDivisas"/>
/// <remarks>
/// Vive en Organización, que es la dueña de la tabla, y se resuelve en proceso: el módulo que
/// pregunta llama a un método (§4, reglas de frontera 1 y 3).
/// </remarks>
internal sealed class ConsultaDeDivisas(OrganizacionDbContext contexto) : IConsultaDeDivisas
{
    /// <summary>Las tres respuestas, desde el ítem 1.7, y por el código desde el 3.3.</summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola consulta y no dos.</b> Preguntar primero si existe y luego si está retirada son
    /// dos viajes y, peor, dos lecturas entre las que la fila puede cambiar: contestaría «existe y
    /// se ofrece» sobre un estado que no fue verdad en ningún instante. Se trae el estado de la
    /// retirada como anulable, y el nulo <b>es</b> la respuesta a la primera pregunta.
    /// </para>
    /// <para>
    /// <c>SoloResuelveLoViejo</c> es el estado de una divisa retirada: una factura emitida en
    /// pesetas tiene que poder seguir diciendo en qué se emitió mucho después de que nadie pueda
    /// emitir una nueva.
    /// </para>
    /// <para>
    /// <b>El código se busca como se guarda</b>, con <c>Divisa.NormalizarCodigo</c>, que no valida:
    /// uno sin forma de código no está en la tabla, y eso es un <c>NoExiste</c>. El índice único
    /// sobre el código es el que contesta.
    /// </para>
    /// </remarks>
    /// <param name="codigo">Código ISO 4217, tal como lo escribieron.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    public async Task<EstadoDeMaestro> EstadoDeAsync(string codigo, CancellationToken cancelacion)
    {
        string buscado = Divisa.NormalizarCodigo(codigo);

        bool? retirada = await contexto.Divisas
            .Where(divisa => divisa.Codigo == buscado)
            .Select(divisa => (bool?)divisa.EstaRetirada)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return retirada switch
        {
            null => EstadoDeMaestro.NoExiste,
            true => EstadoDeMaestro.SoloResuelveLoViejo,
            false => EstadoDeMaestro.SeOfreceParaLoNuevo,
        };
    }
}
