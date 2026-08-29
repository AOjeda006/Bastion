using System.Reflection;
using System.Text.RegularExpressions;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bastion.Api.FunctionalTests.BandejaDeSalida;

/// <summary>
/// Que ningún evento se quede sin nombre: cada tipo que hereda de <see cref="EventoDeIntegracion"/>
/// está declarado en el catálogo, y cada declaración corresponde a un tipo que existe.
/// </summary>
/// <remarks>
/// <para>
/// El nombre no se saca del tipo a propósito: con el nombre de la clase, renombrarla rompería las
/// filas que ya están en la cola y las que están guardadas en la traza de cualquier consumidor. El
/// precio de esa decisión es que hay que declararlo a mano, y quien lo paga es este barrido —el
/// que se olvida no se entera hasta que el interceptor lanza en tiempo de ejecución, dentro de un
/// caso de uso que no tenía nada que ver.
/// </para>
/// <para>
/// <b>Los dos sentidos, como en los demás barridos.</b> Un evento sin declarar falla porque no
/// está; una declaración que ya no corresponde a ningún tipo falla porque sobra — y esa es la que
/// avisa de que alguien retiró un evento dejando su nombre puesto.
/// </para>
/// <para>
/// <b>Sin base de datos:</b> el catálogo se construye al montar el contenedor, así que esto sale
/// en el paso rápido de la CI.
/// </para>
/// </remarks>
public sealed class CadaEventoEstaDeclaradoTests : IDisposable
{
    // `modulo.hecho-ocurrido`: minúsculas, un punto, y guiones dentro de cada parte. No es
    // decoración — es el identificador que va escrito en cada fila de la cola y en cada huella de
    // consumidor, así que la forma se fija una vez y no se discute en cada módulo.
    private const string Forma = "^[a-z]+(?:-[a-z]+)*\\.[a-z]+(?:-[a-z]+)*$";

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ningun_evento_de_integracion_se_queda_sin_declarar()
    {
        CatalogoDeEventos catalogo = Catalogo();

        List<string> sinDeclarar = [.. CatalogoDeEventos.EventosDe(DeBastion())
            .Where(tipo => !catalogo.Conoce(tipo))
            .Select(tipo => tipo.FullName!)];

        sinDeclarar.ShouldBeEmpty(
            "estos eventos no tienen nombre en el catálogo, así que el interceptor lanzará al " +
            "volcarlos: se declaran en el `Modulo…` del módulo que los emite, con " +
            "`DeclararEvento<T>(\"modulo.hecho-ocurrido\")`. Son: " + string.Join(", ", sinDeclarar));
    }

    [Fact]
    public void Ninguna_declaracion_nombra_un_evento_que_ya_no_existe()
    {
        HashSet<Type> existentes = [.. CatalogoDeEventos.EventosDe(DeBastion())];

        List<string> sobran = [.. Catalogo().Declarados
            .Where(tipo => !existentes.Contains(tipo))
            .Select(tipo => tipo.FullName!)];

        sobran.ShouldBeEmpty("estas declaraciones no corresponden a ningún evento: " + string.Join(", ", sobran));
    }

    [Fact]
    public void Todos_los_nombres_tienen_la_forma_acordada()
    {
        CatalogoDeEventos catalogo = Catalogo();

        List<string> raros = [.. catalogo.Declarados
            .Select(catalogo.NombreDe)
            .Where(nombre => !Regex.IsMatch(nombre, Forma, RegexOptions.None, TimeSpan.FromSeconds(1)))];

        raros.ShouldBeEmpty("estos nombres no tienen la forma `modulo.hecho-ocurrido`: " + string.Join(", ", raros));
    }

    private CatalogoDeEventos Catalogo()
    {
        using IServiceScope alcance = _api.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<CatalogoDeEventos>();
    }

    // Los ensamblados del sistema, ya cargados porque el contenedor está montado. Se barren TODOS
    // y no solo los `Contracts`: un evento declarado donde no toca seguiría siendo un evento, y lo
    // que este test defiende es que ninguno se quede sin nombre, viva donde viva.
    private static IEnumerable<Assembly> DeBastion() => AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(ensamblado => ensamblado.GetName().Name is string nombre
            && nombre.StartsWith("Bastion.", StringComparison.Ordinal)
            && !nombre.EndsWith("Tests", StringComparison.Ordinal));
}
