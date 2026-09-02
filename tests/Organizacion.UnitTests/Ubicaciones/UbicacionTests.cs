using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.Organizacion.Domain.Ubicaciones;
using Shouldly;

namespace Bastion.Organizacion.UnitTests.Ubicaciones;

public sealed class UbicacionTests
{
    private static readonly Guid s_empresa = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000001");
    private static readonly Guid s_almacen = Guid.Parse("2f6d5f4e-0000-4000-8000-000000000002");
    private static readonly DateTimeOffset s_momento = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private static Ubicacion Nueva(string codigo = "A-01-3") => Ubicacion.Crear(
        s_empresa, s_almacen, codigo, "A", "01", "3", "Junto al muelle", s_momento);

    [Fact]
    public void Una_ubicacion_nace_activa_con_su_empresa_y_su_almacen()
    {
        Ubicacion ubicacion = Nueva();

        ubicacion.EmpresaId.ShouldBe(s_empresa);
        ubicacion.AlmacenId.ShouldBe(s_almacen);
        ubicacion.Bloqueo.EstaBloqueado.ShouldBeFalse();
        ubicacion.CreadoEn.ShouldBe(s_momento);
        ubicacion.ModificadoEn.ShouldBe(s_momento);
    }

    [Fact]
    public void Lleva_empresa_propia_aunque_su_almacen_ya_tenga_una()
    {
        // No es redundancia: el filtro global de R8 se evalúa sobre las columnas de la fila. Sin
        // esta, filtrar una ubicación exigiría una subconsulta contra su almacén en cada lectura,
        // y bastaría un listado nuevo que empezara por Ubicaciones para enseñar las de otra
        // empresa.
        Nueva().EmpresaId.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(" a-01-3 ", "A-01-3")]
    [InlineData("muelle", "MUELLE")]
    public void El_codigo_se_normaliza_a_mayusculas(string escrito, string guardado)
    {
        Nueva(escrito).Codigo.ShouldBe(guardado);
        Ubicacion.NormalizarCodigo(escrito).ShouldBe(guardado);
    }

    [Fact]
    public void Las_tres_coordenadas_son_opcionales()
    {
        // Hay almacenes que no se dividen. Obligarles a inventarse un pasillo llenaría la tabla
        // de filas que no dicen nada.
        var ubicacion = Ubicacion.Crear(
            s_empresa, s_almacen, "UNICA", null, null, null, null, s_momento);

        ubicacion.Pasillo.ShouldBeNull();
        ubicacion.Estante.ShouldBeNull();
        ubicacion.Hueco.ShouldBeNull();
        ubicacion.Descripcion.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_coordenada_en_blanco_se_guarda_como_nulo_y_no_como_cadena_vacia(string blanco)
    {
        // Dos formas de decir «este almacén no tiene pasillos» obligarían a preguntar por las dos
        // en cada consulta, para siempre.
        var ubicacion = Ubicacion.Crear(
            s_empresa, s_almacen, "UNICA", blanco, blanco, blanco, blanco, s_momento);

        ubicacion.Pasillo.ShouldBeNull();
        ubicacion.Estante.ShouldBeNull();
        ubicacion.Hueco.ShouldBeNull();
        ubicacion.Descripcion.ShouldBeNull();
    }

    [Fact]
    public void Modificar_cambia_las_coordenadas_pero_no_el_codigo_ni_el_almacen()
    {
        Ubicacion ubicacion = Nueva();

        ubicacion.Modificar("B", "02", "1", null);

        ubicacion.Pasillo.ShouldBe("B");
        ubicacion.Estante.ShouldBe("02");
        ubicacion.Hueco.ShouldBe("1");
        ubicacion.Descripcion.ShouldBeNull();
        ubicacion.Codigo.ShouldBe("A-01-3");
        ubicacion.AlmacenId.ShouldBe(s_almacen);
    }

    [Fact]
    public void Una_ubicacion_bloqueada_no_se_modifica()
    {
        Ubicacion ubicacion = Nueva();

        ubicacion.Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento);

        Should.Throw<InvalidOperationException>(() => ubicacion.Modificar("B", null, null, null));
    }

    [Fact]
    public void Bloquear_conserva_la_ficha_y_desbloquear_la_devuelve_a_la_operativa()
    {
        // R16: suprimir no es borrar. En cuanto llegue Inventario, cada movimiento apuntará aquí
        // para siempre, y borrar la fila rompería el histórico de valoración.
        Ubicacion ubicacion = Nueva();

        ubicacion.Bloquear(MotivoDeBloqueo.CeseDeUso, s_momento);

        ubicacion.Bloqueo.EstaBloqueado.ShouldBeTrue();
        ubicacion.Bloqueo.Desde.ShouldBe(s_momento);
        ubicacion.Codigo.ShouldBe("A-01-3");
        ubicacion.Pasillo.ShouldBe("A");

        ubicacion.Desbloquear();

        ubicacion.Bloqueo.EstaBloqueado.ShouldBeFalse();
        ubicacion.Bloqueo.Desde.ShouldBeNull();
        ubicacion.Bloqueo.Motivo.ShouldBeNull();
    }

    [Fact]
    public void Una_ubicacion_sin_empresa_o_sin_almacen_no_existe()
    {
        Should.Throw<ArgumentException>(() => Ubicacion.Crear(
            Guid.Empty, s_almacen, "A-01-3", null, null, null, null, s_momento));

        Should.Throw<ArgumentException>(() => Ubicacion.Crear(
            s_empresa, Guid.Empty, "A-01-3", null, null, null, null, s_momento));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_codigo_es_obligatorio(string vacio)
    {
        Should.Throw<ArgumentException>(() => Nueva(vacio));
    }
}
