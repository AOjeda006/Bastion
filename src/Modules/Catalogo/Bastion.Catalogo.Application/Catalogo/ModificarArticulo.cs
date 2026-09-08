using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Impuestos;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Cambia lo que se puede cambiar de un artículo. El código y la unidad base, no.</summary>
public interface IModificarArticulo
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del artículo.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="peticion">Los datos nuevos.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<ArticuloDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarArticuloDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="IModificarArticulo"/>
/// <remarks>
/// <para>
/// <b>AQUÍ VIVE LA OTRA MITAD DE «SoloResuelveLoViejo», y consiste en algo que NO se hace.</b> Lo
/// que este caso de uso vuelve a preguntar a los puertos es únicamente lo que <b>ha cambiado</b>.
/// </para>
/// <para>
/// Revalidar el impuesto que nadie ha tocado sería lo cómodo de escribir —una llamada, sin
/// condición— y convertiría la retirada de un maestro en un <b>congelador</b>: el día que un tramo
/// dejara de regir, cada artículo que lo propusiera se quedaría sin poder corregir ni su propia
/// descripción, porque el guardado entero se caería por un campo que el usuario no había tocado.
/// «Sigue resolviendo para lo que ya apunta a ella» dejaría de ser verdad exactamente ahí.
/// </para>
/// <para>
/// Y la unidad base es el caso extremo de lo mismo: no se revalida porque <b>no se puede
/// cambiar</b>. No está en el DTO, no está en <c>Articulo.Modificar</c>, y por tanto ninguna
/// unidad retirada puede impedir que se toque una ficha que la usa.
/// </para>
/// </remarks>
internal sealed class ModificarArticulo(
    IRepositorioDeArticulos articulos,
    IRepositorioDeCategorias categorias,
    IConsultaDeImpuestos impuestos,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    IVersionesDeCatalogo versiones,
    TimeProvider reloj) : IModificarArticulo
{
    public async Task<Resultado<ArticuloDto>> EjecutarAsync(
        Guid id,
        VersionDeRecurso version,
        ModificarArticuloDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Articulo? articulo = await articulos.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (articulo is null)
        {
            return Resultado.Fallo<ArticuloDto>(ErroresDeArticulo.NoEncontrado(id));
        }

        versiones.Exigir(articulo, version);

        TipoDeArticulo? tipo = TiposDeArticulo.Leer(peticion.Tipo);

        if (tipo is null)
        {
            return Resultado.Fallo<ArticuloDto>(
                ErroresDeArticulo.TipoNoValido(TiposDeArticulo.Admitidos));
        }

        // SOLO SI CAMBIA. Ver el comentario de la clase: revalidar lo que no se ha tocado
        // convierte la retirada de un maestro en un congelador para todo lo que lo usaba.
        if (peticion.ImpuestoPorDefectoId != articulo.ImpuestoPorDefectoId)
        {
            var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);

            EstadoDeMaestro estado = await impuestos
                .EstadoDeAsync(peticion.ImpuestoPorDefectoId, hoy, cancelacion)
                .ConfigureAwait(false);

            Resultado elImpuesto = ElMaestroSeOfreceParaLoNuevo.ElImpuesto(
                estado, peticion.ImpuestoPorDefectoId, hoy);

            if (!elImpuesto.EsCorrecto)
            {
                return Resultado.Fallo<ArticuloDto>(elImpuesto.Error!);
            }
        }

        // Mismo criterio con la categoría: solo se comprueba la que llega si es otra.
        if (peticion.CategoriaId != articulo.CategoriaId
            && peticion.CategoriaId is { } categoriaId
            && await categorias.EslabonAsync(categoriaId, cancelacion).ConfigureAwait(false) is null)
        {
            return Resultado.Fallo<ArticuloDto>(ErroresDeCategoria.NoEncontrada(categoriaId));
        }

        articulo.Modificar(
            peticion.Descripcion,
            tipo.Value,
            peticion.ImpuestoPorDefectoId,
            peticion.CategoriaId);

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(articulo.ADto());
    }
}
