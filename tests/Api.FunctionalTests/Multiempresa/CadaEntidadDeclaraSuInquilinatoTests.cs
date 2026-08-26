using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Multiempresa;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Multiempresa;

/// <summary>
/// Que <b>no falte ninguna</b>: cada entidad del modelo, o filtra por empresa, o está en la lista
/// de globales con su motivo escrito.
/// </summary>
/// <remarks>
/// <para>
/// Los filtros se escriben a mano, una línea por entidad, en el <c>OnModelCreating</c> de cada
/// contexto (ADR-0011, punto 3: por reflexión el filtro se congelaría con el inquilino del primer
/// contexto). Escribirlos a mano tiene un precio evidente —se olvida uno— y este fichero es quien
/// lo paga: recorre el modelo <b>ya construido</b> y compara, en los dos sentidos, contra una
/// lista que hay que editar a propósito.
/// </para>
/// <para>
/// <b>Los dos sentidos importan.</b> Una entidad nueva sin filtro falla porque no está en la
/// lista. Y un nombre que sobra en la lista también falla: es lo que detecta que alguien puso el
/// motivo de una entidad que ya no existe, o que se le quitó el filtro a una que sí lo tenía y
/// «se arregló» añadiéndola aquí sin decírselo a nadie.
/// </para>
/// <para>
/// <b>Sin base de datos</b>: el modelo se construye antes de abrir ninguna conexión, así que esto
/// tarda milisegundos y sale en el paso rápido de la CI, no en el de Testcontainers.
/// </para>
/// </remarks>
public sealed class CadaEntidadDeclaraSuInquilinatoTests : IDisposable
{
    // Las entidades que NO filtran, y por qué. Cada línea es una decisión, no un descuido.
    private static readonly Dictionary<string, string> s_globalesAPosta = new(StringComparer.Ordinal)
    {
        ["Rol"] =
            "un rol es un catálogo de permisos de la instalación, no de una empresa (clasificación " +
            "del inquilinato, ADR-0011). Consecuencia asumida y escrita: un rol creado desde una " +
            "empresa se ve y se asigna desde las demás",

        ["PermisoDeRol"] =
            "los permisos de un rol son parte del rol; no tiene DbSet ni consulta propia",

        ["RolDeMembresia"] =
            "asignación dependiente de la pertenencia, que sí filtra; no tiene DbSet ni consulta " +
            "propia, y que siga sin tenerlos lo comprueba ElFiltroNoSeSaltaPorAhiTests",

        ["TokenDeRefresco"] =
            "una emisión de refresco es de una sesión, no de una empresa: se busca por su resumen " +
            "antes de que haya empresa activa. La empresa con la que se estaba operando va DENTRO " +
            "de la fila (EmpresaActivaId) y la comprueba RenovarSesion",
    };

    // Las dos que filtran SIN llevar `empresa_id`, y por qué el filtro no es el de siempre.
    private static readonly Dictionary<string, string> s_filtranSinSerDeInquilino = new(StringComparer.Ordinal)
    {
        ["Empresa"] = "es la raíz del inquilinato: se filtra por su propia clave",
        ["Usuario"] = "es global, y la consulta se acota por la pertenencia, que es lo que dice quién comparte empresa",
    };

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_entidad_del_modelo_se_queda_sin_filtro_y_sin_motivo()
    {
        List<string> huerfanas = [.. DelModelo()
            .Where(tipo => tipo.GetDeclaredQueryFilters().Count == 0)
            .Select(tipo => tipo.ClrType.Name)
            .Where(nombre => !s_globalesAPosta.ContainsKey(nombre))];

        // Una entidad nueva sin filtro no rompe ningún test de R8 —nadie ha escrito el suyo—, y
        // el síntoma en producción son las filas de otra empresa saliendo por un listado nuevo.
        huerfanas.ShouldBeEmpty(
            "estas entidades no filtran por empresa y no declaran por qué: " + string.Join(", ", huerfanas));
    }

    [Fact]
    public void La_lista_de_globales_no_nombra_entidades_que_ya_no_estan_o_que_si_filtran()
    {
        HashSet<string> sinFiltro = [.. DelModelo()
            .Where(tipo => tipo.GetDeclaredQueryFilters().Count == 0)
            .Select(tipo => tipo.ClrType.Name)];

        List<string> sobran = [.. s_globalesAPosta.Keys.Where(nombre => !sinFiltro.Contains(nombre))];

        sobran.ShouldBeEmpty(
            "estas entidades están declaradas como globales y no lo son (o ya no existen): " +
            string.Join(", ", sobran));
    }

    [Fact]
    public void Toda_entidad_marcada_como_de_inquilino_filtra()
    {
        List<string> incoherentes = [.. DelModelo()
            .Where(tipo => typeof(IDeInquilino).IsAssignableFrom(tipo.ClrType))
            .Where(tipo => tipo.GetDeclaredQueryFilters().Count == 0)
            .Select(tipo => tipo.ClrType.Name)];

        // `IDeInquilino` es una clasificación, no un comportamiento: no hace nada por sí sola.
        // Esto es lo que hace que declararla signifique algo.
        incoherentes.ShouldBeEmpty(
            "estas entidades se declaran de inquilino y no filtran: " + string.Join(", ", incoherentes));
    }

    [Fact]
    public void Toda_entidad_que_filtra_sin_ser_de_inquilino_esta_documentada()
    {
        List<string> sinExplicar = [.. DelModelo()
            .Where(tipo => tipo.GetDeclaredQueryFilters().Count > 0)
            .Where(tipo => !typeof(IDeInquilino).IsAssignableFrom(tipo.ClrType))
            .Select(tipo => tipo.ClrType.Name)
            .Where(nombre => !s_filtranSinSerDeInquilino.ContainsKey(nombre))];

        // Al revés que el anterior: un filtro sobre una entidad que no lleva `empresa_id` está
        // filtrando por OTRA cosa —una clave propia, una navegación—, y eso hay que haberlo
        // pensado. Si nadie lo ha escrito aquí, probablemente no se pensó.
        sinExplicar.ShouldBeEmpty(
            "estas entidades filtran sin llevar empresa_id y sin explicar por qué: " +
            string.Join(", ", sinExplicar));
    }

    // Las de propiedad quedan fuera: un tipo de propiedad no se consulta por su cuenta, viaja
    // dentro de su dueño, y EF Core ni siquiera admite un filtro sobre él.
    private IReadOnlyList<IEntityType> DelModelo()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return
        [
            .. Entidades(alcance.ServiceProvider.GetRequiredService<OrganizacionDbContext>()),
            .. Entidades(alcance.ServiceProvider.GetRequiredService<IdentidadDbContext>()),
        ];
    }

    private static IEnumerable<IEntityType> Entidades(DbContext contexto) =>
        contexto.Model.GetEntityTypes().Where(tipo => !tipo.IsOwned());
}
