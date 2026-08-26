namespace Bastion.BuildingBlocks.Domain.Multiempresa;

/// <summary>
/// Una entidad que <b>pertenece a una empresa</b> y por tanto se filtra por ella (R8).
/// </summary>
/// <remarks>
/// <para>
/// No todas las entidades son de inquilino, y la clasificación no es opinable: está escrita entera
/// en el <b>ADR-0011</b> y en <c>docs/PLAN.md</c>, con el motivo de cada una. Implementar esta
/// interfaz es la forma de decir «esta sí», y no implementarla es la forma de decir «esta no» — con
/// la diferencia de que lo segundo hay que justificarlo delante de un test: la lista de entidades
/// globales vive escrita en <c>CadaEntidadDeclaraSuInquilinatoTests</c>, así que una entidad nueva
/// que no esté en ninguno de los dos sitios pone el barrido en rojo.
/// </para>
/// <para>
/// Es un marcador de <b>clasificación</b>, no de comportamiento: quien aplica el filtro es el
/// <c>DbContext</c> de cada módulo, con una línea por entidad y a la vista. Un filtro montado por
/// reflexión sobre esta interfaz sería más corto y peor, porque la expresión tendría que capturar
/// la instancia del contexto y ahí es donde vive la trampa del filtro congelado (ADR-0011, punto 3).
/// </para>
/// </remarks>
public interface IDeInquilino
{
    /// <summary>Empresa a la que pertenece la fila (R8).</summary>
    Guid EmpresaId { get; }
}
