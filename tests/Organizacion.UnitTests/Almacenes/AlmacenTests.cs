using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.Organizacion.Domain.Almacenes;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Almacenes;

public sealed class AlmacenTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");

    private static Direccion Ubicacion() => Direccion.De(
        "Polígono Las Fuentes", "12", "50002", "Zaragoza", "Zaragoza", "ES");

    private static Almacen Nuevo(string codigo = "CENTRAL") => Almacen.Crear(
        s_empresa, codigo, "Almacén central", Ubicacion(), TipoDeAlmacen.Fisico);

    [Fact]
    public void Un_almacen_nace_activo_y_con_su_empresa()
    {
        Almacen almacen = Nuevo();

        almacen.Estado.ShouldBe(EstadoDeAlmacen.Activo);
        almacen.EmpresaId.ShouldBe(s_empresa);
        almacen.BloqueadoEn.ShouldBeNull();
    }

    [Fact]
    public void Un_almacen_sin_empresa_no_existe()
    {
        Should.Throw<ArgumentException>(() => Almacen.Crear(
            Guid.Empty, "CENTRAL", "Central", Ubicacion(), TipoDeAlmacen.Fisico));
    }

    [Fact]
    public void El_codigo_se_normaliza_a_mayusculas()
    {
        Nuevo(" central ").Codigo.ShouldBe("CENTRAL");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_nombre_es_obligatorio(string nombre)
    {
        Should.Throw<ArgumentException>(() => Almacen.Crear(
            s_empresa, "CENTRAL", nombre, Ubicacion(), TipoDeAlmacen.Fisico));
    }

    [Fact]
    public void Un_almacen_virtual_puede_no_tener_direccion()
    {
        // Un almacén de regularizaciones o de tránsito no está en ningún sitio. Exigirle una
        // dirección obligaría a inventarse una, que es peor que no tenerla.
        var virtual_ = Almacen.Crear(
            s_empresa, "REGUL", "Regularizaciones", direccion: null, TipoDeAlmacen.Virtual);

        virtual_.Direccion.ShouldBeNull();
    }

    [Fact]
    public void Un_almacen_fisico_sin_direccion_no_se_acepta()
    {
        Should.Throw<ArgumentException>(() => Almacen.Crear(
            s_empresa, "CENTRAL", "Central", direccion: null, TipoDeAlmacen.Fisico));
    }

    [Fact]
    public void Bloquear_un_almacen_lo_saca_de_circulacion_sin_borrar_su_historico()
    {
        // Aquí el motivo NO es el art. 32 —un almacén no es una persona—, pero la forma es la
        // misma y a propósito: cada movimiento de existencias apunta a su almacén para siempre,
        // así que borrarlo rompería el histórico de valoración, que es irreparable. Un tercer
        // estado, distinto de activo y de borrado, cubre los dos motivos con una sola columna,
        // y por eso el 0.10 podrá formalizar el tipo base sin migrar nada.
        Almacen almacen = Nuevo();
        DateTimeOffset momento = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

        almacen.Bloquear(momento);

        almacen.Estado.ShouldBe(EstadoDeAlmacen.Bloqueado);
        almacen.BloqueadoEn.ShouldBe(momento);
    }

    [Fact]
    public void Un_almacen_bloqueado_no_admite_modificaciones()
    {
        Almacen almacen = Nuevo();
        almacen.Bloquear(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            almacen.Modificar("Otro nombre", Ubicacion(), TipoDeAlmacen.Fisico));
    }

    [Fact]
    public void Desbloquear_devuelve_el_almacen_a_activo()
    {
        Almacen almacen = Nuevo();
        almacen.Bloquear(DateTimeOffset.UtcNow);

        almacen.Desbloquear();

        almacen.Estado.ShouldBe(EstadoDeAlmacen.Activo);
        almacen.BloqueadoEn.ShouldBeNull();
    }

    [Fact]
    public void Modificar_cambia_nombre_direccion_y_tipo_pero_no_el_codigo()
    {
        // El código del almacén aparece en albaranes y en etiquetas ya impresas: cambiarlo
        // rompería la correspondencia con el papel que ya está fuera.
        Almacen almacen = Nuevo();
        var otra = Direccion.De("Calle Mayor", "1", "50001", "Zaragoza", "Zaragoza", "ES");

        almacen.Modificar("Almacén norte", otra, TipoDeAlmacen.Transito);

        almacen.Nombre.ShouldBe("Almacén norte");
        almacen.Direccion.ShouldBe(otra);
        almacen.Tipo.ShouldBe(TipoDeAlmacen.Transito);
        almacen.Codigo.ShouldBe("CENTRAL");
    }
}
