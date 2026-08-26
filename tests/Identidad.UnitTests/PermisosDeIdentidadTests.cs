using System.Reflection;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.Identidad.Contracts;
using Shouldly;

namespace Bastion.Identidad.UnitTests;

/// <summary>
/// El catálogo de permisos es una lista escrita a mano, y una lista escrita a mano se olvida.
/// Estos tests son el guardián: si alguien añade una constante y no la mete en <c>Todos</c>, el
/// permiso existe para el endpoint que lo exige y no existe para ningún rol que lo pueda
/// conceder — o sea, una puerta que no abre nunca y nadie sabe por qué.
/// </summary>
public sealed class PermisosDeIdentidadTests
{
    private static readonly IReadOnlyList<string> s_constantes = ConstantesDe(typeof(PermisosDeIdentidad));

    [Fact]
    public void TodasLasConstantesDeclaradas_EstanEnLaLista() =>
        PermisosDeIdentidad.Todos.ShouldBe(s_constantes, ignoreOrder: true);

    [Fact]
    public void TodosTienenLaFormaDeUnPermiso()
    {
        foreach (string texto in PermisosDeIdentidad.Todos)
        {
            Permiso.Intentar(texto, out Permiso? permiso)
                .ShouldBeTrue($"«{texto}» no tiene la forma modulo.recurso.accion");
            permiso!.Modulo.ShouldBe("identidad");
        }
    }

    [Fact]
    public void NoHayNingunoRepetido() =>
        PermisosDeIdentidad.Todos.Distinct(StringComparer.Ordinal)
            .Count().ShouldBe(PermisosDeIdentidad.Todos.Count);

    // Crear y modificar son permisos distintos; ver y bloquear también. Si un recurso apareciera
    // con un solo permiso, «puede consultar» se habría convertido en «puede todo» sin que nadie
    // lo decidiera.
    [Fact]
    public void CadaRecurso_TieneMasDeUnVerbo()
    {
        IEnumerable<IGrouping<string, Permiso>> porRecurso = PermisosDeIdentidad.Todos
            .Select(Permiso.De)
            .GroupBy(permiso => permiso.Recurso, StringComparer.Ordinal);

        foreach (IGrouping<string, Permiso> recurso in porRecurso)
        {
            recurso.Count().ShouldBeGreaterThan(
                1,
                $"el recurso «{recurso.Key}» solo declara un permiso: revisa si consultarlo y " +
                "cambiarlo se están autorizando con la misma llave");
        }
    }

    internal static IReadOnlyList<string> ConstantesDe(Type tipo) =>
        [.. tipo
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(campo => campo is { IsLiteral: true, IsInitOnly: false } && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)];
}
