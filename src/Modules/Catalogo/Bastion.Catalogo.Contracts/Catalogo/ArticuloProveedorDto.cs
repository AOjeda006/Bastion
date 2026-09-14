using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Quién suministra un artículo, tal como sale de la API.</summary>
/// <remarks>
/// <para>
/// <b>Sale el identificador del tercero y nada más de él</b>: ni razón social, ni identificador
/// fiscal, ni domicilio. No es una omisión de comodidad — es la frontera: la ficha es de Terceros,
/// y rellenarla aquí exigiría un puerto que la devolviera, lo que convertiría a cualquier módulo en
/// un lector de datos personales sin permiso ni traza. Quien tenga permiso de terceros pide la
/// ficha a <c>/api/v1/terceros/terceros/{id}</c>, que es quien además sabe si puede enseñarla.
/// </para>
/// <para>
/// <b>Y lo que sale ya viene filtrado por el art. 32.</b> Un suministro cuyo tercero está bloqueado
/// no aparece en el listado. El filtro no lo pone la pantalla: lo pone el caso de uso, preguntando
/// a Terceros, porque lo que la pantalla no pinta sigue estando en el JSON.
/// </para>
/// <para>
/// <b>La lectura de una fila, <c>ObtenerProveedorDelArticulo</c>, no pregunta, y es correcto solo
/// porque este DTO no publica ningún dato de una persona:</b> el día que lleve el nombre del
/// proveedor, esa lectura publicará a quien el listado esconde.
/// </para>
/// </remarks>
/// <param name="Id">Identificador de la fila.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="ArticuloId">Artículo que se suministra.</param>
/// <param name="TerceroId">Quien lo suministra, del módulo de Terceros.</param>
/// <param name="ReferenciaDelProveedor">Su código para este artículo, o nulo.</param>
public sealed record ArticuloProveedorDto(
    Guid Id,
    Guid EmpresaId,
    Guid ArticuloId,
    Guid TerceroId,
    string? ReferenciaDelProveedor);

/// <summary>Lo que hace falta para declarar que un tercero suministra un artículo.</summary>
/// <remarks>
/// <b>No lleva empresa</b>, y no puede llevarla: sale del claim de la sesión y nunca del cuerpo
/// (R8). Y no lleva artículo: va en la ruta, porque un suministro no existe sin el artículo del
/// que cuelga.
/// </remarks>
public sealed record AgregarProveedorDto
{
    /// <summary>El tercero que lo suministra. Tiene que existir, estar activo y ser proveedor.</summary>
    [Required(ErrorMessage = "El proveedor es obligatorio.")]
    public Guid TerceroId { get; init; }

    /// <summary>Con qué código llama él a este artículo, si tiene uno propio.</summary>
    [StringLength(50, ErrorMessage = "La referencia del proveedor no puede pasar de {1} caracteres.")]
    public string? ReferenciaDelProveedor { get; init; }
}

/// <summary>Lo que se puede cambiar de un suministro ya declarado.</summary>
/// <remarks>
/// <b>Solo la referencia.</b> Ni el artículo ni el tercero están aquí: cambiar cualquiera de los
/// dos no es modificar este suministro, es otro. Al no estar en el contrato no hay ni manera de
/// intentarlo, que es la misma forma en que el identificador fiscal no se puede cambiar en una
/// ficha de tercero.
/// </remarks>
public sealed record ModificarProveedorDto
{
    /// <summary>El código nuevo, o vacío para dejar de tener uno.</summary>
    [StringLength(50, ErrorMessage = "La referencia del proveedor no puede pasar de {1} caracteres.")]
    public string? ReferenciaDelProveedor { get; init; }
}
