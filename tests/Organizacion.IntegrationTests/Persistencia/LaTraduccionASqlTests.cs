using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Listados;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Contracts.Paginacion;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Bastion.BuildingBlocks.Infrastructure.Listados;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Domain.Empresas;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Organizacion.IntegrationTests.Persistencia;

/// <summary>
/// Que todo orden y todo filtro que un listado declara admitir <b>se traduce a SQL</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin contenedor, y por eso está aquí sin el rasgo <c>Integracion</c>.</b> Generar el SQL
/// necesita el proveedor de Npgsql y el modelo, no un servidor: es exactamente la parte del carril
/// de integración que <i>no necesita el carril</i>, y por tanto la que tiene que poder ejercerse
/// con Docker parado. Este proyecto es el único que ve los repositorios —lo dice su
/// <c>InternalsVisibleTo</c>—, así que es donde puede vivir.
/// </para>
/// <para>
/// <b>Por qué hace falta.</b> El paginador del ítem 1.3 arma el <c>OrderBy</c> por reflexión sobre
/// una <see cref="LambdaExpression"/> guardada en un diccionario. Un descuido corriente —declarar
/// la clave como <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>— mete un <c>Convert</c> en el
/// árbol, y ese <c>Convert</c> no revienta al compilar: revienta en ejecución, con un 500, y solo
/// sobre las propiedades con conversor de valor. Otro tanto pasa con el filtro: <c>ILIKE</c> sobre
/// una columna que no es texto compila igual de bien. Nada de eso lo ve el compilador y nada de
/// eso necesita una fila.
/// </para>
/// <para>
/// <b>El universo se descubre.</b> Los listados no se enumeran a mano: son los tipos del
/// ensamblado que declaran <see cref="IOrdenaPor"/>, y sus criterios salen del campo estático cuyo
/// TIPO es <see cref="CriteriosDe{T}"/> —por el tipo, no por el nombre—. Un repositorio nuevo
/// entra solo; uno escrito a mano se habría quedado fuera en silencio, que es el modo de fallo que
/// el ítem 1.2 encontró y el 1.3 vino a quitar.
/// </para>
/// </remarks>
public sealed class LaTraduccionASqlTests
{
    // No se conecta a nada: generar el SQL no abre conexión. La cadena tiene que ser sintáctica-
    // mente válida y nada más; el puerto 1 está ahí para que, si algún día alguien escribe aquí
    // una consulta que SÍ ejecute, falle en el acto en vez de esperar un tiempo de espera.
    private const string HaciaNingunSitio =
        "Host=127.0.0.1;Port=1;Database=nohay;Username=nadie;Password=nada;Timeout=1";

    [Fact]
    public void Todo_orden_y_todo_filtro_declarado_se_traduce_a_sql()
    {
        using OrganizacionDbContext contexto = Abrir();

        List<string> rotos = [];

        foreach (Sonda sonda in Sondas(contexto))
        {
            try
            {
                sonda.Sql();
            }
            catch (InvalidOperationException fallo)
            {
                rotos.Add($"{sonda.Nombre}: {fallo.Message}");
            }
        }

        rotos.ShouldBeEmpty(
            "estas consultas de listado no se pueden traducir a SQL, así que son un 500 en cuanto " +
            "alguien las pida:" + Environment.NewLine + string.Join(Environment.NewLine, rotos));
    }

    /// <summary>
    /// El arnés: que haya repositorios, que cada uno aporte sondas, y que una consulta
    /// intraducible se distinga de una que se traduce.
    /// </summary>
    /// <remarks>
    /// Sin esto, el test de arriba sale verde de las dos maneras en que puede estar roto: si el
    /// descubrimiento no encuentra ningún repositorio, y si <c>ToQueryString</c> dejara de
    /// intentar la traducción. Las dos dejan el 500 en pie con el carril en verde.
    /// </remarks>
    [Fact]
    public void El_barrido_ve_los_listados_y_reconoce_una_consulta_intraducible()
    {
        using OrganizacionDbContext contexto = Abrir();

        List<Sonda> sondas = [.. Sondas(contexto)];

        sondas.ShouldNotBeEmpty(
            "no se ha descubierto ni un repositorio con criterios de listado, así que el test de " +
            "al lado recorrería una lista vacía y saldría verde sin traducir nada");

        SortedSet<string> repositorios = new(
            sondas.Select(sonda => sonda.Repositorio), StringComparer.Ordinal);

        // Los diez listados de Organización del ítem 1.3. El número está escrito porque un
        // repositorio que perdiera su campo de criterios —y con él su orden— desaparecería de
        // aquí sin ruido, y el listado seguiría respondiendo, sin `ORDER BY`, con páginas que se
        // pisan entre sí.
        repositorios.Count.ShouldBe(10, "repositorios de Organización que listan: " +
            string.Join(", ", repositorios));

        // Y la pregunta de control: la consulta que el propio repositorio de empresas advierte que
        // NO se puede escribir —entrar en el `.Valor` de un valor convertido— tiene que romperse
        // aquí. Si esto no lanzara, el silencio del test de al lado no significaría nada.
        Should.Throw<InvalidOperationException>(
            () => contexto.Empresas.Where(empresa => empresa.Nif.Valor == "B12345674").ToQueryString(),
            "entrar en el `.Valor` de un valor convertido se traduce, así que esta prueba ya no " +
            "distingue una consulta traducible de una que no lo es");
    }

    /// <summary>
    /// La búsqueda por criterio se traduce entera, incluido el «después de esto» del cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va aparte porque no se puede sondear igual: <c>BuscarAsync</c> ejecuta, y su primera
    /// consulta es la que lleva el criterio, el orden y el «después de esto» —no hay un recuento
    /// por delante que se coma el fallo—. Así que se ejecuta de verdad contra un sitio donde no
    /// hay nadie, y lo que se afirma es <b>de qué</b> se queja.
    /// </para>
    /// <para>
    /// <b>El discriminante es el TIPO, no el mensaje</b>, y no es el de fuera: EF Core envuelve el
    /// fallo del proveedor en un <see cref="InvalidOperationException"/> que dice «likely due to a
    /// transient failure», así que mirar el tipo de arriba no distingue nada. Lo que distingue es
    /// si en la cadena hay una <see cref="DbException"/>: eso solo aparece si el SQL se generó y
    /// el proveedor llegó a intentar hablar con alguien. Un mensaje habría dependido del idioma
    /// del ejecutor.
    /// </para>
    /// <para>
    /// Y lleva su contraria en el mismo test: la consulta intraducible ejecutada por el mismo
    /// camino tiene que fallar <b>sin</b> ninguna <see cref="DbException"/> en la cadena. Sin esa
    /// mitad, el discriminante podría estar diciendo «sí» a todo.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_busqueda_por_criterio_y_su_cursor_se_traducen_a_sql()
    {
        using OrganizacionDbContext contexto = Abrir();
        var repositorio = new RepositorioDeEmpresas(contexto);

        var criterio = new CriterioDeEmpresas(Nif.De("B12345674"), "construcciones");

        Exception fallo = await Should.ThrowAsync<Exception>(
            () => repositorio.BuscarAsync(criterio, Guid.CreateVersion7(), 20, CancellationToken.None));

        LlegoALaBase(fallo).ShouldBeTrue(
            "la búsqueda no ha llegado a hablar con el proveedor: se ha roto antes, traduciendo. " +
            Describir(fallo));

        Exception intraducible = await Should.ThrowAsync<Exception>(
            () => contexto.Empresas
                .Where(empresa => empresa.Nif.Valor == "B12345674")
                .ToListAsync(CancellationToken.None));

        LlegoALaBase(intraducible).ShouldBeFalse(
            "una consulta que EF Core no sabe traducir ha llegado igualmente al proveedor, así " +
            "que este discriminante dice que sí a todo y la afirmación de arriba no vale nada. " +
            Describir(intraducible));
    }

    private static bool LlegoALaBase(Exception fallo)
    {
        for (Exception? actual = fallo; actual is not null; actual = actual.InnerException)
        {
            if (actual is DbException)
            {
                return true;
            }
        }

        return false;
    }

    private static string Describir(Exception fallo) =>
        "Cadena: " + string.Join(" -> ", Cadena(fallo).Select(uno => uno.GetType().Name)) +
        ". Mensaje: " + fallo.Message;

    private static IEnumerable<Exception> Cadena(Exception fallo)
    {
        for (Exception? actual = fallo; actual is not null; actual = actual.InnerException)
        {
            yield return actual;
        }
    }

    private static OrganizacionDbContext Abrir()
    {
        DbContextOptionsBuilder<OrganizacionDbContext> opciones = new();
        OrganizacionDbContext.Configurar(opciones, HaciaNingunSitio);

        return new OrganizacionDbContext(
            opciones.Options,
            new InquilinoDeMentira(),
            new SinAccesoALoBloqueado());
    }

    // Una sonda por listado, por campo ordenable y por sentido, más el orden de omisión y el
    // filtro. Es el producto entero a propósito: el `Convert` que rompe la traducción aparece por
    // el TIPO de la clave, así que basta con que un solo campo esté mal declarado.
    private static IEnumerable<Sonda> Sondas(OrganizacionDbContext contexto)
    {
        foreach (Type repositorio in typeof(OrganizacionDbContext).Assembly.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false }
                && typeof(IOrdenaPor).IsAssignableFrom(tipo))
            .OrderBy(tipo => tipo.Name, StringComparer.Ordinal))
        {
            FieldInfo? campo = repositorio
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(candidato => candidato.FieldType.IsGenericType
                    && candidato.FieldType.GetGenericTypeDefinition() == typeof(CriteriosDe<>));

            if (campo is null)
            {
                continue;
            }

            Type entidad = campo.FieldType.GetGenericArguments()[0];

            var sondas = (IEnumerable<Sonda>)typeof(LaTraduccionASqlTests)
                .GetMethod(nameof(SondasDe), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entidad)
                .Invoke(null, [contexto, repositorio.Name, campo.GetValue(null)!])!;

            foreach (Sonda sonda in sondas)
            {
                yield return sonda;
            }
        }
    }

    private static IEnumerable<Sonda> SondasDe<T>(
        OrganizacionDbContext contexto,
        string repositorio,
        CriteriosDe<T> criterios)
        where T : class
    {
        IQueryable<T> origen = contexto.Set<T>();

        yield return new Sonda(
            repositorio,
            $"{repositorio} orden de omisión",
            () => origen.Ordenar(null, criterios).ToQueryString());

        foreach (string ordenable in criterios.CamposOrdenables)
        {
            foreach (bool descendente in new[] { false, true })
            {
                yield return new Sonda(
                    repositorio,
                    $"{repositorio} ?sort={(descendente ? "-" : string.Empty)}{ordenable}",
                    () => origen.Ordenar(new Orden(ordenable, descendente), criterios).ToQueryString());
            }
        }

        if (criterios.Filtro is { } filtro)
        {
            yield return new Sonda(
                repositorio,
                $"{repositorio} ?q=",
                () => origen.Where(filtro("texto de prueba")).ToQueryString());
        }
    }

    private sealed record Sonda(string Repositorio, string Nombre, Func<string> Sql);

    // Los dos dobles contestan en vez de lanzar, al revés que los de `PostgresDeVerdad`: aquí SÍ
    // se compilan consultas sobre entidades del módulo, y los filtros de R8 y R16 forman parte de
    // lo que hay que traducir. Un doble que lanzara taparía justamente la mitad interesante.
    private sealed class InquilinoDeMentira : IInquilinoActual
    {
        private static readonly Guid s_empresa = Guid.CreateVersion7();

        public Guid? EmpresaDelFiltro => s_empresa;

        public bool HayEmpresaActiva => true;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Aquí no se suspende el inquilinato: lo que se prueba es que el filtro se TRADUCE.");
    }

    private sealed class SinAccesoALoBloqueado : IAccesoALoBloqueado
    {
        public bool Abierto => false;

        public MotivoParaVerLoBloqueado? MotivoDelAmbito => null;

        public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo) =>
            throw new NotSupportedException(
                "Aquí no se abre el ámbito de R16: lo que se prueba es que su filtro se TRADUCE.");
    }
}
