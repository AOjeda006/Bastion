namespace Bastion.BuildingBlocks.Application;

/// <summary>
/// Confirma en UNA transacción todos los cambios que ha hecho un caso de uso.
/// </summary>
/// <remarks>
/// <para>
/// Los repositorios no confirman por su cuenta (`patrones/repository-y-dto.md`). Si lo hicieran,
/// un caso de uso que toca dos agregados dejaría el primero grabado y el segundo no cuando algo
/// falla en medio, y no habría manera de deshacerlo: el fallo se descubre con los datos ya
/// partidos por la mitad. Quien decide cuándo se confirma es el caso de uso, que es el único que
/// sabe dónde acaba la operación de negocio.
/// </para>
/// <para>
/// Es un puerto de la capa de aplicación y no una interfaz sobre EF Core: lo implementa
/// Infrastructure, sobre el <c>DbContext</c> del módulo que corresponda. La dependencia sigue
/// apuntando hacia dentro.
/// </para>
/// <para>
/// <b>Ningún caso de uso pide ESTE tipo.</b> Cada módulo declara el suyo heredando de aquí
/// —<c>IUnidadTrabajoDeOrganizacion</c>, <c>IUnidadTrabajoDeIdentidad</c>— y es ese el que se
/// inyecta. Con un único tipo compartido, el contenedor resuelve por tipo y gana la última
/// inscripción: el segundo módulo en registrarse se llevaría por delante al primero, y los casos
/// de uso de aquel confirmarían sobre un contexto que no rastrea nada. Cero filas grabadas, cero
/// excepciones y un <c>201</c> en la respuesta. Lo que esta interfaz aporta es la FORMA común;
/// quién confirma qué lo dice el tipo de cada módulo.
/// </para>
/// </remarks>
public interface IUnidadTrabajo
{
    /// <summary>Confirma los cambios pendientes.</summary>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>Cuántas filas se han visto afectadas.</returns>
    Task<int> ConfirmarAsync(CancellationToken cancelacion);
}
