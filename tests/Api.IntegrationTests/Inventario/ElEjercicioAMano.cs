using Bastion.Api.IntegrationTests.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Inventario.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.Api.IntegrationTests.Inventario;

/// <summary>
/// Mover y borrar un ejercicio con sus adaptadores REALES, para poder pararlos antes del
/// <c>COMMIT</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe para un solo orden de la carrera: «mover o borrar primero».</b> Por la API, una
/// petición abre su transacción y la confirma sin que el caso pueda meterse en medio, y lo que hay
/// que ejercer es justo ese medio: el caso de uso que ya ha decidido y todavía no ha soltado. El
/// otro orden, «el documento primero», va por la API, porque allí quien se queda a medias es la
/// anulación y la petición puede correr entera.
/// </para>
/// <para>
/// <b>El puerto de documentos va sobre un contexto de Inventario propio</b>, como en producción,
/// donde cada módulo resuelve el suyo: la pregunta de Organización no viaja en la transacción de
/// nadie, y ve lo que esté confirmado en el momento de hacerla.
/// </para>
/// </remarks>
internal sealed class ElEjercicioAMano : IAsyncDisposable
{
    private readonly OrganizacionDbContext _organizacion;
    private readonly InventarioDbContext _inventario;

    internal ElEjercicioAMano(PostgresConTodosLosModulos postgres, Guid empresaId)
    {
        _organizacion = postgres.AbrirOrganizacion(empresaId);
        _inventario = postgres.AbrirInventario(empresaId);

        RepositorioDeEjercicios ejercicios = new(_organizacion);
        CerrojoDeEjercicios cerrojo = new(_organizacion, new InquilinoFijo(empresaId));
        UnidadDeTrabajoDeOrganizacion unidadDeTrabajo = new(_organizacion);
        VersionesDeOrganizacion versiones = new(_organizacion);
        IDocumentosDeUnPeriodo[] documentos = [new LosDocumentosDeInventarioEnUnPeriodo(_inventario)];

        Mover = new ModificarEjercicio(ejercicios, cerrojo, unidadDeTrabajo, versiones, documentos);
        Borrar = new EliminarEjercicio(ejercicios, cerrojo, unidadDeTrabajo, versiones, documentos);
    }

    internal IModificarEjercicio Mover { get; }

    internal IEliminarEjercicio Borrar { get; }

    /// <summary>Abre la transacción de Organización, que el caso de uso reutiliza sin confirmarla.</summary>
    /// <returns>La transacción, abierta: quien la pidió decide cuándo suelta.</returns>
    internal Task<IDbContextTransaction> AbrirTransaccionAsync() =>
        _organizacion.Database.BeginTransactionAsync();

    public async ValueTask DisposeAsync()
    {
        await _inventario.DisposeAsync();
        await _organizacion.DisposeAsync();
    }
}
