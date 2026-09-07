using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Terceros.Domain.Terceros;

/// <summary>
/// Cliente, proveedor, o las dos cosas a la vez: la ficha con la que una empresa conoce a
/// alguien con quien opera.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un solo agregado con roles, y no dos entidades.</b> Lo dice el §7.2 del plan maestro y lo
/// dice la realidad de una pyme: el mismo taller al que se le compra chapa le factura reparaciones,
/// y con dos fichas separadas su NIF estaría dos veces, su dirección se cambiaría en una y no en
/// la otra, y el día que pidiera la supresión de sus datos habría que acordarse de las dos.
/// </para>
/// <para>
/// <b>Es <see cref="IBloqueable"/> por el artículo 32, sin matices.</b> A diferencia del almacén
/// —que se bloquea por una razón contable— aquí el motivo es el que la ley nombra: un tercero
/// puede ser una persona física, y su nombre, su NIF y su domicilio son datos personales. Cuando
/// procede la supresión, se identifican y se reservan; no se borran, porque las facturas que ya se
/// le emitieron tienen que seguir cuadrando (R15).
/// </para>
/// <para>
/// <b>Los datos fiscales que van en una factura se COPIAN en el documento</b> (§7.7), no se
/// referencian a esta ficha. Es lo que permite que bloquear a un tercero y conservar sus facturas
/// intactas no se contradigan. Esta ficha no es el histórico: es con quién se opera hoy.
/// </para>
/// </remarks>
public sealed class Tercero : EntidadBase, IDeInquilino, IBloqueable
{
    /// <summary>
    /// Tope de la razón social. Es el del campo <c>NombreRazon</c> del diseño de registro de la
    /// AEAT: lo que no quepa aquí no cabrá en la factura.
    /// </summary>
    public const int LongitudMaximaDeRazonSocial = 120;

    /// <summary>Tope del nombre comercial. El mismo, porque es la misma clase de dato.</summary>
    public const int LongitudMaximaDeNombreComercial = 120;

    private readonly List<Contacto> _contactos = [];
    private readonly List<CuentaBancaria> _cuentasBancarias = [];
    private readonly List<CondicionPago> _condicionesPago = [];

    private Tercero(
        Guid id,
        Guid empresaId,
        IdentificacionFiscal identificacion,
        string razonSocial,
        string? nombreComercial,
        Direccion domicilioFiscal,
        bool esCliente,
        bool esProveedor,
        RegimenFiscal regimenFiscal,
        DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Identificacion = identificacion;
        RazonSocial = razonSocial;
        NombreComercial = nombreComercial;
        DomicilioFiscal = domicilioFiscal;
        EsCliente = esCliente;
        EsProveedor = esProveedor;
        RegimenFiscal = regimenFiscal;
        Bloqueo = Bloqueo.Ninguno();
    }

    // EF Core necesita poder materializar la entidad desde la base de datos sin pasar por las
    // invariantes: los datos que ya están guardados ya pasaron por ellas.
    private Tercero()
    {
        Identificacion = null!;
        RazonSocial = null!;
        DomicilioFiscal = null!;
        RegimenFiscal = null!;
        Bloqueo = null!;
    }

    /// <summary>Identificador del tercero.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece la ficha (R8).</summary>
    /// <remarks>
    /// Un tercero es de la empresa que lo conoce, no de la instalación: dos empresas que le
    /// compran al mismo proveedor tienen cada una su ficha, con sus condiciones y su histórico. Es
    /// lo que dice el §7.2 al exigir la unicidad por CIF <b>y empresa</b>.
    /// </remarks>
    public Guid EmpresaId { get; private set; }

    /// <summary>Con qué identificador fiscal se le conoce. No cambia nunca.</summary>
    /// <remarks>
    /// Por lo mismo que el NIF de una empresa: el identificador aparece en cada factura ya
    /// emitida. Cambiarlo no es modificar al tercero, es otro tercero.
    /// </remarks>
    public IdentificacionFiscal Identificacion { get; private set; }

    /// <summary>Razón social, o nombre y apellidos si es una persona física.</summary>
    public string RazonSocial { get; private set; }

    /// <summary>Nombre comercial, si opera con uno distinto del fiscal.</summary>
    public string? NombreComercial { get; private set; }

    /// <summary>Domicilio fiscal, en campos estructurados (R17).</summary>
    /// <remarks>
    /// Obligatorio, a diferencia del de un almacén: sin domicilio fiscal no se puede emitir una
    /// factura a nombre de este tercero, y emitir facturas es para lo que existe la ficha.
    /// </remarks>
    public Direccion DomicilioFiscal { get; private set; }

    /// <summary>Se le vende.</summary>
    public bool EsCliente { get; private set; }

    /// <summary>Se le compra.</summary>
    public bool EsProveedor { get; private set; }

    /// <summary>Dónde tributa y bajo qué condiciones especiales.</summary>
    public RegimenFiscal RegimenFiscal { get; private set; }

    /// <summary>
    /// Cuánto se le fía, si se le fía.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es un <see cref="Importe"/>, o sea que LLEVA SU DIVISA, y esa es la decisión.</b> El
    /// enunciado admitía la otra —heredar la de la empresa y dejarlo escrito—, y se ha descartado
    /// por una razón que no es de gusto: <see cref="Importe"/> es el tipo de dinero de este
    /// proyecto y no existe sin divisa; guardar aquí un <c>decimal</c> pelado convertiría el
    /// límite de crédito en la única cantidad de dinero del sistema que no dice de qué es. Y la
    /// herencia implícita tiene un modo de fallo silencioso: el día que alguien cambie la divisa
    /// base de la empresa, todos los límites ya guardados cambiarían de significado sin que
    /// ninguna fila se toque.
    /// </para>
    /// <para>
    /// Lo que sí hereda es <b>lo que se ofrece por omisión</b>: la divisa base de la empresa
    /// (<c>Empresa.DivisaBase</c>, que es un código ISO, no un identificador). Eso es de la
    /// pantalla y del caso de uso, no del modelo.
    /// </para>
    /// <para>
    /// <b>Solo el importe.</b> El riesgo vivo, el disponible y el bloqueo por exceso que nombra el
    /// §7.2 son cálculo sobre documentos que todavía no existen: esa es la raya de la fase 6. Un
    /// «disponible» guardado en esta fila sería un número que nadie recalcula.
    /// </para>
    /// </remarks>
    public Importe? LimiteCredito { get; private set; }

    /// <summary>Las personas con las que se habla en su casa.</summary>
    public IReadOnlyList<Contacto> Contactos => _contactos;

    /// <summary>Sus cuentas, con como mucho una preferente.</summary>
    public IReadOnlyList<CuentaBancaria> CuentasBancarias => _cuentasBancarias;

    /// <summary>Sus condiciones de pago, como mucho una por rol.</summary>
    public IReadOnlyList<CondicionPago> CondicionesPago => _condicionesPago;

    /// <inheritdoc/>
    public Bloqueo Bloqueo { get; private set; }

    /// <summary>Da de alta un tercero activo.</summary>
    /// <remarks>El <c>momento</c> es la fecha de creación, y la pone quien tiene el
    /// <c>TimeProvider</c>: no la base de datos.</remarks>
    public static Tercero Crear(
        Guid empresaId,
        IdentificacionFiscal identificacion,
        string razonSocial,
        string? nombreComercial,
        Direccion domicilioFiscal,
        bool esCliente,
        bool esProveedor,
        RegimenFiscal regimenFiscal,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(identificacion);
        ArgumentNullException.ThrowIfNull(domicilioFiscal);
        ArgumentNullException.ThrowIfNull(regimenFiscal);

        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un tercero pertenece siempre a una empresa (R8).", nameof(empresaId));
        }

        ExigirAlgunRol(esCliente, esProveedor);

        return new Tercero(
            Guid.CreateVersion7(),
            empresaId,
            identificacion,
            Recortado(razonSocial, nameof(razonSocial), LongitudMaximaDeRazonSocial),
            Opcional(nombreComercial, nameof(nombreComercial), LongitudMaximaDeNombreComercial),
            domicilioFiscal,
            esCliente,
            esProveedor,
            regimenFiscal,
            momento);
    }

    /// <summary>Cambia lo que puede cambiar. El identificador fiscal no está entre ello.</summary>
    public void Modificar(
        string razonSocial,
        string? nombreComercial,
        Direccion domicilioFiscal,
        bool esCliente,
        bool esProveedor,
        RegimenFiscal regimenFiscal)
    {
        ArgumentNullException.ThrowIfNull(domicilioFiscal);
        ArgumentNullException.ThrowIfNull(regimenFiscal);

        ExigirQueSePuedaTratar();

        ExigirAlgunRol(esCliente, esProveedor);

        RazonSocial = Recortado(razonSocial, nameof(razonSocial), LongitudMaximaDeRazonSocial);
        NombreComercial = Opcional(
            nombreComercial, nameof(nombreComercial), LongitudMaximaDeNombreComercial);
        DomicilioFiscal = domicilioFiscal;
        EsCliente = esCliente;
        EsProveedor = esProveedor;
        RegimenFiscal = regimenFiscal;
    }

    /// <summary>Cuelga un contacto de la ficha.</summary>
    /// <param name="nombre">Nombre de la persona.</param>
    /// <param name="cargo">Qué hace en casa del tercero.</param>
    /// <param name="correo">Correo profesional.</param>
    /// <param name="telefono">Teléfono profesional.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public Contacto AgregarContacto(
        string nombre,
        string? cargo,
        Correo? correo,
        string? telefono,
        DateTimeOffset momento)
    {
        ExigirQueSePuedaTratar();

        var contacto = Contacto.Crear(Id, nombre, cargo, correo, telefono, momento);
        _contactos.Add(contacto);

        return contacto;
    }

    /// <summary>Quita un contacto.</summary>
    /// <param name="contactoId">Cuál.</param>
    public void QuitarContacto(Guid contactoId)
    {
        ExigirQueSePuedaTratar();
        _contactos.RemoveAll(contacto => contacto.Id == contactoId);
    }

    /// <summary>
    /// Cuelga una cuenta bancaria, y si es la preferente, baja a la que lo fuera.
    /// </summary>
    /// <remarks>
    /// <b>La coordinación vive aquí y no en el caso de uso</b>, porque «como mucho una preferente»
    /// es un invariante <b>del conjunto</b> de cuentas de esta ficha, y el sitio donde un
    /// invariante de conjunto se puede sostener es el agregado que lo contiene. En el caso de uso
    /// habría que acordarse en cada uno de los que tocan cuentas; aquí no hay dónde olvidarse. La
    /// otra mitad —dos peticiones a la vez— la sostiene el índice único, no esto.
    /// </remarks>
    /// <param name="iban">El IBAN, ya validado.</param>
    /// <param name="bic">El BIC, si se conoce.</param>
    /// <param name="alias">Con qué nombre se distingue de las demás.</param>
    /// <param name="esPreferente">Si pasa a ser la de por omisión.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public CuentaBancaria AgregarCuentaBancaria(
        Iban iban,
        string? bic,
        string? alias,
        bool esPreferente,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(iban);
        ExigirQueSePuedaTratar();

        if (_cuentasBancarias.Exists(cuenta =>
            string.Equals(cuenta.Iban.Valor, iban.Valor, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Esta ficha ya tiene esa cuenta. Repetirla dejaría dos filas que el fichero de " +
                "adeudos no sabría distinguir.");
        }

        // Si es la primera, es la preferente aunque nadie lo pida: una ficha con cuentas y sin
        // ninguna preferente obliga a elegir a quien pague, y elegiría por el orden de la base.
        bool preferente = esPreferente || _cuentasBancarias.Count == 0;

        if (preferente)
        {
            foreach (CuentaBancaria anterior in _cuentasBancarias)
            {
                anterior.MarcarPreferente(false);
            }
        }

        var cuentaNueva =
            CuentaBancaria.Crear(Id, iban, bic, alias, preferente, momento);
        _cuentasBancarias.Add(cuentaNueva);

        return cuentaNueva;
    }

    /// <summary>Hace preferente una de las cuentas que ya tiene.</summary>
    /// <param name="cuentaId">Cuál.</param>
    public void MarcarCuentaPreferente(Guid cuentaId)
    {
        ExigirQueSePuedaTratar();

        CuentaBancaria elegida = _cuentasBancarias.Find(cuenta => cuenta.Id == cuentaId)
            ?? throw new InvalidOperationException(
                "Esa cuenta no es de esta ficha, así que no se puede hacer preferente.");

        foreach (CuentaBancaria cuenta in _cuentasBancarias)
        {
            cuenta.MarcarPreferente(cuenta.Id == elegida.Id);
        }
    }

    /// <summary>Quita una cuenta.</summary>
    /// <remarks>
    /// Si la que se va era la preferente y quedan otras, la más antigua toma el relevo: dejar la
    /// ficha con cuentas y sin preferente es el estado que obliga a elegir a quien pague.
    /// </remarks>
    /// <param name="cuentaId">Cuál.</param>
    public void QuitarCuentaBancaria(Guid cuentaId)
    {
        ExigirQueSePuedaTratar();

        CuentaBancaria? cuenta = _cuentasBancarias.Find(fila => fila.Id == cuentaId);

        if (cuenta is null)
        {
            return;
        }

        _cuentasBancarias.Remove(cuenta);

        if (cuenta.EsPreferente && _cuentasBancarias.Count > 0)
        {
            _cuentasBancarias[0].MarcarPreferente(true);
        }
    }

    /// <summary>
    /// Fija la condición de pago de un rol: la crea, o cambia la que ya hubiera.
    /// </summary>
    /// <remarks>
    /// Es una sola operación y no un alta más una modificación porque «como mucho una por rol» es,
    /// otra vez, un invariante del conjunto.
    /// </remarks>
    /// <param name="rol">Si es la condición como cliente o como proveedor.</param>
    /// <param name="diasDePlazo">Días naturales desde la entrega. Como mucho, sesenta.</param>
    /// <param name="diaDePagoFijo">Día del mes en que se paga, de 1 a 28.</param>
    /// <param name="descuentoPorProntoPago">Porcentaje de descuento por pagar antes.</param>
    /// <param name="momento">Cuándo, del reloj inyectado.</param>
    public CondicionPago FijarCondicionPago(
        RolDeCondicionPago rol,
        int diasDePlazo,
        int? diaDePagoFijo,
        decimal? descuentoPorProntoPago,
        DateTimeOffset momento)
    {
        ExigirQueSePuedaTratar();

        CondicionPago? existente = _condicionesPago.Find(condicion => condicion.Rol == rol);

        if (existente is not null)
        {
            existente.Modificar(diasDePlazo, diaDePagoFijo, descuentoPorProntoPago);
            return existente;
        }

        var condicionNueva = CondicionPago.Crear(
            Id, rol, diasDePlazo, diaDePagoFijo, descuentoPorProntoPago, momento);
        _condicionesPago.Add(condicionNueva);

        return condicionNueva;
    }

    /// <summary>Fija —o retira, con <c>null</c>— el límite de crédito.</summary>
    /// <param name="limite">El importe con su divisa, o <c>null</c> para quitarlo.</param>
    public void FijarLimiteDeCredito(Importe? limite)
    {
        ExigirQueSePuedaTratar();

        if (limite is not null && limite.Cantidad < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limite), limite.Cantidad, "Un límite de crédito no puede ser negativo.");
        }

        LimiteCredito = limite;
    }

    /// <inheritdoc/>
    /// <remarks>Sus datos quedan reservados y sus facturas, intactas.</remarks>
    public void Bloquear(MotivoDeBloqueo motivo, DateTimeOffset momento) =>
        Bloqueo = Bloqueo.Bloquear(motivo, momento);

    /// <inheritdoc/>
    public void Desbloquear() => Bloqueo = Bloqueo.Desbloquear();

    /// <summary>
    /// Un tercero es cliente, proveedor, o las dos cosas. Ninguna de las dos, no.
    /// </summary>
    /// <remarks>
    /// No es purismo: una ficha sin rol no sale en ningún selector —ni en el de clientes ni en el
    /// de proveedores—, así que es una fila que ocupa el identificador fiscal, hace chocar el alta
    /// siguiente con un conflicto que nadie entiende, y no se puede usar para nada. Se rechaza al
    /// crearla, que es cuando todavía no le ha pasado eso a nadie.
    /// </remarks>
    /// <summary>
    /// Lo que el art. 32 impide: tratar los datos de una ficha bloqueada.
    /// </summary>
    /// <remarks>
    /// Colgarle un contacto o cambiarle la cuenta es tratarlos igual que cambiarle la razón
    /// social, así que la puerta es la misma para todas las operaciones del agregado.
    /// </remarks>
    private void ExigirQueSePuedaTratar() =>
        Bloqueo.ExigirQueNoEsteBloqueado(
            "Un tercero bloqueado",
            "el art. 32 de la LOPDGDD impide el tratamiento de los datos bloqueados, y " +
            "modificarlos es tratarlos");

    private static void ExigirAlgunRol(bool esCliente, bool esProveedor)
    {
        if (!esCliente && !esProveedor)
        {
            throw new ArgumentException(
                "Un tercero que no es ni cliente ni proveedor no se puede usar para nada: no " +
                "aparece en ningún selector y ocupa su identificador fiscal.",
                nameof(esCliente));
        }
    }

    private static string Recortado(string valor, string campo, int longitudMaxima)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, campo);

        string recortado = valor.Trim();

        return recortado.Length <= longitudMaxima
            ? recortado
            : throw new ArgumentException(
                $"«{campo}» admite {longitudMaxima} caracteres como máximo y trae {recortado.Length}.",
                campo);
    }

    private static string? Opcional(string? valor, string campo, int longitudMaxima) =>
        string.IsNullOrWhiteSpace(valor) ? null : Recortado(valor, campo, longitudMaxima);
}
