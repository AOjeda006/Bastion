using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Multiempresa;

/// <summary>
/// Base de los <c>DbContext</c> de módulo: aporta el único sitio del que sale la empresa por la
/// que filtra el inquilinato (R8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que esta clase NO hace: montar los filtros.</b> Cada contexto escribe su
/// <c>HasQueryFilter</c> por entidad, con una línea a la vista en su <c>OnModelCreating</c>. Un
/// barrido por reflexión sobre <see cref="Bastion.BuildingBlocks.Domain.Multiempresa.IDeInquilino"/>
/// sería más corto y peor: la expresión tendría que construirse con la instancia del contexto
/// dentro, y el modelo de EF Core se cachea por tipo de contexto y opciones — o sea, el filtro se
/// quedaría con el inquilino del <b>primer</b> contexto que construyera el modelo. Que no se
/// olvide ninguna entidad no lo garantiza la reflexión: lo garantiza
/// <c>CadaEntidadDeclaraSuInquilinatoTests</c>, que recorre el modelo ya construido y exige que
/// cada tipo tenga filtro o esté en la lista de globales, con su motivo.
/// </para>
/// <para>
/// <b>Y por qué es una propiedad y no un campo copiado en el constructor.</b> El filtro se evalúa
/// en <b>cada</b> consulta. Si el contexto copiase el identificador al construirse y alguien
/// activara la agrupación de contextos (<c>AddDbContextPool</c>) —o pusiera mal un ámbito—, el
/// contexto reutilizado seguiría filtrando por el inquilino del anterior y le serviría sus datos
/// al siguiente. Es un fallo que no da error, no sale en ningún test unitario y solo aparece con
/// dos inquilinos. Hoy los contextos se registran con <c>AddDbContext</c> (uno por petición), así
/// que la trampa no está armada; la propiedad es lo que hace que siga sin estarlo el día que eso
/// cambie. Lo fija <c>ElFiltroSeLeeEnCadaConsultaTests</c>.
/// </para>
/// </remarks>
/// <param name="opciones">Opciones del contexto.</param>
/// <param name="inquilino">De dónde sale la empresa activa.</param>
public abstract class ContextoDeModulo(DbContextOptions opciones, IInquilinoActual inquilino)
    : DbContext(opciones)
{
    /// <summary>
    /// Empresa por la que filtra <b>esta</b> consulta, o <c>null</c> si hay un ámbito sin
    /// inquilino abierto a propósito.
    /// </summary>
    /// <remarks>
    /// Es <c>protected</c> y no pública: quien la lee son los <c>HasQueryFilter</c> de las clases
    /// derivadas. Un caso de uso que necesite la empresa activa la pide por
    /// <c>IUsuarioActual.EmpresaId</c>, que es el puerto que existe para eso.
    /// </remarks>
    /// <exception cref="FaltaLaEmpresaActivaException">
    /// Si no hay empresa activa y tampoco ámbito sin inquilino. Falla cerrado a propósito: la
    /// alternativa es servir las filas de todas las empresas con un <c>200</c>.
    /// </exception>
    protected Guid? EmpresaDelFiltro => inquilino.EmpresaDelFiltro;
}
