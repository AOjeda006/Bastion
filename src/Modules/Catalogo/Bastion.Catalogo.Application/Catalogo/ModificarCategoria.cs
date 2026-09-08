using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Cambia el nombre de una categoría o la mueve de sitio en el árbol.</summary>
public interface IModificarCategoria
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CategoriaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarCategoriaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarCategoria"/>
/// <remarks>
/// <b>ESTE es el caso de uso donde la comprobación de ciclos protege algo.</b> El alta no puede
/// cerrar uno —la fila que nace no está todavía en el árbol—; reasignar el padre sí, sin más que
/// mover una rama debajo de su propia descendencia. Una comprobación escrita solo en el alta
/// dejaría abierto exactamente el único camino por el que el árbol deja de serlo, y lo dejaría
/// abierto en verde. Es la misma forma del hueco que el ítem 1.7 encontró en la inversa de una
/// conversión, por su otra cara.
/// </remarks>
internal sealed class ModificarCategoria(
    IRepositorioDeCategorias categorias,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones) : IModificarCategoria
{
    public async Task<Resultado<CategoriaDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarCategoriaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Categoria? categoria = await categorias.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (categoria is null)
        {
            return Resultado.Fallo<CategoriaDto>(ErroresDeCategoria.NoEncontrada(id));
        }

        versiones.Exigir(categoria, version);

        // Contra el árbol TAL COMO ESTÁ, y no contra el que había cuando se dio de alta: entre las
        // dos operaciones ha podido moverse cualquier otra rama. Y se comprueba aunque el padre
        // que llega sea el mismo que ya tenía, que es lo contrario del criterio de
        // `ModificarArticulo` con su impuesto — a propósito. Allí lo que hay al otro lado es el
        // ciclo de vida de OTRO módulo, y revalidarlo congelaría fichas ajenas a lo que el usuario
        // toca. Aquí lo que se mira es la integridad estructural de los datos de este mismo
        // módulo: si el ascenso ya no llega a una raíz, guardar encima es empeorarlo.
        Resultado elArbol = await ElArbolSigueSiendoUnArbol
            .ComprobarAsync(categoria.Id, peticion.PadreId, categorias, cancelacion)
            .ConfigureAwait(false);

        if (!elArbol.EsCorrecto)
        {
            return Resultado.Fallo<CategoriaDto>(elArbol.Error!);
        }

        categoria.Modificar(peticion.Nombre, peticion.PadreId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(categoria.ADto());
    }
}
