using Bastion.BuildingBlocks.Domain.Autorizacion;

namespace Bastion.BuildingBlocks.Application.Autorizacion;

/// <summary>
/// Todos los permisos que existen en la aplicación, que es contra lo que se valida un rol.
/// </summary>
/// <remarks>
/// <para>
/// <b>El catálogo lo compone el <i>composition root</i>, no Identidad.</b> Cada módulo declara
/// sus permisos en su propio <c>Contracts</c>; el host los junta y registra la unión. Si el
/// catálogo viviera en Identidad, Identidad tendría que referenciar a los dieciséis módulos para
/// conocer sus permisos, y la frontera del §4 quedaría del revés: el módulo genérico dependiendo
/// de todos los demás.
/// </para>
/// <para>
/// <b>Y por qué es código y no una tabla.</b> Los permisos no son datos: son la lista de puertas
/// que el código tiene. Una tabla sería una copia que hay que mantener sincronizada, y el día que
/// se desincronice el síntoma es un rol que concede un permiso que ya no existe —o peor, un
/// endpoint que exige uno que nadie puede tener—. Lo que sí es dato es qué rol concede qué, y eso
/// sí está en una tabla, validado contra este catálogo al escribirlo.
/// </para>
/// </remarks>
public interface ICatalogoDePermisos
{
    /// <summary>Todos los permisos declarados, ordenados.</summary>
    IReadOnlyList<Permiso> Todos { get; }

    /// <summary>Si ese permiso está declarado por algún módulo.</summary>
    /// <param name="permiso">Permiso que se busca.</param>
    bool Contiene(Permiso permiso);
}
