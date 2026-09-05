using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Entidades;
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

    private Tercero(
        Guid id,
        Guid empresaId,
        IdentificacionFiscal identificacion,
        string razonSocial,
        string? nombreComercial,
        Direccion domicilioFiscal,
        bool esCliente,
        bool esProveedor,
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
        Bloqueo = Bloqueo.Ninguno();
    }

    // EF Core necesita poder materializar la entidad desde la base de datos sin pasar por las
    // invariantes: los datos que ya están guardados ya pasaron por ellas.
    private Tercero()
    {
        Identificacion = null!;
        RazonSocial = null!;
        DomicilioFiscal = null!;
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
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(identificacion);
        ArgumentNullException.ThrowIfNull(domicilioFiscal);

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
            momento);
    }

    /// <summary>Cambia lo que puede cambiar. El identificador fiscal no está entre ello.</summary>
    public void Modificar(
        string razonSocial,
        string? nombreComercial,
        Direccion domicilioFiscal,
        bool esCliente,
        bool esProveedor)
    {
        ArgumentNullException.ThrowIfNull(domicilioFiscal);

        Bloqueo.ExigirQueNoEsteBloqueado(
            "Un tercero bloqueado",
            "el art. 32 de la LOPDGDD impide el tratamiento de los datos bloqueados, y " +
            "modificarlos es tratarlos");

        ExigirAlgunRol(esCliente, esProveedor);

        RazonSocial = Recortado(razonSocial, nameof(razonSocial), LongitudMaximaDeRazonSocial);
        NombreComercial = Opcional(
            nombreComercial, nameof(nombreComercial), LongitudMaximaDeNombreComercial);
        DomicilioFiscal = domicilioFiscal;
        EsCliente = esCliente;
        EsProveedor = esProveedor;
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
