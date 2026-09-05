using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Terceros.Domain.Terceros;
using Bastion.Terceros.UnitTests.Identificacion;
using Shouldly;

namespace Bastion.Terceros.UnitTests.Terceros;

/// <summary>
/// Las invariantes del agregado: las que no se pueden romper desde ningún caso de uso porque no
/// hay forma de construirlo roto.
/// </summary>
public sealed class TerceroTests
{
    private static readonly DateTimeOffset s_momento =
        new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    private static readonly Direccion s_domicilio = Direccion.De(
        calle: "Calle de la Prueba",
        numero: "1",
        codigoPostal: "28001",
        poblacion: "Madrid",
        subdivision: "Madrid",
        pais: "ES");

    [Fact]
    public void Un_tercero_nace_activo_y_con_su_identificacion()
    {
        Tercero tercero = Alta();

        tercero.Id.ShouldNotBe(Guid.Empty);
        tercero.Bloqueo.EstaBloqueado.ShouldBeFalse();
        tercero.Identificacion.Verificacion.ShouldBe(EstadoDeVerificacion.VerificadoPorAlgoritmo);
        tercero.CreadoEn.ShouldBe(s_momento);
    }

    /// <summary>
    /// Cliente, proveedor, o las dos cosas. Ninguna de las dos, no.
    /// </summary>
    /// <remarks>
    /// Una ficha sin rol no sale en ningún selector, no se puede usar para nada y ocupa el
    /// identificador fiscal — con lo que hace chocar el alta siguiente con un conflicto que nadie
    /// entiende. Se rechaza al crearla, que es cuando todavía no le ha pasado eso a nadie.
    /// </remarks>
    [Fact]
    public void Un_tercero_que_no_es_ni_cliente_ni_proveedor_no_se_puede_crear()
    {
        Should.Throw<ArgumentException>(() => Alta(esCliente: false, esProveedor: false));
    }

    [Fact]
    public void Un_tercero_que_se_queda_sin_rol_al_modificarlo_tampoco_vale()
    {
        Tercero tercero = Alta();

        Should.Throw<ArgumentException>(() => tercero.Modificar(
            "Otra Razón",
            nombreComercial: null,
            s_domicilio,
            esCliente: false,
            esProveedor: false));
    }

    [Fact]
    public void Un_tercero_sin_empresa_no_existe()
    {
        Should.Throw<ArgumentException>(() => Tercero.Crear(
            Guid.Empty,
            IdentificacionFiscal.Espanola(NifInventado()),
            "Razón Social",
            nombreComercial: null,
            s_domicilio,
            esCliente: true,
            esProveedor: false,
            s_momento));
    }

    /// <summary>
    /// Modificar un tercero bloqueado es tratar sus datos, y eso es lo que el art. 32 impide.
    /// </summary>
    /// <remarks>
    /// La invariante está en el agregado y no solo en el caso de uso a propósito: el caso de uso
    /// no llega a leerlo —el filtro de R16 no se lo da— pero un camino futuro que abriera el
    /// ámbito para otra cosa sí podría tenerlo en la mano. La regla vive donde no se puede
    /// esquivar.
    /// </remarks>
    [Fact]
    public void Un_tercero_bloqueado_no_se_modifica()
    {
        Tercero tercero = Alta();
        tercero.Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        Should.Throw<InvalidOperationException>(() => tercero.Modificar(
            "Otra Razón",
            nombreComercial: null,
            s_domicilio,
            esCliente: true,
            esProveedor: false));
    }

    [Fact]
    public void Desbloquear_devuelve_el_tercero_a_la_operativa()
    {
        Tercero tercero = Alta();
        tercero.Bloquear(MotivoDeBloqueo.SupresionSolicitada, s_momento);

        tercero.Desbloquear();

        tercero.Bloqueo.EstaBloqueado.ShouldBeFalse();
        tercero.Modificar("Otra Razón", null, s_domicilio, esCliente: true, esProveedor: true);
        tercero.EsProveedor.ShouldBeTrue();
    }

    /// <summary>
    /// El identificador fiscal no se puede cambiar, y la prueba es que <c>Modificar</c> no lo
    /// admite.
    /// </summary>
    /// <remarks>
    /// Aparece en cada factura ya emitida a ese tercero: cambiarlo no es modificar al tercero, es
    /// otro tercero. Como no está en la firma, no hay manera de intentarlo — y por eso lo que este
    /// test comprueba es que sigue siendo el mismo después de una modificación completa.
    /// </remarks>
    [Fact]
    public void Modificar_no_toca_el_identificador_fiscal()
    {
        Tercero tercero = Alta();
        IdentificacionFiscal antes = tercero.Identificacion;

        tercero.Modificar("Otra Razón", "Otro Nombre", s_domicilio, true, true);

        tercero.Identificacion.ShouldBe(antes);
    }

    [Fact]
    public void La_razon_social_se_recorta_y_no_admite_mas_de_lo_que_cabe_en_una_factura()
    {
        Tercero tercero = Alta(razonSocial: "  Con Espacios  ");
        tercero.RazonSocial.ShouldBe("Con Espacios");

        Should.Throw<ArgumentException>(
            () => Alta(razonSocial: new string('A', Tercero.LongitudMaximaDeRazonSocial + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void El_nombre_comercial_en_blanco_se_guarda_como_ausente(string? escrito)
    {
        // Y no como cadena vacía: «no tiene nombre comercial» y «tiene uno que es la cadena
        // vacía» se enseñarían distinto en una pantalla y son el mismo hecho.
        Alta(nombreComercial: escrito).NombreComercial.ShouldBeNull();
    }

    private static Nif NifInventado() =>
        Nif.De(IdentificadoresInventados.PersonaJuridica('B', 1_234_567, comoLetra: false).Valido);

    private static Tercero Alta(
        string razonSocial = "Razón Social",
        string? nombreComercial = null,
        bool esCliente = true,
        bool esProveedor = false) =>
        Tercero.Crear(
            Guid.CreateVersion7(),
            IdentificacionFiscal.Espanola(NifInventado()),
            razonSocial,
            nombreComercial,
            s_domicilio,
            esCliente,
            esProveedor,
            s_momento);
}
