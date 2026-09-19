using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Inventario.Contracts.Ajustes;
using Bastion.Inventario.Domain.Movimientos;
using Bastion.Organizacion.Contracts.Almacenes;
using Bastion.Organizacion.Contracts.Comun;

namespace Bastion.Inventario.Application.Ajustes;

/// <summary>Las filas del libro que escribió un documento (R13, la ida).</summary>
public interface IMovimientosDelDocumento
{
    /// <summary>Lee los movimientos de un ajuste.</summary>
    /// <param name="ajusteId">El documento.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Sus movimientos, o el motivo por el que no se leen.</returns>
    Task<Resultado<IReadOnlyList<MovimientoDto>>> DeUnAjusteAsync(
        Guid ajusteId,
        CancellationToken cancelacion);
}

/// <summary>
/// La otra mitad del ADR-0037: un movimiento ya escrito contra un almacén <b>bloqueado</b> se
/// sigue leyendo, y se lee resolviendo su almacén por el mismo puerto que el alta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí el estado del almacén no es una condición, es un dato.</b> El alta pregunta y, si la
/// respuesta es <c>SoloResuelveLoViejo</c>, no deja pasar; esta lectura pregunta y <b>lo cuenta</b>.
/// Es la misma llamada al mismo puerto con dos usos distintos, y por eso las dos mitades se
/// comprueban con el mismo almacén bloqueado: si la lectura también cerrara, bloquear una
/// estantería haría desaparecer las existencias que hay dentro, que es exactamente lo que el
/// artículo 32 no pide.
/// </para>
/// <para>
/// <b>Y no hay ningún filtro de bloqueo en el contexto del módulo</b>, así que esto no depende de
/// que nadie se acuerde de abrirlo: el movimiento no implementa <c>IBloqueable</c> y su consulta no
/// tiene nada que sortear.
/// </para>
/// </remarks>
/// <param name="ajustes">Dónde vive el libro.</param>
/// <param name="almacenes">Puerto de almacenes (ítem 2.2).</param>
internal sealed class MovimientosDelDocumento(
    IRepositorioDeAjustes ajustes,
    IConsultaDeAlmacenes almacenes) : IMovimientosDelDocumento
{
    /// <inheritdoc/>
    public async Task<Resultado<IReadOnlyList<MovimientoDto>>> DeUnAjusteAsync(
        Guid ajusteId,
        CancellationToken cancelacion)
    {
        IReadOnlyList<MovimientoStock> movimientos = await ajustes
            .MovimientosDeAsync(TipoDeDocumentoOrigen.Ajuste, ajusteId, cancelacion)
            .ConfigureAwait(false);

        Dictionary<Guid, bool> almacenesResueltos = [];

        List<MovimientoDto> leidos = [];

        foreach (MovimientoStock movimiento in movimientos)
        {
            if (!almacenesResueltos.TryGetValue(movimiento.AlmacenId, out bool seOfrece))
            {
                EstadoDeMaestro estado = await almacenes
                    .EstadoDeAsync(movimiento.AlmacenId, cancelacion)
                    .ConfigureAwait(false);

                seOfrece = estado == EstadoDeMaestro.SeOfreceParaLoNuevo;
                almacenesResueltos.Add(movimiento.AlmacenId, seOfrece);
            }

            leidos.Add(movimiento.ADto(seOfrece));
        }

        return Resultado.Correcto<IReadOnlyList<MovimientoDto>>(leidos);
    }
}
