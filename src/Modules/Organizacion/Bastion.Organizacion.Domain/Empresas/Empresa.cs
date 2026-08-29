using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Identificacion;

namespace Bastion.Organizacion.Domain.Empresas;

/// <summary>
/// Sociedad o empresario individual que opera en Bastion. Es la raíz del multiempresa (R8):
/// toda entidad transaccional del sistema lleva su identificador.
/// </summary>
/// <remarks>
/// <para>
/// La empresa NO lleva <c>empresa_id</c>: es el tenant, no un inquilino de otro.
/// </para>
/// <para>
/// Es la primera <see cref="RaizAgregado"/> del sistema: de ella salen eventos de integración
/// (R12). Heredarlo no le añade estado persistido ninguno —la colección de eventos vive en
/// memoria y el modelo la ignora—; lo que declara es que lo que le pasa se cuenta.
/// </para>
/// <para>
/// Sí lleva estado <see cref="EstadoDeEmpresa.Bloqueada"/>, y la razón está escrita en el
/// ADR-0007: un <b>empresario individual</b> es persona física, así que la razón social puede
/// ser un nombre propio y el domicilio fiscal, un domicilio particular. El artículo 32 de la
/// LOPDGDD alcanza entonces a esta ficha igual que a la de un tercero.
/// </para>
/// </remarks>
public sealed class Empresa : RaizAgregado
{
    private Empresa(
        Guid id,
        Nif nif,
        string razonSocial,
        Direccion domicilioFiscal,
        string divisaBase,
        RegimenDeIva regimenDeIva)
    {
        Id = id;
        Nif = nif;
        RazonSocial = razonSocial;
        DomicilioFiscal = domicilioFiscal;
        DivisaBase = divisaBase;
        RegimenDeIva = regimenDeIva;
        Estado = EstadoDeEmpresa.Activa;
    }

    // EF Core necesita poder materializar la entidad desde la base de datos sin pasar por las
    // invariantes: los datos que ya están guardados ya pasaron por ellas.
    private Empresa()
    {
        Nif = null!;
        RazonSocial = null!;
        DomicilioFiscal = null!;
        DivisaBase = null!;
    }

    /// <summary>Identificador de la empresa.</summary>
    public Guid Id { get; private set; }

    /// <summary>NIF, con su carácter de control comprobado. No cambia nunca.</summary>
    public Nif Nif { get; private set; }

    /// <summary>Razón social o nombre del empresario individual.</summary>
    public string RazonSocial { get; private set; }

    /// <summary>Domicilio fiscal, en campos estructurados (R17).</summary>
    public Direccion DomicilioFiscal { get; private set; }

    /// <summary>Divisa base de la contabilidad, en ISO 4217.</summary>
    public string DivisaBase { get; private set; }

    /// <summary>Régimen de IVA en el que tributa.</summary>
    public RegimenDeIva RegimenDeIva { get; private set; }

    /// <summary>Estado de la ficha: activa o bloqueada (R16).</summary>
    public EstadoDeEmpresa Estado { get; private set; }

    /// <summary>
    /// Instante del bloqueo. Es un momento del tiempo, no una fecha de negocio: de él arranca
    /// el plazo de prescripción del art. 32, así que se guarda con zona horaria.
    /// </summary>
    public DateTimeOffset? BloqueadaEn { get; private set; }

    /// <summary>Crea una empresa activa.</summary>
    public static Empresa Crear(
        Nif nif,
        string razonSocial,
        Direccion domicilioFiscal,
        string divisaBase,
        RegimenDeIva regimenDeIva)
    {
        ArgumentNullException.ThrowIfNull(nif);
        ArgumentNullException.ThrowIfNull(domicilioFiscal);

        return new Empresa(
            Guid.CreateVersion7(),
            nif,
            RazonSocialValida(razonSocial),
            domicilioFiscal,
            DivisaBaseValida(divisaBase),
            regimenDeIva);
    }

    /// <summary>Cambia lo que puede cambiar. El NIF no está entre ello.</summary>
    public void Modificar(
        string razonSocial,
        Direccion domicilioFiscal,
        string divisaBase,
        RegimenDeIva regimenDeIva)
    {
        ArgumentNullException.ThrowIfNull(domicilioFiscal);
        ExigirQueNoEsteBloqueada();

        RazonSocial = RazonSocialValida(razonSocial);
        DomicilioFiscal = domicilioFiscal;
        DivisaBase = DivisaBaseValida(divisaBase);
        RegimenDeIva = regimenDeIva;
    }

    /// <summary>Bloquea la ficha (R16). Suprimir no es borrar.</summary>
    /// <remarks>
    /// Es idempotente y NO mueve la fecha del primer bloqueo: de esa fecha cuelga el plazo de
    /// prescripción, y re-bloquear alargaría la conservación sin que nadie lo hubiera decidido.
    /// </remarks>
    public void Bloquear(DateTimeOffset momento)
    {
        if (Estado == EstadoDeEmpresa.Bloqueada)
        {
            return;
        }

        Estado = EstadoDeEmpresa.Bloqueada;
        BloqueadaEn = momento;
    }

    /// <summary>Levanta el bloqueo.</summary>
    public void Desbloquear()
    {
        Estado = EstadoDeEmpresa.Activa;
        BloqueadaEn = null;
    }

    private void ExigirQueNoEsteBloqueada()
    {
        if (Estado == EstadoDeEmpresa.Bloqueada)
        {
            throw new InvalidOperationException(
                "Una empresa bloqueada no admite cambios: el art. 32 de la LOPDGDD impide el " +
                "tratamiento de los datos bloqueados, y modificarlos es tratarlos.");
        }
    }

    private static string RazonSocialValida(string razonSocial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razonSocial);
        return razonSocial.Trim();
    }

    private static string DivisaBaseValida(string divisaBase)
    {
        string normalizada = Divisas.Normalizar(divisaBase);

        // Comprobar la unidad mínima AHORA y no al emitir la primera factura: una empresa cuya
        // divisa no sabemos redondear no puede calcular una cuota (R6), y descubrirlo con el
        // libro registro a medias es tarde.
        _ = Divisas.UnidadMinima(normalizada);

        return normalizada;
    }
}
