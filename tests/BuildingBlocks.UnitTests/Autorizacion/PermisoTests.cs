using Bastion.BuildingBlocks.Domain.Autorizacion;
using Shouldly;

namespace Bastion.BuildingBlocks.UnitTests.Autorizacion;

/// <summary>
/// Un permiso es una cadena `modulo.recurso.accion` (§11). Que sea un TIPO y no un `string`
/// suelto es lo que impide que `organizacion.empresa.crear` y `organizacion.empresas.crear`
/// convivan en el mismo sistema sin que nadie se entere hasta que una de las dos no case.
/// </summary>
public sealed class PermisoTests
{
    [Theory]
    [InlineData("organizacion.empresa.crear")]
    [InlineData("ventas.pedido.confirmar")]
    [InlineData("contabilidad.asiento.contabilizar")]
    [InlineData("identidad.usuario.cambiar-contrasena")]
    public void De_ConLaFormaDelPlanMaestro_LoAcepta(string texto)
    {
        var permiso = Permiso.De(texto);

        permiso.Valor.ShouldBe(texto);
    }

    [Fact]
    public void De_DescomponeLasTresPartes()
    {
        var permiso = Permiso.De("ventas.pedido.confirmar");

        permiso.Modulo.ShouldBe("ventas");
        permiso.Recurso.ShouldBe("pedido");
        permiso.Accion.ShouldBe("confirmar");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ventas.pedido")]                 // faltan partes
    [InlineData("ventas.pedido.confirmar.ya")]    // sobran
    [InlineData("Ventas.Pedido.Confirmar")]       // mayúsculas
    [InlineData("ventas..confirmar")]             // parte vacía
    [InlineData("ventas.pedido.")]                // parte vacía al final
    [InlineData("ventas.pedido.con firmar")]      // espacio
    [InlineData("ventas.pedido.confirmar_ya")]    // guion bajo: la convención es el guion
    [InlineData("ventas.pedido.-confirmar")]      // guion al principio
    public void De_ConCualquierOtraCosa_Lanza(string texto) =>
        Should.Throw<ArgumentException>(() => Permiso.De(texto));

    [Fact]
    public void Intentar_NoLanza_YEsLaPuertaDelBorde()
    {
        Permiso.Intentar("no vale", out Permiso? malo).ShouldBeFalse();
        malo.ShouldBeNull();

        Permiso.Intentar("identidad.rol.crear", out Permiso? bueno).ShouldBeTrue();
        bueno!.Valor.ShouldBe("identidad.rol.crear");
    }

    // Dos permisos con el mismo texto son EL MISMO permiso: se comparan por valor, que es lo
    // que permite guardarlos en un conjunto y preguntar por pertenencia.
    [Fact]
    public void DosPermisosConElMismoTexto_SonIguales()
    {
        Permiso.De("crm.oportunidad.ver").ShouldBe(Permiso.De("crm.oportunidad.ver"));
        Permiso.De("crm.oportunidad.ver").ShouldNotBe(Permiso.De("crm.oportunidad.crear"));
    }

    // Crear y modificar son permisos DISTINTOS aunque los escriba el mismo código: autorizar
    // una operación no autoriza lo que esa operación escribe.
    [Fact]
    public void CrearYModificar_NoSonElMismoPermiso() =>
        Permiso.De("organizacion.almacen.crear")
            .ShouldNotBe(Permiso.De("organizacion.almacen.modificar"));
}
