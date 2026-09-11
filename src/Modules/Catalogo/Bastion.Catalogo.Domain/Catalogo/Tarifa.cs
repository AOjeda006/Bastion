using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Catalogo.Domain.Catalogo;

/// <summary>
/// Una lista de precios de la empresa, con la divisa en la que se expresa y el tramo de fechas en
/// el que rige.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es de la empresa (R8).</b> Los precios a los que vende una ferretería no son los de la
/// imprenta de al lado, y al revés que las unidades o los impuestos no hay nada que compartir.
/// </para>
/// <para>
/// <b>Una tarifa no se edita: se sucede</b>, exactamente como un tramo de impuesto y por el mismo
/// motivo. Los precios de esta fila son los que se aplicaron a los documentos emitidos mientras
/// rigió; cambiarlos sobre la fila los reescribiría hacia atrás la próxima vez que alguien
/// reimprimiera un albarán. Por eso <see cref="Modificar"/> solo toca el nombre: subir precios es
/// <b>cerrar</b> la fila vigente y <b>crear</b> la siguiente, con el mismo código.
/// </para>
/// <para>
/// <b>El código NO es único, y esa es la forma correcta.</b> <c>PVP</c> tiene tantas filas como
/// veces se han revisado sus precios. Lo que no puede haber es <b>solape</b>: dos filas del mismo
/// código cuyos tramos se pisen harían que «la tarifa PVP del día D» devolviera dos respuestas, y
/// la consulta elegiría una según el orden del plan de ejecución — el síntoma no sería un error,
/// sería un precio distinto de un día para otro sin que nadie hubiera tocado nada. Lo impide una
/// <b>restricción de exclusión</b> en la base y no un caso de uso: una regla que solo vive en el
/// caso de uso deja abierto cualquier otro camino de escritura, presente o futuro.
/// </para>
/// <para>
/// <b>Los extremos de la vigencia son CERRADOS por los dos lados</b>, igual que los del impuesto:
/// «del 1 de enero al 31 de diciembre» incluye los dos días, que es como lo escribe quien acuerda
/// una tarifa. La restricción de la base construye el mismo rango con <c>'[]'</c> y
/// <see cref="RigeEl"/> contesta lo mismo, así que no hay dos convenciones que puedan separarse —
/// que es lo que pasaría si esta tabla usara <c>'[)'</c> y la de al lado <c>'[]'</c>, y el día de
/// la frontera valdría lo que dijera la última que se leyó.
/// </para>
/// <para>
/// <b>La divisa es un <c>Guid</c> de Organización, sin clave ajena</b> (§5, regla 4). Lo único que
/// impide que ahí acabe un identificador inventado es <c>IConsultaDeDivisas</c>, el puerto que el
/// ítem 1.2 declaró diciendo —con estas palabras— que su consumidor sería «la tarifa del §7.3».
/// Este es el sitio. Y una divisa <b>retirada</b> no funda una tarifa nueva, pero la tarifa que ya
/// la usaba sigue resolviéndola: las dos mitades del ADR-0023, otra vez.
/// </para>
/// <para>
/// <b>Que la divisa no sea la de la empresa se acepta a propósito.</b> Una tarifa de exportación en
/// dólares es legítima, y rechazarla obligaría a montar una instalación por mercado. Lo que hace
/// que eso no sea peligroso no es una comprobación: es que <b>la divisa viaja siempre pegada al
/// precio resuelto</b>, así que nadie puede confundirla con la de la empresa. Convertir es otra
/// cosa —<c>TipoCambio</c> y la facturación de la fase 5—, y no es de este ítem.
/// </para>
/// </remarks>
public sealed class Tarifa : EntidadBase, IDeInquilino
{
    /// <summary>Tope del código de la tarifa: cabe en la cabecera de un presupuesto.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    /// <summary>Tope del nombre con el que se muestra.</summary>
    public const int LongitudMaximaDeNombre = 100;

    private Tarifa(
        Guid id,
        Guid empresaId,
        string codigo,
        string nombre,
        Guid divisaId,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Codigo = codigo;
        Nombre = nombre;
        DivisaId = divisaId;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }

    private Tarifa()
    {
        Codigo = null!;
        Nombre = null!;
    }

    /// <summary>Identificador de la tarifa.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Código de la tarifa, en mayúsculas. Se repite entre tramos de la misma tarifa.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>Divisa en la que se expresan sus precios, del maestro de Organización.</summary>
    /// <remarks>
    /// <b>No cambia</b>, por lo mismo que no cambia el porcentaje de un tramo de impuesto: los
    /// precios que cuelgan de esta fila están escritos en esta divisa, y cambiar la columna los
    /// reinterpretaría todos de golpe sin tocar ni un número. Vender en otra divisa es otra tarifa.
    /// </remarks>
    public Guid DivisaId { get; private set; }

    /// <summary>Primer día en que rige. Fecha de negocio: sin hora y sin zona (R14).</summary>
    public DateOnly VigenteDesde { get; private set; }

    /// <summary>Último día en que rige, o nulo mientras siga vigente.</summary>
    public DateOnly? VigenteHasta { get; private set; }

    /// <summary>Si la tarifa rige el día indicado, con los dos extremos incluidos.</summary>
    /// <param name="dia">Día para el que se quiere el precio.</param>
    public bool RigeEl(DateOnly dia) =>
        dia >= VigenteDesde && (VigenteHasta is null || dia <= VigenteHasta);

    /// <summary>Da de alta un tramo de tarifa.</summary>
    /// <param name="empresaId">Empresa a la que pertenece (R8), que sale del <i>claim</i>.</param>
    /// <param name="codigo">Código; se normaliza a mayúsculas.</param>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="divisaId">Divisa, ya validada contra su puerto.</param>
    /// <param name="vigenteDesde">Primer día en que rige.</param>
    /// <param name="vigenteHasta">Último día en que rige, o nulo si sigue vigente.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Tarifa Crear(
        Guid empresaId,
        string codigo,
        string nombre,
        Guid divisaId,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        DateTimeOffset momento)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una tarifa pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        // Igual que en el artículo: el identificador ajeno viene ya preguntado a su puerto, pero un
        // `Guid.Empty` no llega a preguntarse —es el valor por omisión de la estructura, o sea lo
        // que aparece cuando alguien construye por un camino que no pasó por la validación—.
        if (divisaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Una tarifa se expresa en alguna divisa.", nameof(divisaId));
        }

        return new Tarifa(
            Guid.CreateVersion7(),
            empresaId,
            CodigoValido(codigo),
            NombreValido(nombre),
            divisaId,
            vigenteDesde,
            HastaValido(vigenteDesde, vigenteHasta),
            momento);
    }

    /// <summary>Cambia el nombre. Ni el código, ni la divisa, ni el tramo.</summary>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    public void Modificar(string nombre) => Nombre = NombreValido(nombre);

    /// <summary>Cierra el tramo el día indicado, que es como se sustituye una tarifa por otra.</summary>
    /// <param name="ultimoDia">Último día en que rige.</param>
    public void Cerrar(DateOnly ultimoDia)
    {
        if (ultimoDia < VigenteDesde)
        {
            throw new ArgumentException(
                "Una tarifa no puede dejar de regir antes de empezar a regir.", nameof(ultimoDia));
        }

        VigenteHasta = ultimoDia;
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública por lo mismo que la del artículo y la del impuesto: quien busca los tramos de un
    /// código —para resolver un precio, o para comprobar el solape antes de insertar— tiene que
    /// preguntar por la forma guardada y no por la que escribió el usuario.
    /// </remarks>
    /// <param name="codigo">Código tal como lo escribieron.</param>
    public static string NormalizarCodigo(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.Trim().ToUpperInvariant();
    }

    private static string CodigoValido(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string normalizado = NormalizarCodigo(codigo);

        return normalizado.Length <= LongitudMaximaDeCodigo
            ? normalizado
            : throw new ArgumentException(
                $"El código de tarifa admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string limpio = nombre.Trim();

        return limpio.Length <= LongitudMaximaDeNombre
            ? limpio
            : throw new ArgumentException(
                $"El nombre de la tarifa admite {LongitudMaximaDeNombre} caracteres como máximo.",
                nameof(nombre));
    }

    private static DateOnly? HastaValido(DateOnly desde, DateOnly? hasta) =>
        hasta is null || hasta >= desde
            ? hasta
            : throw new ArgumentException(
                "Una tarifa no puede dejar de regir antes de empezar a regir.", nameof(hasta));
}
