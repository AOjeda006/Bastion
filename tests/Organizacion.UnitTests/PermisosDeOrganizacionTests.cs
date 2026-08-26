using System.Reflection;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Organizacion.Contracts;
using Shouldly;

namespace Bastion.Organizacion.UnitTests;

/// <summary>
/// Guardián del catálogo del módulo: una constante que no llegue a <c>Todos</c> es un permiso que
/// el endpoint exige y que ningún rol puede conceder — una puerta que no abre nunca.
/// </summary>
public sealed class PermisosDeOrganizacionTests
{
    private static readonly IReadOnlyList<string> s_constantes = ConstantesDe(typeof(PermisosDeOrganizacion));

    [Fact]
    public void TodasLasConstantesDeclaradas_EstanEnLaLista() =>
        PermisosDeOrganizacion.Todos.ShouldBe(s_constantes, ignoreOrder: true);

    [Fact]
    public void TodosTienenLaFormaDeUnPermiso()
    {
        foreach (string texto in PermisosDeOrganizacion.Todos)
        {
            Permiso.Intentar(texto, out Permiso? permiso)
                .ShouldBeTrue($"«{texto}» no tiene la forma modulo.recurso.accion");
            permiso!.Modulo.ShouldBe("organizacion");
        }
    }

    [Fact]
    public void NoHayNingunoRepetido() =>
        PermisosDeOrganizacion.Todos.Distinct(StringComparer.Ordinal)
            .Count().ShouldBe(PermisosDeOrganizacion.Todos.Count);

    // Los cuatro recursos del 0.4, cada uno con sus verbos. Está escrito como cifra esperada y no
    // como «mayor que uno» porque este módulo ya está terminado: si el número cambia, es que
    // alguien ha abierto o cerrado una puerta, y eso hay que verlo.
    [Theory]
    [InlineData("empresa", 5)]
    [InlineData("ejercicio", 6)]
    [InlineData("serie", 4)]
    [InlineData("almacen", 5)]
    public void CadaRecursoDeclaraSusVerbos(string recurso, int cuantos) =>
        PermisosDeOrganizacion.Todos
            .Select(Permiso.De)
            .Count(permiso => permiso.Recurso == recurso)
            .ShouldBe(cuantos);

    // Deshacer un bloqueo legal no puede ir con la misma llave que ponerlo, ni reabrir un
    // ejercicio con la misma que cerrarlo (§11, segregación de funciones).
    [Theory]
    [InlineData(PermisosDeOrganizacion.EmpresaBloquear, PermisosDeOrganizacion.EmpresaDesbloquear)]
    [InlineData(PermisosDeOrganizacion.AlmacenBloquear, PermisosDeOrganizacion.AlmacenDesbloquear)]
    [InlineData(PermisosDeOrganizacion.EjercicioCerrar, PermisosDeOrganizacion.EjercicioReabrir)]
    [InlineData(PermisosDeOrganizacion.EmpresaCrear, PermisosDeOrganizacion.EmpresaModificar)]
    public void HacerYDeshacer_SonPermisosDistintos(string uno, string otro) =>
        uno.ShouldNotBe(otro);

    private static IReadOnlyList<string> ConstantesDe(Type tipo) =>
        [.. tipo
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(campo => campo is { IsLiteral: true, IsInitOnly: false } && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)];
}
