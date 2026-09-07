using System.ComponentModel.DataAnnotations;

namespace Bastion.Terceros.Contracts.Terceros;

/// <summary>El régimen fiscal de un tercero, tal como viaja por la API.</summary>
/// <remarks>
/// <para>
/// Los enumerados viajan como <b>texto</b>, igual que el estado de verificación: un entero en el
/// contrato deja de significar lo mismo en cuanto alguien reordena el enumerado, y quien lo lee es
/// un cliente ya desplegado.
/// </para>
/// <para>
/// <b>Sin el tipo de retención.</b> Solo si procede. El porcentaje es un <c>Impuesto</c> de
/// Organización con su vigencia; ver la nota de <c>RegimenFiscal</c> en el dominio.
/// </para>
/// </remarks>
/// <param name="Territorio">
/// Dónde tributa: <c>PeninsulaYBaleares</c>, <c>Canarias</c>, <c>CeutaYMelilla</c>,
/// <c>UnionEuropea</c> o <c>TercerosPaises</c>.
/// </param>
/// <param name="RecargoDeEquivalencia">Si es minorista en recargo de equivalencia.</param>
/// <param name="CriterioDeCaja">Si está acogido al criterio de caja.</param>
/// <param name="SujetoARetencionIrpf">Si sus facturas llevan retención de IRPF.</param>
public sealed record RegimenFiscalDto(
    string Territorio,
    bool RecargoDeEquivalencia,
    bool CriterioDeCaja,
    bool SujetoARetencionIrpf);

/// <summary>Con qué régimen fiscal se da de alta o se modifica un tercero.</summary>
public sealed record RegimenFiscalDeAltaDto
{
    /// <summary>
    /// Dónde tributa. Uno de los cinco del §7.2; si no se dice, península y Baleares.
    /// </summary>
    /// <remarks>
    /// Tiene valor por omisión porque es el de la inmensa mayoría de las fichas, y obligar a
    /// nombrarlo en cada alta hace que se copie sin mirarlo. Lo que no tiene es la opción de
    /// dejarlo sin decidir: un tercero tributa en algún sitio.
    /// </remarks>
    [StringLength(20, ErrorMessage = "El territorio fiscal no puede pasar de {1} caracteres.")]
    // El literal y no un `nameof`: `Contracts` no ve el dominio, que es justo la frontera
    // que lo hace publicable. Que este texto siga siendo un valor del enumerado lo
    // comprueba el barrido de contrato, no el compilador.
    public string Territorio { get; init; } = "PeninsulaYBaleares";

    /// <summary>Si es minorista en recargo de equivalencia.</summary>
    public bool RecargoDeEquivalencia { get; init; }

    /// <summary>Si está acogido al criterio de caja.</summary>
    public bool CriterioDeCaja { get; init; }

    /// <summary>Si sus facturas llevan retención de IRPF.</summary>
    public bool SujetoARetencionIrpf { get; init; }
}

/// <summary>Un contacto del tercero, tal como sale de la API.</summary>
/// <param name="Id">Identificador del contacto.</param>
/// <param name="TerceroId">La ficha de la que cuelga.</param>
/// <param name="Nombre">Nombre de la persona.</param>
/// <param name="Cargo">Qué hace en casa del tercero.</param>
/// <param name="Correo">Correo profesional.</param>
/// <param name="Telefono">Teléfono profesional.</param>
public sealed record ContactoDto(
    Guid Id,
    Guid TerceroId,
    string Nombre,
    string? Cargo,
    string? Correo,
    string? Telefono);

/// <summary>Lo que hace falta para colgar o cambiar un contacto.</summary>
/// <remarks>
/// <b>No hay campo de notas</b>, y su ausencia es la decisión: ver <c>Contacto</c> en el dominio.
/// </remarks>
public sealed record ContactoDeAltaDto
{
    /// <summary>Nombre de la persona.</summary>
    [Required(ErrorMessage = "El nombre del contacto es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de {1} caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Qué hace en casa del tercero.</summary>
    [StringLength(60, ErrorMessage = "El cargo no puede pasar de {1} caracteres.")]
    public string? Cargo { get; init; }

    /// <summary>Correo profesional.</summary>
    [StringLength(254, ErrorMessage = "El correo no puede pasar de {1} caracteres.")]
    public string? Correo { get; init; }

    /// <summary>Teléfono profesional.</summary>
    [StringLength(20, ErrorMessage = "El teléfono no puede pasar de {1} caracteres.")]
    public string? Telefono { get; init; }
}

/// <summary>Una cuenta bancaria del tercero, tal como sale de la API.</summary>
/// <remarks>
/// <b>El IBAN sale entero</b>, no enmascarado: quien tiene el permiso de terceros de esta empresa
/// es quien va a pagar por esa cuenta, y una pantalla que enseñe <c>ES99****1234</c> no sirve para
/// comprobar que el número es el que puso el proveedor en su factura. Lo que no lo enseña es el
/// <b>registro</b>, que es donde se queda para siempre: de eso se encarga el <c>ToString</c> del
/// objeto de valor.
/// </remarks>
/// <param name="Id">Identificador de la cuenta.</param>
/// <param name="TerceroId">La ficha de la que cuelga.</param>
/// <param name="Iban">El IBAN, normalizado y sin espacios.</param>
/// <param name="Bic">El BIC, si se conoce.</param>
/// <param name="Alias">Con qué nombre se distingue de las demás.</param>
/// <param name="EsPreferente">Si es la que se usa cuando nadie dice otra cosa.</param>
public sealed record CuentaBancariaDto(
    Guid Id,
    Guid TerceroId,
    string Iban,
    string? Bic,
    string? Alias,
    bool EsPreferente);

/// <summary>Lo que hace falta para colgar una cuenta bancaria.</summary>
public sealed record CuentaBancariaDeAltaDto
{
    /// <summary>El IBAN. Admite los espacios con los que se imprime.</summary>
    [Required(ErrorMessage = "El IBAN es obligatorio.")]
    [StringLength(42, ErrorMessage = "El IBAN no puede pasar de {1} caracteres.")]
    public string Iban { get; init; } = string.Empty;

    /// <summary>El BIC, si se conoce. Ocho u once posiciones.</summary>
    [StringLength(11, MinimumLength = 8, ErrorMessage = "Un BIC tiene 8 u 11 posiciones.")]
    public string? Bic { get; init; }

    /// <summary>Con qué nombre se distingue de las demás.</summary>
    [StringLength(60, ErrorMessage = "El alias no puede pasar de {1} caracteres.")]
    public string? Alias { get; init; }

    /// <summary>Si pasa a ser la de por omisión.</summary>
    public bool EsPreferente { get; init; }
}

/// <summary>Una condición de pago del tercero, tal como sale de la API.</summary>
/// <param name="Id">Identificador de la condición.</param>
/// <param name="TerceroId">La ficha de la que cuelga.</param>
/// <param name="Rol">De qué cara es: <c>Cliente</c> o <c>Proveedor</c>.</param>
/// <param name="DiasDePlazo">Días naturales <b>desde la entrega</b>. Como mucho, sesenta.</param>
/// <param name="DiaDePagoFijo">Día del mes en que se paga, si lo hay.</param>
/// <param name="DescuentoPorProntoPago">Porcentaje de descuento por pagar antes, si lo hay.</param>
public sealed record CondicionPagoDto(
    Guid Id,
    Guid TerceroId,
    string Rol,
    int DiasDePlazo,
    int? DiaDePagoFijo,
    decimal? DescuentoPorProntoPago);

/// <summary>Lo que hace falta para fijar la condición de pago de un rol.</summary>
/// <remarks>
/// <b>El rol no está en el cuerpo: está en la ruta</b>, porque la condición de un rol es un
/// recurso —hay una, o no hay ninguna—, y ponerlo en el cuerpo dejaría que un <c>PUT</c> sobre la
/// del cliente cambiara la del proveedor.
/// </remarks>
public sealed record CondicionPagoDeAltaDto
{
    /// <summary>
    /// Días naturales desde la entrega o la prestación.
    /// </summary>
    /// <remarks>
    /// El tope de sesenta está aquí <b>y</b> en el dominio, y eso no es duplicar por duplicar: el
    /// del borde existe para que el usuario lea por qué se le rechaza en vez de recibir un 500, y
    /// el del dominio para que la regla siga puesta cuando la llamada venga de una importación de
    /// CSV o de una migración. El que manda es el del dominio.
    /// </remarks>
    [Range(0, 60, ErrorMessage =
        "El plazo máximo de pago son {2} días naturales desde la entrega (Ley 3/2004), y no es " +
        "ampliable por acuerdo entre las partes.")]
    public int DiasDePlazo { get; init; }

    /// <summary>Día del mes en que se paga, del 1 al 28.</summary>
    [Range(1, 28, ErrorMessage = "El día de pago fijo va del {1} al {2}.")]
    public int? DiaDePagoFijo { get; init; }

    /// <summary>Porcentaje de descuento por pagar antes.</summary>
    [Range(0, 100, ErrorMessage = "El descuento por pronto pago va del {1} al {2} por ciento.")]
    public decimal? DescuentoPorProntoPago { get; init; }
}

/// <summary>El límite de crédito de un tercero, tal como sale de la API.</summary>
/// <param name="TerceroId">La ficha de la que cuelga.</param>
/// <param name="Cantidad">El importe. Nulo si no se le fía.</param>
/// <param name="Divisa">La divisa del importe, en ISO 4217. Nula si no se le fía.</param>
public sealed record LimiteCreditoDto(Guid TerceroId, decimal? Cantidad, string? Divisa);

/// <summary>Lo que hace falta para fijar —o retirar— el límite de crédito.</summary>
/// <remarks>
/// <b>La divisa es obligatoria si hay cantidad</b>, y no se hereda en silencio de la empresa: un
/// importe que no dice de qué es sería la única cantidad de dinero del sistema que no lo dice. Lo
/// que la pantalla ofrece por omisión es la divisa base de la empresa, y eso es de la pantalla.
/// </remarks>
public sealed record LimiteCreditoDeAltaDto
{
    /// <summary>El importe. Nulo retira el límite.</summary>
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Un límite de crédito no puede ser negativo.")]
    public decimal? Cantidad { get; init; }

    /// <summary>La divisa, en ISO 4217. Obligatoria si hay cantidad.</summary>
    [StringLength(3, MinimumLength = 3, ErrorMessage = "La divisa son tres letras (ISO 4217).")]
    public string? Divisa { get; init; }
}
