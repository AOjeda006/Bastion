using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Contracts.Empresas;
using Bastion.Organizacion.Domain.Empresas;

namespace Bastion.Organizacion.Application.Empresas;

/// <summary>Busca empresas por un criterio que no puede viajar en la URL (ADR-0025).</summary>
/// <remarks>
/// <para>
/// <b>Devuelve <c>Resultado</c> y el listado no</b>, y la diferencia no es de gusto: aquí hay
/// entradas que pueden estar mal de una manera que el borde no sabe comprobar —un NIF cuyo
/// carácter de control no cuadra, un cursor ilegible, una búsqueda sin ningún criterio— y cada
/// una tiene una respuesta distinta que dar. Un listado no tiene ninguna de las tres: su
/// paginación la valida el modelo del borde y una colección vacía es una respuesta correcta
/// (ADR-0004).
/// </para>
/// </remarks>
public interface IBuscarEmpresas
{
    /// <summary>Ejecuta la búsqueda.</summary>
    /// <param name="peticion">Criterio y por dónde seguir, tal como llegaron en el cuerpo.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado<TramoDe<EmpresaDto>>> EjecutarAsync(
        BuscarEmpresasDto peticion,
        CancellationToken cancelacion);
}

/// <summary>
/// El criterio ya comprobado, tal como lo recibe el repositorio.
/// </summary>
/// <remarks>
/// El NIF llega como <see cref="Nif"/> y no como cadena, por lo mismo que en
/// <c>ExisteConNifAsync</c>: en la base es un valor convertido, así que EF Core sabe comparar el
/// objeto entero contra la columna y <b>no</b> sabe entrar en su <c>.Valor</c>. Con el tipo en la
/// firma, la versión que compila y revienta en ejecución no se puede escribir.
/// </remarks>
/// <param name="Nif">NIF exacto, ya validado, o nulo si no se busca por él.</param>
/// <param name="RazonSocial">Trozo de razón social, ya recortado, o nulo.</param>
public sealed record CriterioDeEmpresas(Nif? Nif, string? RazonSocial);

/// <inheritdoc cref="IBuscarEmpresas"/>
internal sealed class BuscarEmpresas(IRepositorioDeEmpresas empresas) : IBuscarEmpresas
{
    public async Task<Resultado<TramoDe<EmpresaDto>>> EjecutarAsync(
        BuscarEmpresasDto peticion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var errores = new ErroresPorCampo();
        Nif? nif = null;

        if (!string.IsNullOrWhiteSpace(peticion.Nif))
        {
            if (Nif.Intentar(peticion.Nif, out Nif? leido))
            {
                nif = leido;
            }
            else
            {
                errores.Agregar("nif", "No es un NIF español válido: revise el carácter de control.");
            }
        }

        string? razonSocial = string.IsNullOrWhiteSpace(peticion.RazonSocial)
            ? null
            : peticion.RazonSocial.Trim();

        // Una búsqueda sin criterio es el listado entero pedido por un camino que no pagina por
        // número y que además no queda en la caché de nadie. No se rechaza por purismo: se
        // rechaza porque el listado YA existe, con su tope y su orden, y tener dos formas de
        // pedir lo mismo garantiza que una de las dos envejezca sin que nadie la mire.
        if (nif is null && razonSocial is null && !errores.Hay)
        {
            errores.Agregar(
                "nif",
                "Indique al menos un criterio. Para ver todas las empresas está el listado " +
                "GET /api/v1/organizacion/empresas.");
        }

        Guid? desde = null;

        if (peticion.Cursor is not null)
        {
            if (Cursores.Intentar(peticion.Cursor, out Guid posicion))
            {
                desde = posicion;
            }
            else
            {
                errores.Agregar(
                    "cursor",
                    "No se entiende. Devuelva el cursor tal como vino en el tramo anterior, sin " +
                    "componerlo a mano.");
            }
        }

        if (errores.Hay)
        {
            return Resultado.Fallo<TramoDe<EmpresaDto>>(errores.AError());
        }

        TramoDe<Empresa> tramo = await empresas
            .BuscarAsync(new CriterioDeEmpresas(nif, razonSocial), desde, peticion.Tamanio, cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(
            new TramoDe<EmpresaDto>(
                [.. tramo.Elementos.Select(empresa => empresa.ADto())],
                tramo.Tamanio,
                tramo.CursorSiguiente));
    }
}
