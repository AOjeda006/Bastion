using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Quién suministra un artículo, y con qué referencia lo llama él.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el primer cruce de Catálogo hacia Terceros, y el primero MUTUO del proyecto.</b> Los tres
/// cruces que había apuntaban todos a <c>Organizacion.Contracts</c>: un módulo dueño publicaba una
/// lectura y los demás preguntaban, sin que nadie le preguntara a ellos. Aquí Catálogo mira a
/// Terceros y Terceros mira a Catálogo —<c>Tercero.TarifaAsignada</c>—, y por eso los dos van en el
/// mismo ítem: la dependencia mutua se ve entera o no se ve.
/// </para>
/// <para>
/// <b><see cref="TerceroId"/> es un <c>Guid</c> de otro módulo, sin clave ajena</b> (§5, regla 4:
/// ninguna consulta cruza esquemas). Lo único que impide que ahí acabe un identificador inventado
/// es <c>IConsultaDeTerceros</c>, que la capa de aplicación pregunta antes de construir esta fila
/// (ADR-0024). El dominio no le pregunta nada a nadie.
/// </para>
/// <para>
/// <b>Y el puerto no contesta «¿existe?».</b> Un tercero tiene dos ejes —qué papeles hace y si está
/// bloqueado— y los dos deciden si puede suministrar: uno que no sea proveedor no lo es por no
/// estar en esta tabla, y uno bloqueado tiene sus datos reservados por el art. 32 de la LOPDGDD.
/// Un booleano de existencia dejaría entrar a los dos.
/// </para>
/// <para>
/// <b>Lleva <c>EmpresaId</c> propio</b> aunque su artículo ya lo tenga, por lo mismo que
/// <c>LineaTarifa</c>: el filtro global de la R8 se escribe por entidad y se evalúa sobre las
/// columnas de la fila. Sin la columna, una consulta que empezara por aquí traería las de otra
/// empresa. Y aquí importa el doble, porque lo que saldría de otra empresa es <b>con quién trabaja
/// la competencia</b>.
/// </para>
/// <para>
/// <b>Es agregado propio y no una colección dentro del artículo</b>, igual que la línea de tarifa:
/// añadir un proveedor no tiene por qué cargar los demás, y la R12 —una transacción, un agregado—
/// dice que no se manipulen hijos a través de la colección del padre (ADR-0010).
/// </para>
/// <para>
/// <b>ESTA ENTIDAD CAMBIA LA RESPUESTA DEL ART. 32 PARA CATÁLOGO, y no por lo que guarda sino por
/// lo que señala.</b> No hay aquí ni un nombre, ni un identificador fiscal, ni un domicilio: hay un
/// <c>Guid</c>. Pero ese <c>Guid</c> apunta a una ficha que puede ser la de una persona física —un
/// autónomo es un tercero como cualquier otro—, así que la fila «este artículo lo suministra X» es
/// un dato de X. Por eso la lectura filtra por el bloqueo del tercero al que apunta, y por eso ese
/// filtro está en el repositorio y no en la pantalla; el razonamiento entero, en
/// <c>ProveedoresDelArticulo</c> y en <c>ElCatalogoNoGuardaDatosDeNadieTests</c>, que fue quien
/// obligó a contestar la pregunta al ponerse rojo con esta clase.
/// </para>
/// </remarks>
public sealed class ArticuloProveedor : EntidadBase, IDeInquilino
{
    /// <summary>
    /// Tope de la referencia del proveedor. La misma que el código del artículo más margen: es un
    /// código ajeno y no se le pueden imponer nuestras costumbres.
    /// </summary>
    public const int LongitudMaximaDeReferencia = 50;

    private ArticuloProveedor(
        Guid id,
        Guid empresaId,
        Guid articuloId,
        Guid terceroId,
        string? referenciaDelProveedor,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        ArticuloId = articuloId;
        TerceroId = terceroId;
        ReferenciaDelProveedor = referenciaDelProveedor;
    }

    private ArticuloProveedor()
    {
    }

    /// <summary>Identificador de la fila.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Artículo que se suministra.</summary>
    public Guid ArticuloId { get; private set; }

    /// <summary>
    /// Tercero que lo suministra, del módulo de Terceros.
    /// </summary>
    /// <remarks>
    /// Sin clave ajena —cruza esquemas— y sin nombre al lado: lo que aquí se guarda es a quién
    /// apuntar, no quién es. Quien quiera saber quién es se lo pregunta a Terceros, que es quien
    /// sabe además si puede decirlo.
    /// </remarks>
    public Guid TerceroId { get; private set; }

    /// <summary>
    /// Con qué código llama el proveedor a este artículo, o nulo si se pide por el nuestro.
    /// </summary>
    /// <remarks>
    /// Es el motivo de que esto sea una entidad y no una lista de identificadores: un pedido de
    /// compra sale con la referencia del que lo recibe, y cada proveedor tiene la suya para la
    /// misma mercancía. Anulable porque la mitad de las veces no hay otra: se pide por el nuestro.
    /// </remarks>
    public string? ReferenciaDelProveedor { get; private set; }

    /// <summary>Declara que un tercero suministra un artículo.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="articuloId">Artículo que se suministra, ya comprobado.</param>
    /// <param name="terceroId">Quien lo suministra, <b>ya preguntado por su puerto</b>.</param>
    /// <param name="referenciaDelProveedor">Su código para este artículo, o nulo.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static ArticuloProveedor Nuevo(
        Guid empresaId,
        Guid articuloId,
        Guid terceroId,
        string? referenciaDelProveedor,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("Un suministro es de alguna empresa.", nameof(empresaId));
        }

        if (articuloId == Guid.Empty)
        {
            throw new ArgumentException("Un suministro es de algún artículo.", nameof(articuloId));
        }

        if (terceroId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un suministro lo hace algún tercero.", nameof(terceroId));
        }

        return new ArticuloProveedor(
            Guid.CreateVersion7(),
            empresaId,
            articuloId,
            terceroId,
            Normalizar(referenciaDelProveedor),
            momento);
    }

    /// <summary>Cambia la referencia con la que el proveedor llama a este artículo.</summary>
    /// <remarks>
    /// Sin <c>momento</c>: <c>ModificadoEn</c> lo sella el interceptor de auditoría al guardar,
    /// como en los mutadores del tercero y del artículo. Quien decide la hora es uno solo.
    /// </remarks>
    /// <param name="referenciaDelProveedor">La nueva, o nulo para dejar de tenerla.</param>
    public void CambiarReferencia(string? referenciaDelProveedor) =>
        ReferenciaDelProveedor = Normalizar(referenciaDelProveedor);

    private static string? Normalizar(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
        {
            return null;
        }

        string limpia = referencia.Trim();

        return limpia.Length > LongitudMaximaDeReferencia
            ? throw new ArgumentException(
                $"La referencia del proveedor no pasa de {LongitudMaximaDeReferencia} caracteres.",
                nameof(referencia))
            : limpia;
    }
}
