using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Un código de barras de un artículo: el GTIN que lleva, el nivel en el que va impreso y las
/// unidades base que contiene.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se llama como en el §7.3 del plan maestro</b>, que lo pone entre los hijos del artículo junto
/// al proveedor (ADR-0052). El GTIN es lo que lleva, y es un objeto de valor aparte, <c>Gtin</c>.
/// </para>
/// <para>
/// <b>Es agregado propio y no una colección dentro del artículo</b> (ADR-0051 §2), como el proveedor
/// del artículo y la línea de tarifa (ADR-0010): dar de alta un GTIN no carga los demás, y el
/// artículo no cambia de versión. Su ETag sigue diciendo la verdad.
/// </para>
/// <para>
/// <b>No tiene mutadores.</b> Un GTIN mal tecleado se quita y se da de alta el bueno. Y las unidades
/// tampoco cambian: el <i>GTIN Management Standard</i> pide un GTIN nuevo cuando cambia lo que lleva
/// una agrupación.
/// </para>
/// <para>
/// Lleva <c>EmpresaId</c> propio por lo mismo que <c>ArticuloProveedor</c>: el filtro de la R8 se
/// evalúa sobre las columnas de la fila, y el índice único va por empresa. En otra empresa, el mismo
/// GTIN entra.
/// </para>
/// </remarks>
public sealed class CodigoBarras : EntidadBase, IDeInquilino
{
    /// <summary>Las unidades base de la base: una, la que se vende.</summary>
    public const int UnidadesDeLaBase = 1;

    /// <summary>Lo menos que lleva una caja o un palé. Una caja de una es la base con otro código.</summary>
    public const int UnidadesMinimasDeUnaAgrupacion = 2;

    private CodigoBarras(
        Guid id,
        Guid empresaId,
        Guid articuloId,
        Gtin gtin,
        NivelDeGtin nivel,
        int unidades,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        ArticuloId = articuloId;
        Gtin = gtin;
        Nivel = nivel;
        Unidades = unidades;
    }

    private CodigoBarras()
    {
        Gtin = null!;
    }

    /// <summary>Identificador de la fila. El GTIN no es clave de nada.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Artículo al que identifica.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>El GTIN, en sus catorce dígitos.</summary>
    public Gtin Gtin { get; private set; }

    /// <summary>En qué agrupación va impreso.</summary>
    public NivelDeGtin Nivel { get; private set; }

    /// <summary>Cuántas unidades base lleva: una la base, dos o más la caja y el palé.</summary>
    public int Unidades { get; private set; }

    /// <summary>Declara un código de barras de un artículo.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="articuloId">Artículo al que identifica, ya comprobado.</param>
    /// <param name="gtin">El GTIN, ya leído.</param>
    /// <param name="nivel">En qué agrupación va impreso.</param>
    /// <param name="unidades">Unidades base que lleva: una la base, dos o más la caja y el palé.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    /// <exception cref="ArgumentException">Falta la empresa o el artículo.</exception>
    /// <exception cref="ArgumentOutOfRangeException">El nivel o sus unidades no son de los que hay.</exception>
    public static CodigoBarras Nuevo(
        Guid empresaId,
        Guid articuloId,
        Gtin gtin,
        NivelDeGtin nivel,
        int unidades,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Un código de barras es de alguna empresa.", nameof(empresaId));
        }

        if (articuloId == Guid.Empty)
        {
            throw new ArgumentException("Un código de barras es de algún artículo.", nameof(articuloId));
        }

        ArgumentNullException.ThrowIfNull(gtin);

        if (!Enum.IsDefined(nivel))
        {
            throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "El nivel es la base, la caja o el palé.");
        }

        // El caso de uso ya lo comprobó y dio su error con nombre: llegar aquí es un defecto.
        bool cuadran = nivel == NivelDeGtin.Base
            ? unidades == UnidadesDeLaBase
            : unidades >= UnidadesMinimasDeUnaAgrupacion;

        if (!cuadran)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unidades),
                unidades,
                $"La base lleva {UnidadesDeLaBase} unidad, y la caja y el palé, " +
                $"{UnidadesMinimasDeUnaAgrupacion} o más.");
        }

        return new CodigoBarras(Guid.CreateVersion7(), empresaId, articuloId, gtin, nivel, unidades, momento);
    }
}
