using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.Identidad.Application.Usuarios;
using Bastion.Identidad.Domain.Usuarios;
using Bastion.Identidad.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Persistencia;

/// <summary>
/// Que dar de alta a alguien en una empresa salga como un <c>INSERT</c> y no como un
/// <c>UPDATE</c> que no toca ninguna fila.
/// </summary>
/// <remarks>
/// <para>
/// La avería que fija este fichero costó un rojo entero de la CI y no se veía por ninguna otra
/// parte. Cuando el usuario se ha leído de la base, EF Core lo tiene en <c>Unchanged</c>; al
/// detectar cambios se encuentra en su colección una <see cref="Membresia"/> que no seguía y
/// decide qué es <b>mirando si tiene clave</b>. La tiene —el constructor le pone un <c>Guid</c>
/// v7 el primer día—, así que da por hecho que ya existía y la marca <c>Modified</c>. El
/// <c>UPDATE</c> resultante no encuentra la fila, y eso es un <c>DbUpdateConcurrencyException</c>:
/// un <c>500</c> en cada alta.
/// </para>
/// <para>
/// <b>Sin base de datos.</b> Lo que hay que comprobar es en qué estado deja EF Core la entidad, y
/// eso se decide antes de abrir ninguna conexión. Ese es justamente el punto: la avería solo se vio
/// contra PostgreSQL en la CI, cuando bastaba con preguntar por el estado —aquí, en dos segundos y
/// en la máquina de cualquiera—.
/// </para>
/// </remarks>
public sealed class LasPertenenciasNuevasSeInsertanTests : IDisposable
{
    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Una_pertenencia_concedida_a_un_usuario_ya_guardado_sale_como_alta()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IRepositorioDeUsuarios usuarios = Repositorio(alcance);
        IdentidadDbContext contexto = Contexto(alcance);

        Usuario usuario = ComoSiViniraDeLaBase(contexto);

        usuarios.Registrar(usuario.Conceder(Guid.CreateVersion7()));

        contexto.ChangeTracker.DetectChanges();
        Estado(contexto, usuario.Membresias.Single()).ShouldBe(EntityState.Added);
    }

    [Fact]
    public void Sin_registrarla_EF_Core_la_daria_por_existente()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IdentidadDbContext contexto = Contexto(alcance);

        Usuario usuario = ComoSiViniraDeLaBase(contexto);

        // Colgarla del usuario y nada más: el camino que parecía suficiente.
        Membresia membresia = usuario.Conceder(Guid.CreateVersion7());

        contexto.ChangeTracker.DetectChanges();

        // Es un canario, no una bendición: fija POR QUÉ existe `Registrar`. El día que EF Core
        // cambie de criterio, este test se pondrá rojo y el rodeo podrá desaparecer.
        Estado(contexto, membresia).ShouldBe(EntityState.Modified);
    }

    [Fact]
    public void Con_el_usuario_recien_creado_la_pertenencia_ya_salia_bien()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IRepositorioDeUsuarios usuarios = Repositorio(alcance);
        IdentidadDbContext contexto = Contexto(alcance);

        Usuario usuario = Cuenta();
        Membresia membresia = usuario.Conceder(Guid.CreateVersion7());
        usuarios.Agregar(usuario);

        contexto.ChangeTracker.DetectChanges();

        // Aquí el hijo hereda el alta del padre. Es el camino de la semilla, el único que se
        // ejecutaba en cada arranque, y por eso nada delató lo otro hasta la CI.
        Estado(contexto, membresia).ShouldBe(EntityState.Added);
    }

    [Fact]
    public void Un_rol_nuevo_sobre_una_pertenencia_que_ya_existia_tambien_sale_como_alta()
    {
        using IServiceScope alcance = _api.Services.CreateScope();
        IdentidadDbContext contexto = Contexto(alcance);

        Usuario usuario = Cuenta();
        Membresia membresia = usuario.Conceder(Guid.CreateVersion7());
        contexto.Entry(usuario).State = EntityState.Unchanged;
        contexto.Entry(membresia).State = EntityState.Unchanged;

        membresia.AsignarRol(Guid.CreateVersion7());

        contexto.ChangeTracker.DetectChanges();

        // `RolDeMembresia` tiene clave COMPUESTA, y con esa EF Core acierta solo. Por eso
        // `AsignarRol` no necesita el mismo rodeo, y por eso no se le ha puesto.
        Estado(contexto, membresia.Roles.Single()).ShouldBe(EntityState.Added);
    }

    private static IRepositorioDeUsuarios Repositorio(IServiceScope alcance) =>
        alcance.ServiceProvider.GetRequiredService<IRepositorioDeUsuarios>();

    private static IdentidadDbContext Contexto(IServiceScope alcance) =>
        alcance.ServiceProvider.GetRequiredService<IdentidadDbContext>();

    private static EntityState Estado(IdentidadDbContext contexto, object entidad) =>
        contexto.Entry(entidad).State;

    private static Usuario Cuenta() => Usuario.Crear(
        Correo.De($"pertenencias-{Guid.CreateVersion7():N}@bastion.pruebas"),
        "Cuenta de prueba",
        "resumen-que-no-es-una-contrasena",
        DateTimeOffset.UtcNow);

    // `Unchanged` es exactamente en lo que queda un usuario recién leído: es la única forma de
    // reproducir el camino de producción sin una base de datos delante.
    private static Usuario ComoSiViniraDeLaBase(IdentidadDbContext contexto)
    {
        Usuario usuario = Cuenta();
        contexto.Entry(usuario).State = EntityState.Unchanged;
        return usuario;
    }
}
