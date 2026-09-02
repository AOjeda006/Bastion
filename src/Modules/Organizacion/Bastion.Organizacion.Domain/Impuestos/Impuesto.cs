using Bastion.BuildingBlocks.Domain.Entidades;

namespace Bastion.Organizacion.Domain.Impuestos;

/// <summary>
/// Un tipo impositivo con su porcentaje y el tramo de fechas en el que estuvo vigente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un maestro de la instalación, no de una empresa</b>, y eso es la R8 aplicada, no una
/// omisión: «los maestros que se comparten entre sociedades se marcan explícitamente». El tipo
/// general del IVA es el mismo para todas las sociedades que operan en España, porque lo fija el
/// BOE y no el usuario. Que aquí no haya <c>EmpresaId</c> está declarado —con su motivo— en la
/// lista de globales de <c>CadaEntidadDeclaraSuInquilinatoTests</c>, así que no puede pasar por
/// un descuido: el barrido se pone rojo si una entidad no está en ninguno de los dos sitios.
/// </para>
/// <para>
/// <b>Un impuesto no se edita: se sucede.</b> El general del IVA pasó del 18 % al 21 % el 1 de
/// septiembre de 2012, y una factura de agosto de ese año sigue llevando el 18 para siempre. Si
/// el porcentaje se cambiara sobre la fila, toda la facturación anterior quedaría recalculada al
/// tipo nuevo la próxima vez que alguien la reimprimiese — que es una falsedad documental, no un
/// error de redondeo. Por eso el porcentaje y el tramo se fijan al crear y <see cref="Modificar"/>
/// no los toca: un cambio de tipo es <b>cerrar</b> la fila vigente y <b>crear</b> la siguiente.
/// </para>
/// <para>
/// <b>El código NO es único, y esa es la forma correcta.</b> <c>IVA-GENERAL</c> tiene tantas filas
/// como tramos ha tenido. Lo que no puede haber es solape: para un mismo código, dos filas cuyos
/// tramos se pisen harían que «el impuesto vigente el día D» devolviera dos respuestas, y la
/// consulta elegiría una según el orden del plan de ejecución. Eso lo impide la base de datos con
/// una restricción de exclusión, no un caso de uso: una regla que solo vive en el caso de uso deja
/// la puerta abierta a cualquier otro camino de escritura, presente o futuro.
/// </para>
/// <para>
/// El porcentaje es <see cref="decimal"/> por la R6, y no por costumbre: el 21 % de 33,33 € en
/// coma flotante no es el mismo número que en decimal, y esa diferencia acaba en la casilla de un
/// modelo 303.
/// </para>
/// </remarks>
public sealed class Impuesto : EntidadBase
{
    /// <summary>Tope del código: cabe en una columna de un desglose impreso.</summary>
    public const int LongitudMaximaDeCodigo = 20;

    /// <summary>Tope del nombre con el que se muestra.</summary>
    public const int LongitudMaximaDeNombre = 120;

    /// <summary>Dígitos que admite una cuenta del Plan General Contable.</summary>
    /// <remarks>
    /// El PGC numera hasta nueve dígitos, y aquí se admite el rango entero en vez de fijarlo a
    /// nueve: una pyme que trabaje a cuatro dígitos no tiene por qué rellenar con ceros para que
    /// le quepa.
    /// </remarks>
    public const int LongitudMaximaDeCuenta = 9;

    private Impuesto(
        Guid id,
        string codigo,
        string nombre,
        TipoDeImpuesto tipo,
        decimal porcentaje,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        string? cuentaRepercutido,
        string? cuentaSoportado,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        Codigo = codigo;
        Nombre = nombre;
        Tipo = tipo;
        Porcentaje = porcentaje;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
        CuentaRepercutido = cuentaRepercutido;
        CuentaSoportado = cuentaSoportado;
    }

    private Impuesto()
    {
        Codigo = null!;
        Nombre = null!;
    }

    /// <summary>Identificador del impuesto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código del tramo, en mayúsculas. Se repite entre tramos del mismo impuesto.</summary>
    public string Codigo { get; private set; }

    /// <summary>Nombre con el que se muestra.</summary>
    public string Nombre { get; private set; }

    /// <summary>Naturaleza del impuesto.</summary>
    public TipoDeImpuesto Tipo { get; private set; }

    /// <summary>Porcentaje, en tanto por ciento: el 21 % es <c>21</c>, no <c>0,21</c>.</summary>
    /// <remarks>
    /// Se guarda como aparece en el BOE y en la factura, que es como lo lee quien lo comprueba.
    /// Dividir entre cien es cosa del cálculo, y hacerlo en un solo sitio evita la duda de si un
    /// <c>0,21</c> guardado era el tipo o ya la cuota.
    /// </remarks>
    public decimal Porcentaje { get; private set; }

    /// <summary>Primer día en que se aplica. Fecha de negocio: sin hora y sin zona (R14).</summary>
    public DateOnly VigenteDesde { get; private set; }

    /// <summary>Último día en que se aplica, o nulo mientras sigue vigente.</summary>
    public DateOnly? VigenteHasta { get; private set; }

    /// <summary>Cuenta del PGC en la que se repercute; nula hasta que llegue Contabilidad.</summary>
    public string? CuentaRepercutido { get; private set; }

    /// <summary>Cuenta del PGC en la que se soporta; nula hasta que llegue Contabilidad.</summary>
    public string? CuentaSoportado { get; private set; }

    /// <summary>Si el impuesto se aplica el día indicado.</summary>
    /// <param name="dia">Día de devengo de la operación.</param>
    public bool RigeEl(DateOnly dia) =>
        dia >= VigenteDesde && (VigenteHasta is null || dia <= VigenteHasta);

    /// <summary>Crea un tramo de un impuesto.</summary>
    /// <param name="codigo">Código del impuesto; se normaliza a mayúsculas.</param>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="tipo">Naturaleza del impuesto.</param>
    /// <param name="porcentaje">Porcentaje en tanto por ciento.</param>
    /// <param name="vigenteDesde">Primer día en que se aplica.</param>
    /// <param name="vigenteHasta">Último día en que se aplica, o nulo si sigue vigente.</param>
    /// <param name="cuentaRepercutido">Cuenta del PGC en la que se repercute, o nulo.</param>
    /// <param name="cuentaSoportado">Cuenta del PGC en la que se soporta, o nulo.</param>
    /// <param name="momento">Ahora, de quien tenga el <c>TimeProvider</c>.</param>
    public static Impuesto Crear(
        string codigo,
        string nombre,
        TipoDeImpuesto tipo,
        decimal porcentaje,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        string? cuentaRepercutido,
        string? cuentaSoportado,
        DateTimeOffset momento) =>
        new(
            Guid.CreateVersion7(),
            CodigoValido(codigo),
            NombreValido(nombre),
            tipo,
            PorcentajeValido(porcentaje, tipo),
            vigenteDesde,
            HastaValido(vigenteDesde, vigenteHasta),
            CuentaValida(cuentaRepercutido, nameof(cuentaRepercutido)),
            CuentaValida(cuentaSoportado, nameof(cuentaSoportado)),
            momento);

    /// <summary>
    /// Cambia lo que se puede cambiar: el nombre y las dos cuentas contables.
    /// </summary>
    /// <remarks>
    /// <b>Ni el porcentaje, ni el tipo, ni el tramo.</b> Los tres describen lo que ya se aplicó a
    /// las facturas emitidas bajo esta fila; cambiarlos las reescribiría hacia atrás. Lo que sí
    /// cambia sin reescribir nada es cómo se llama y a qué cuenta va, porque ninguna de las dos
    /// cosas entra en el cálculo de una cuota.
    /// </remarks>
    /// <param name="nombre">Nombre con el que se muestra.</param>
    /// <param name="cuentaRepercutido">Cuenta del PGC en la que se repercute, o nulo.</param>
    /// <param name="cuentaSoportado">Cuenta del PGC en la que se soporta, o nulo.</param>
    public void Modificar(string nombre, string? cuentaRepercutido, string? cuentaSoportado)
    {
        Nombre = NombreValido(nombre);
        CuentaRepercutido = CuentaValida(cuentaRepercutido, nameof(cuentaRepercutido));
        CuentaSoportado = CuentaValida(cuentaSoportado, nameof(cuentaSoportado));
    }

    /// <summary>Cierra el tramo el día indicado, que es como se sustituye un tipo por otro.</summary>
    /// <param name="ultimoDia">Último día en que se aplica.</param>
    public void Cerrar(DateOnly ultimoDia)
    {
        if (ultimoDia < VigenteDesde)
        {
            throw new ArgumentException(
                "Un impuesto no puede dejar de regir antes de empezar a regir.", nameof(ultimoDia));
        }

        VigenteHasta = ultimoDia;
    }

    /// <summary>Deja el código en la forma exacta en la que se guarda.</summary>
    /// <remarks>
    /// Pública por lo mismo que la del almacén: quien busca los tramos de un código antes de
    /// insertar tiene que preguntar por la forma guardada, no por la que escribió el usuario.
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
                $"El código de impuesto admite {LongitudMaximaDeCodigo} caracteres como máximo.",
                nameof(codigo));
    }

    private static string NombreValido(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        string limpio = nombre.Trim();

        return limpio.Length <= LongitudMaximaDeNombre
            ? limpio
            : throw new ArgumentException(
                $"El nombre del impuesto admite {LongitudMaximaDeNombre} caracteres como máximo.",
                nameof(nombre));
    }

    // El cero es válido y hay que dejarlo pasar: el tipo 0 % de una entrega intracomunitaria
    // existe, y no es lo mismo que «esta operación no lleva impuesto».
    private static decimal PorcentajeValido(decimal porcentaje, TipoDeImpuesto tipo)
    {
        if (porcentaje < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(porcentaje),
                porcentaje,
                "Un porcentaje impositivo no es negativo. Una retención RESTA por ser retención, " +
                "no por llevar el signo puesto: guardar el 15 % como -15 lo restaría dos veces.");
        }

        return porcentaje <= 100m
            ? porcentaje
            : throw new ArgumentOutOfRangeException(
                nameof(porcentaje), porcentaje, $"Un {tipo} por encima del 100 % no existe.");
    }

    private static DateOnly? HastaValido(DateOnly desde, DateOnly? hasta) =>
        hasta is null || hasta >= desde
            ? hasta
            : throw new ArgumentException(
                "Un impuesto no puede dejar de regir antes de empezar a regir.", nameof(hasta));

    private static string? CuentaValida(string? cuenta, string parametro)
    {
        if (string.IsNullOrWhiteSpace(cuenta))
        {
            return null;
        }

        string limpia = cuenta.Trim();

        if (limpia.Length > LongitudMaximaDeCuenta || !limpia.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                $"Una cuenta del PGC son hasta {LongitudMaximaDeCuenta} dígitos, sin puntos ni letras.",
                parametro);
        }

        return limpia;
    }
}
