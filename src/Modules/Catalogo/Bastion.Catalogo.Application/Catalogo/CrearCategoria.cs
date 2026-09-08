using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Catalogo.Application.Comun;
using Bastion.Catalogo.Contracts.Catalogo;
using Bastion.Catalogo.Domain.Catalogo;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Catalogo.Application.Catalogo;

/// <summary>Da de alta una categoría.</summary>
public interface ICrearCategoria
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="peticion">Datos de la categoría.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<CategoriaDto>> EjecutarAsync(
        CrearCategoriaDto peticion,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICrearCategoria"/>
internal sealed class CrearCategoria(
    IUsuarioActual usuarioActual,
    IRepositorioDeCategorias categorias,
    IConsultaDeEmpresas empresas,
    IUnidadTrabajoDeCatalogo unidadTrabajo,
    TimeProvider reloj) : ICrearCategoria
{
    public async Task<Resultado<CategoriaDto>> EjecutarAsync(
        CrearCategoriaDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        Guid empresaId = usuarioActual.EmpresaId;

        if (!await empresas.EstaActivaAsync(empresaId, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<CategoriaDto>(ErroresDeInquilinato.EmpresaActivaNoOperativa());
        }

        string codigo = Categoria.NormalizarCodigo(peticion.Codigo);

        if (await categorias.ExisteElCodigoAsync(empresaId, codigo, cancelacion).ConfigureAwait(false))
        {
            return Resultado.Fallo<CategoriaDto>(ErroresDeCategoria.CodigoDuplicado(codigo));
        }

        // LA MISMA COMPROBACIÓN QUE EN LA MODIFICACIÓN, Y NO ES UN TRÁMITE — pero tampoco hace
        // aquí lo mismo, y conviene decirlo en vez de dejarlo creer.
        //
        // Se pasa `Guid.Empty` como identificador de la categoría que se cuelga porque todavía no
        // tiene ninguno: nace en `Categoria.Crear`, unas líneas más abajo. Eso deja la rama del
        // CICLO estructuralmente inalcanzable en el alta —ningún antepasado puede ser una fila que
        // aún no existe—, y las otras dos ramas del recorrido bien vivas: que el padre exista, y
        // que colgar de él no pase de la profundidad máxima. La llamada no es vacua; lo que sería
        // falso es contar el alta como el sitio donde se atrapan los ciclos.
        Resultado elArbol = await ElArbolSigueSiendoUnArbol
            .ComprobarAsync(Guid.Empty, peticion.PadreId, categorias, cancelacion)
            .ConfigureAwait(false);

        if (!elArbol.EsCorrecto)
        {
            return Resultado.Fallo<CategoriaDto>(elArbol.Error!);
        }

        var categoria = Categoria.Crear(
            empresaId,
            codigo,
            peticion.Nombre,
            peticion.PadreId,
            reloj.GetUtcNow());

        categorias.Agregar(categoria);
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto(categoria.ADto());
    }
}
