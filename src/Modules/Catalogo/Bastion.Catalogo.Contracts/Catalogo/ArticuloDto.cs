using System.ComponentModel.DataAnnotations;

namespace Bastion.Catalogo.Contracts.Catalogo;

/// <summary>Un artículo, tal como sale de la API.</summary>
/// <remarks>
/// <para>
/// <b>La unidad y el impuesto salen como identificadores desnudos, no expandidos.</b> Poner aquí
/// el símbolo de la unidad o el porcentaje del tramo obligaría a este módulo a leer de
/// <c>organizacion.unidades_de_medida</c>, que es exactamente el <c>JOIN</c> entre esquemas que la
/// regla 4 prohíbe; hacerlo por el puerto sería una consulta por artículo y por pantalla. Quien
/// pinta una lista de artículos pide el maestro de unidades una vez y lo cruza en el cliente.
/// </para>
/// <para>
/// <b>Sin campo de retirada ni de bloqueo</b>, y las dos ausencias tienen motivo. Un artículo no
/// se retira —eso es de los cuatro maestros de instalación (ADR-0023)— y no es bloqueable, porque
/// no guarda ningún dato de una persona (R16, art. 32 de la LOPDGDD). Lo segundo está comprobado
/// recorriendo el modelo y no dicho de palabra.
/// </para>
/// </remarks>
/// <param name="Id">Identificador del artículo.</param>
/// <param name="EmpresaId">Empresa a la que pertenece la ficha (R8).</param>
/// <param name="Codigo">Código, en mayúsculas. No cambia.</param>
/// <param name="Descripcion">Lo que sale impreso en una factura.</param>
/// <param name="Tipo">Si es mercancía o prestación, como texto: <c>Bien</c> o <c>Servicio</c>.</param>
/// <param name="UnidadBaseId">Unidad en la que se cuenta, del maestro de Organización.</param>
/// <param name="ImpuestoPorDefectoId">Tramo de impuesto propuesto, del maestro de Organización.</param>
/// <param name="CategoriaId">Categoría en la que se clasifica, o nula.</param>
public sealed record ArticuloDto(
    Guid Id,
    Guid EmpresaId,
    string Codigo,
    string Descripcion,
    string Tipo,
    Guid UnidadBaseId,
    Guid ImpuestoPorDefectoId,
    Guid? CategoriaId);

/// <summary>Lo que hace falta para dar de alta un artículo.</summary>
/// <remarks>
/// <b>No lleva empresa</b>, y no puede llevarla: la empresa sale del <i>claim</i> de la sesión y
/// nunca del cuerpo (R8). Al no estar en el contrato, no hay ni siquiera manera de intentarlo.
/// </remarks>
public sealed record CrearArticuloDto
{
    /// <summary>Código del artículo. Se normaliza a mayúsculas.</summary>
    [Required(ErrorMessage = "El código del artículo es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede pasar de {1} caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    /// <summary>Descripción con la que se muestra y con la que se factura.</summary>
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede pasar de {1} caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Si es mercancía o prestación: <c>Bien</c> o <c>Servicio</c>.</summary>
    /// <remarks>
    /// Como texto y no como número, por lo mismo que el territorio fiscal de un tercero: un
    /// ordinal ata el contrato al orden en que están escritos los valores del enumerado, y ese
    /// orden es un detalle interno que alguien puede reordenar sin creer que rompe nada.
    /// </remarks>
    [Required(ErrorMessage = "El tipo de artículo es obligatorio.")]
    public string Tipo { get; init; } = string.Empty;

    /// <summary>
    /// Unidad en la que se cuenta este artículo. Tiene que existir y <b>ofrecerse para lo nuevo</b>.
    /// </summary>
    /// <remarks>
    /// No es una clave ajena: la unidad vive en el esquema de Organización y entre esquemas no hay
    /// claves ajenas (§5, regla 4). Lo que impide que aquí acabe un identificador inventado —o el
    /// de una unidad <b>retirada</b>— es <c>IConsultaDeUnidadesDeMedida</c>, que el alta pregunta
    /// antes de construir la ficha (ADR-0024).
    /// </remarks>
    [Required(ErrorMessage = "La unidad base es obligatoria.")]
    public Guid UnidadBaseId { get; init; }

    /// <summary>Tramo de impuesto que se propone al facturarlo.</summary>
    /// <remarks>
    /// Tampoco es clave ajena, y se comprueba contra <c>IConsultaDeImpuestos</c> <b>con la fecha
    /// del alta como devengo</b>: un tramo que ya no rige hoy sigue resolviendo facturas viejas,
    /// pero no se propone para un artículo nuevo.
    /// </remarks>
    [Required(ErrorMessage = "El impuesto por defecto es obligatorio.")]
    public Guid ImpuestoPorDefectoId { get; init; }

    /// <summary>Categoría en la que se clasifica, o nula para dejarlo sin clasificar.</summary>
    public Guid? CategoriaId { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de un artículo ya dado de alta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin código y sin unidad base, y las dos ausencias son la decisión.</b> El código ya está
/// impreso en albaranes y etiquetas fuera del sistema. La unidad base es peor todavía: cambiarla
/// reinterpretaría cada existencia, cada línea y cada valoración ya registradas <b>sin que nadie
/// hubiera movido mercancía</b>, que es el mismo argumento por el que <c>UnidadMedida.Decimales</c>
/// tampoco se toca. Medir de otra manera es un artículo nuevo.
/// </para>
/// <para>
/// Que no estén en el contrato no es lo único que lo impide: el agregado no tiene por dónde
/// cambiarlas. <c>Articulo.Modificar</c> no recibe ninguna de las dos.
/// </para>
/// </remarks>
public sealed record ModificarArticuloDto
{
    /// <summary>Descripción con la que se muestra y con la que se factura.</summary>
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede pasar de {1} caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Si es mercancía o prestación: <c>Bien</c> o <c>Servicio</c>.</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Tramo de impuesto que se propone al facturarlo.</summary>
    /// <remarks>
    /// <b>Solo se vuelve a preguntar al puerto si CAMBIA</b>, y eso no es una optimización: es la
    /// otra mitad de lo que significa <c>SoloResuelveLoViejo</c>. Revalidar un impuesto que nadie
    /// ha tocado convertiría la retirada de un maestro en un congelador — cada artículo que lo
    /// usara dejaría de poder corregir su propia descripción.
    /// </remarks>
    [Required(ErrorMessage = "El impuesto por defecto es obligatorio.")]
    public Guid ImpuestoPorDefectoId { get; init; }

    /// <summary>Categoría en la que se clasifica, o nula para dejarlo sin clasificar.</summary>
    public Guid? CategoriaId { get; init; }
}
