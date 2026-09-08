using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Lo que una empresa compra, vende o fabrica: la ficha de la que cuelga todo lo demás.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es de la empresa (R8).</b> El artículo con código <c>TORN-8</c> de una ferretería no es el
/// de la otra, aunque se llamen igual.
/// </para>
/// <para>
/// <b>No es bloqueable, y está comprobado en vez de supuesto.</b> El artículo 32 de la LOPDGDD
/// —que es lo que hace bloqueable a un tercero y a una empresa (R16, ADR-0016)— habla de datos
/// personales, y un artículo no tiene ninguno: ni nombre de una persona, ni identificador fiscal,
/// ni domicilio. Lo comprueba <c>ElCatalogoNoGuardaDatosDeNadieTests</c> recorriendo el modelo, y
/// no de palabra: el día que a esta ficha se le cuelgue un dato de una persona —un responsable de
/// compras, un contacto del fabricante—, la respuesta deja de ser esta y hay que volver a
/// contestarla.
/// </para>
/// <para>
/// <b>Tampoco se retira.</b> La retirada del ADR-0023 es de los <b>cuatro maestros de
/// instalación</b>, que son de todas las empresas a la vez y por eso no se pueden borrar sin dejar
/// a nadie sin ellos. Un artículo es de una empresa, y qué le pasa cuando deja de venderse es una
/// pregunta de la fase 2, cuando tenga existencias que sostener. Hoy no tiene ninguna, y darle un
/// final de vida antes de que haya nada que conservar sería inventarse el motivo.
/// </para>
/// <para>
/// <b>La unidad base y el impuesto son <c>Guid</c> de otro módulo, sin clave ajena</b> (§5, regla
/// 4: ninguna consulta cruza esquemas). Lo único que impide que ahí acabe un identificador
/// inventado son los puertos del ítem 1.2 —<c>IConsultaDeUnidadesDeMedida</c> e
/// <c>IConsultaDeImpuestos</c>—, que consulta la capa de aplicación antes de construir esta ficha
/// (ADR-0024). El dominio no le pregunta nada a nadie.
/// </para>
/// </remarks>
public sealed class Articulo : EntidadBase, IDeInquilino
{
    /// <summary>Tope del código del artículo: cabe en una línea de albarán y en una etiqueta.</summary>
    public const int LongitudMaximaDeCodigo = 30;

    /// <summary>Tope de la descripción, que es lo que sale impreso en una factura.</summary>
    public const int LongitudMaximaDeDescripcion = 200;

    private Articulo(
        Guid id,
        Guid empresaId,
        string codigo,
        string descripcion,
        TipoDeArticulo tipo,
        Guid unidadBaseId,
        Guid impuestoPorDefectoId,
        Guid? categoriaId,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Codigo = codigo;
        Descripcion = descripcion;
        Tipo = tipo;
        UnidadBaseId = unidadBaseId;
        ImpuestoPorDefectoId = impuestoPorDefectoId;
        CategoriaId = categoriaId;
    }

    private Articulo()
    {
        Codigo = null!;
        Descripcion = null!;
    }

    /// <summary>Identificador del artículo.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Código del artículo, en mayúsculas. No cambia: ya está impreso fuera.</summary>
    public string Codigo { get; private set; }

    /// <summary>Descripción con la que se muestra y con la que se factura.</summary>
    public string Descripcion { get; private set; }

    /// <summary>Si es mercancía o prestación.</summary>
    public TipoDeArticulo Tipo { get; private set; }

    /// <summary>
    /// Unidad en la que se cuenta este artículo, del maestro de Organización.
    /// </summary>
    /// <remarks>
    /// <b>No cambia nunca</b>, y por el mismo motivo por el que <c>UnidadMedida.Decimales</c>
    /// tampoco: pasar un artículo de kilos a unidades reinterpretaría cada existencia, cada línea
    /// de albarán y cada valoración ya registradas, sin que nadie hubiera movido mercancía. Medir
    /// de otra manera es un artículo nuevo, o una conversión declarada.
    /// </remarks>
    public Guid UnidadBaseId { get; private set; }

    /// <summary>
    /// Tramo de impuesto que se propone al facturarlo, del maestro de Organización.
    /// </summary>
    /// <remarks>
    /// <b>Sí cambia</b>, al revés que la unidad: un tipo impositivo se mueve por el BOE —el general
    /// del IVA pasó del 18 % al 21 % el 1 de septiembre de 2012— y la ficha tiene que poder
    /// seguirlo sin dar de alta un artículo nuevo. Es «por defecto» y no «el impuesto»: lo que se
    /// factura de verdad lo decide la factura con su fecha de devengo, en la fase 5.
    /// </remarks>
    public Guid ImpuestoPorDefectoId { get; private set; }

    /// <summary>Categoría en la que se clasifica, o nula si todavía no se ha clasificado.</summary>
    /// <remarks>
    /// Anulable a propósito: obligar a clasificar en el alta llenaría el árbol de una categoría
    /// «varios» que no clasifica nada, y la tarifa por categoría del ítem 1.9 tiene que poder
    /// decir «este artículo no la tiene» en vez de encontrarse un cajón de sastre.
    /// </remarks>
    public Guid? CategoriaId { get; private set; }

    /// <summary>Da de alta un artículo.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="codigo">Código; se normaliza a mayúsculas.</param>
    /// <param name="descripcion">Lo que sale impreso.</param>
    /// <param name="tipo">Si es mercancía o prestación.</param>
    /// <param name="unidadBaseId">Unidad en la que se cuenta, ya validada contra su puerto.</param>
    /// <param name="impuestoPorDefectoId">Tramo de impuesto, ya validado contra su puerto.</param>
    /// <param name="categoriaId">Categoría, o nula.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Articulo Crear(
        Guid empresaId,
        string codigo,
        string descripcion,
        TipoDeArticulo tipo,
        Guid unidadBaseId,
        Guid impuestoPorDefectoId,
        Guid? categoriaId,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un artículo pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        // Los dos identificadores ajenos vienen ya validados contra su puerto, pero un `Guid.Empty`
        // no llega a preguntarse: es el valor por omisión de la estructura, o sea justo lo que
        // aparece cuando alguien construye la ficha por un camino que no pasó por la validación.
        if (unidadBaseId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un artículo se cuenta en alguna unidad.", nameof(unidadBaseId));
        }

        if (impuestoPorDefectoId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un artículo propone algún impuesto.", nameof(impuestoPorDefectoId));
        }

        return new Articulo(
            Guid.CreateVersion7(),
            empresaId,
            CodigoValido(codigo),
            DescripcionValida(descripcion),
            tipo,
            unidadBaseId,
            impuestoPorDefectoId,
            CategoriaValida(categoriaId),
            momento);
    }

    /// <summary>Cambia lo que se puede cambiar. Ni el código ni la unidad base.</summary>
    /// <param name="descripcion">Lo que sale impreso.</param>
    /// <param name="tipo">Si es mercancía o prestación.</param>
    /// <param name="impuestoPorDefectoId">Tramo de impuesto, ya validado si ha cambiado.</param>
    /// <param name="categoriaId">Categoría, o nula.</param>
    public void Modificar(
        string descripcion,
        TipoDeArticulo tipo,
        Guid impuestoPorDefectoId,
        Guid? categoriaId)
    {
        if (impuestoPorDefectoId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un artículo propone algún impuesto.", nameof(impuestoPorDefectoId));
        }

        Descripcion = DescripcionValida(descripcion);
        Tipo = tipo;
        ImpuestoPorDefectoId = impuestoPorDefectoId;
        CategoriaId = CategoriaValida(categoriaId);
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static Guid? CategoriaValida(Guid? categoriaId) =>
        categoriaId == Guid.Empty ? null : categoriaId;

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de artículo admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string DescripcionValida(string descripcion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);

        string limpia = descripcion.Trim();

        return limpia.Length <= LongitudMaximaDeDescripcion
            ? limpia
            : throw new ArgumentException(
                $"La descripción admite {LongitudMaximaDeDescripcion} caracteres como máximo.",
                nameof(descripcion));
    }
}
