using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Identidad.Domain.Roles;
using Shouldly;

namespace Bastion.Identidad.UnitTests.Roles;

public sealed class RolTests
{
    private static readonly Permiso s_crear = Permiso.De("organizacion.almacen.crear");
    private static readonly Permiso s_modificar = Permiso.De("organizacion.almacen.modificar");

    [Fact]
    public void Crear_NaceSinPermisosYSinSerDelSistema()
    {
        var rol = Rol.Crear("contable", "Contable");

        rol.Codigo.ShouldBe("contable");
        rol.Nombre.ShouldBe("Contable");
        rol.EsDelSistema.ShouldBeFalse();
        rol.Permisos.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("  Contable  ", "contable")]
    [InlineData("JEFE-DE-ALMACEN", "jefe-de-almacen")]
    public void Crear_NormalizaElCodigo(string entrada, string esperado) =>
        Rol.Crear(entrada, "Un rol").Codigo.ShouldBe(esperado);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("con table")]
    [InlineData("contable_jefe")]
    [InlineData("-contable")]
    [InlineData("contable-")]
    [InlineData("contable.jefe")]
    public void Crear_ConUnCodigoQueNoEsRanuraEstable_Lanza(string codigo) =>
        Should.Throw<ArgumentException>(() => Rol.Crear(codigo, "Un rol"));

    [Fact]
    public void Crear_ConUnCodigoDemasiadoLargo_Lanza() =>
        Should.Throw<ArgumentException>(() =>
            Rol.Crear(new string('a', Rol.LongitudDelCodigo + 1), "Un rol"));

    [Fact]
    public void Conceder_AnadeElPermisoUnaSolaVez()
    {
        var rol = Rol.Crear("contable", "Contable");

        rol.Conceder(s_crear).ShouldBeTrue();
        rol.Conceder(s_crear).ShouldBeFalse();

        rol.Tiene(s_crear).ShouldBeTrue();
        rol.Permisos.Count.ShouldBe(1);
    }

    // Autorizar una operación no autoriza lo que esa operación escribe: crear y modificar son
    // dos permisos, y conceder uno no puede conceder el otro aunque los sirva el mismo
    // controlador.
    [Fact]
    public void ConcederCrear_NoConcedeModificar()
    {
        var rol = Rol.Crear("alta-de-almacenes", "Alta de almacenes");

        rol.Conceder(s_crear);

        rol.Tiene(s_crear).ShouldBeTrue();
        rol.Tiene(s_modificar).ShouldBeFalse();
    }

    [Fact]
    public void Retirar_QuitaSoloEsePermiso()
    {
        var rol = Rol.Crear("contable", "Contable");
        rol.Conceder(s_crear);
        rol.Conceder(s_modificar);

        rol.Retirar(s_crear).ShouldBeTrue();

        rol.Tiene(s_crear).ShouldBeFalse();
        rol.Tiene(s_modificar).ShouldBeTrue();
    }

    [Fact]
    public void Retirar_UnPermisoQueNoTenia_NoHaceNada() =>
        Rol.Crear("contable", "Contable").Retirar(s_crear).ShouldBeFalse();

    // `FijarPermisos` es lo que necesita un formulario que edita la lista entera: si el borde
    // tuviera que calcular la diferencia, un descuido dejaría concedido un permiso que el
    // formulario ya no mostraba, que es la peor clase de permiso: el que nadie sabe que está.
    [Fact]
    public void FijarPermisos_DejaExactamenteLosQueSeLePasan()
    {
        var rol = Rol.Crear("contable", "Contable");
        rol.Conceder(s_crear);

        rol.FijarPermisos([s_modificar]);

        rol.Tiene(s_crear).ShouldBeFalse();
        rol.Tiene(s_modificar).ShouldBeTrue();
        rol.Permisos.Count.ShouldBe(1);
    }

    [Fact]
    public void FijarPermisos_ConLaListaVacia_LoDejaSinNinguno()
    {
        var rol = Rol.Crear("contable", "Contable");
        rol.Conceder(s_crear);

        rol.FijarPermisos([]);

        rol.Permisos.ShouldBeEmpty();
    }

    [Fact]
    public void FijarPermisos_ConRepetidos_NoLosDuplica()
    {
        var rol = Rol.Crear("contable", "Contable");

        rol.FijarPermisos([s_crear, s_crear, s_modificar]);

        rol.Permisos.Count.ShouldBe(2);
    }

    [Fact]
    public void Renombrar_CambiaElNombreYNoElCodigo()
    {
        var rol = Rol.Crear("contable", "Contable");

        rol.Renombrar("  Responsable de contabilidad  ");

        rol.Nombre.ShouldBe("Responsable de contabilidad");
        rol.Codigo.ShouldBe("contable");
    }

    [Fact]
    public void UnRolDeLaSemilla_SeMarcaComoDelSistema() =>
        Rol.Crear("administracion", "Administración", esDelSistema: true).EsDelSistema.ShouldBeTrue();
}
