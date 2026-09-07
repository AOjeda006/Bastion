using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Infrastructure.Bloqueos;
using Bastion.Identidad.Infrastructure.Persistencia;
using Bastion.Identidad.Infrastructure.Persistencia.Repositorios;
using Bastion.Organizacion.Infrastructure.Persistencia;
using Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;
using Bastion.Terceros.Infrastructure.Persistencia;
using Bastion.Terceros.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bastion.Api.IntegrationTests.Bloqueos;

/// <summary>
/// El listado del artículo 32 se traduce a SQL <b>en los tres módulos</b>, no en uno.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin contenedor, y por eso NO lleva el rasgo <c>Integracion</c>.</b> Generar el SQL necesita
/// el proveedor de Npgsql y el modelo, no un servidor: es la parte del carril de integración que
/// <i>no necesita el carril</i>, y por tanto la que tiene que poder ejercerse con Docker parado. Es
/// el segundo caso de este ensamblado que corre en el carril rápido, y está declarado como tal en
/// la cabecera del <c>.csproj</c> y en el paso de la CI, junto al censo.
/// </para>
/// <para>
/// <b>Y está aquí, y no en <c>Organizacion.IntegrationTests</c>, por el motivo del ítem.</b> Hasta
/// el 1.6 este barrido vivía allí y decía «el listado de lo bloqueado se traduce entero» mirando un
/// repositorio que unía tres tablas del esquema <c>organizacion</c>. Desde el 1.6 el listado lo
/// componen tres módulos, así que allí «entero» habría pasado a significar un tercio —por
/// construcción, que es exactamente el defecto de cumplimiento que este ítem vino a cerrar—. Este
/// es el único proyecto de pruebas que ve las tres infraestructuras.
/// </para>
/// <para>
/// <b>Por qué hace falta.</b> Las tres proyecciones se ordenan por <c>Collate(…, "C")</c> y se
/// filtran por <c>ILIKE</c> sobre un tipo que no es una entidad, y la de Organización llega además
/// detrás de un <c>UNION</c>. Nada de eso lo ve el compilador: un <c>?sort=</c> intraducible
/// aparece como un 500 en la pantalla del art. 32, que es la que se abre cuando alguien viene a
/// rectificar un bloqueo hecho por error. Y la de Identidad lleva una subconsulta correlacionada
/// —la membresía, que es lo que impide que una empresa vea los usuarios bloqueados de otra—, que es
/// justo la forma que EF Core puede decidir no traducir.
/// </para>
/// </remarks>
public sealed class ElListadoDelArticulo32SeTraduceEnteroTests
{
    // No se conecta a nada. `ToQueryString()` no abre conexión; el puerto 1 está para que, si
    // alguien escribe aquí una consulta que SÍ ejecute, falle en el acto en vez de esperar.
    private const string HaciaNingunSitio =
        "Host=127.0.0.1;Port=1;Database=nohay;Username=nadie;Password=nada;Timeout=1";

    /// <summary>
    /// Las proyecciones declaradas, una por módulo que bloquea.
    /// </summary>
    /// <remarks>
    /// Escritas a mano y comparadas enteras contra lo que hay compilado, que es lo que hace el
    /// resto del proyecto con toda lista cerrada. Descubrirlas por reflexión sería posible, pero
    /// la lista tiene que poder leerse: quien añada un módulo que bloquea tiene que tropezarse con
    /// esta línea, no heredarla en silencio.
    /// </remarks>
    private static IReadOnlyList<Proyeccion> Declaradas() =>
    [
        new("Identidad",
            () => ConsultaDeLoBloqueadoDeIdentidad.LoBloqueado(
                new IdentidadDbContext(
                    Opciones<IdentidadDbContext>(IdentidadDbContext.Configurar),
                    new InquilinoDeMentira(),
                    new AmbitoAbierto()))),

        new("Organizacion",
            () => ConsultaDeLoBloqueadoDeOrganizacion.LoBloqueado(
                new OrganizacionDbContext(
                    Opciones<OrganizacionDbContext>(OrganizacionDbContext.Configurar),
                    new InquilinoDeMentira(),
                    new AmbitoAbierto()))),

        new("Terceros",
            () => ConsultaDeLoBloqueadoDeTerceros.LoBloqueado(
                new TercerosDbContext(
                    Opciones<TercerosDbContext>(TercerosDbContext.Configurar),
                    new InquilinoDeMentira(),
                    new AmbitoAbierto()))),
    ];

    /// <summary>
    /// La lista de arriba y las implementaciones compiladas del puerto son el mismo conjunto.
    /// </summary>
    /// <remarks>
    /// El ancla de este fichero, y la mitad que impide que se quede viejo. Un módulo que empiece a
    /// bloquear registra su puerto y el listado empieza a servir sus filas; sin esta comparación,
    /// su proyección sería la única que nadie comprueba que se traduce, y el ítem 1.6 existe
    /// precisamente porque un módulo se quedó fuera de un barrido sin que nada avisara.
    /// </remarks>
    [Fact]
    public void Las_proyecciones_declaradas_son_las_que_implementan_el_puerto()
    {
        IReadOnlyList<string> compiladas =
        [
            .. from ensamblado in new[]
               {
                   typeof(IdentidadDbContext).Assembly,
                   typeof(OrganizacionDbContext).Assembly,
                   typeof(TercerosDbContext).Assembly,
               }
               from tipo in ensamblado.GetTypes()
               where tipo is { IsAbstract: false, IsInterface: false }
                  && typeof(IConsultaDeLoBloqueado).IsAssignableFrom(tipo)
               orderby tipo.Name, StringComparer.Ordinal
               select ensamblado.GetName().Name!.Split('.')[1],
        ];

        compiladas.ShouldNotBeEmpty(
            "no se ha encontrado ninguna implementación de `IConsultaDeLoBloqueado`, así que este " +
            "fichero está comprobando la traducción de un listado que nadie sirve");

        IReadOnlyList<string> declaradas = [.. Declaradas().Select(una => una.Modulo).Order(StringComparer.Ordinal)];

        IReadOnlyList<string> ordenadas = [.. compiladas.Order(StringComparer.Ordinal)];

        ordenadas.ShouldBe(
            declaradas,
            customMessage:
            "las proyecciones declaradas en este fichero y los módulos que implementan el puerto " +
            "del art. 32 han dejado de ser el mismo conjunto. Compiladas: " +
            string.Join(", ", compiladas) + ". Declaradas: " + string.Join(", ", declaradas));
    }

    /// <summary>
    /// Cada proyección, con cada orden que el listado admite y con el filtro puesto, se traduce.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El producto entero de módulos × campos × sentidos, más el orden de omisión, y todo con el
    /// <c>?q=</c> puesto: lo que rompe la traducción es el TIPO de una expresión, así que basta con
    /// que un solo campo esté mal escrito para que solo esa combinación reviente.
    /// </para>
    /// <para>
    /// Se ejerce por <see cref="ConsultasDeLoBloqueado.Consulta"/>, que es la MISMA expresión que
    /// ejecuta <c>ResponderAsync</c>. Contra el listado entero no valdría: <c>ResponderAsync</c>
    /// cuenta antes de traer, el <c>COUNT</c> revienta primero contra una conexión muerta y el
    /// <c>ORDER BY</c> no llegaría a traducirse nunca — un <c>?sort=</c> roto saldría verde.
    /// </para>
    /// </remarks>
    [Fact]
    public void Todo_orden_del_listado_se_traduce_en_los_tres_modulos()
    {
        List<string> rotos = [];
        int ejercidas = 0;

        foreach (Proyeccion proyeccion in Declaradas())
        {
            foreach (CriterioDeLoBloqueado criterio in Criterios())
            {
                ejercidas++;

                try
                {
                    ConsultasDeLoBloqueado.Consulta(proyeccion.Abrir(), criterio).ToQueryString();
                }
                catch (InvalidOperationException fallo)
                {
                    rotos.Add($"{proyeccion.Modulo} ?sort={Etiqueta(criterio)}: {fallo.Message}");
                }
            }
        }

        // El ancla: sin esta afirmación, un `Declaradas()` vacío o una lista de criterios vacía
        // dejarían `rotos` vacío y este caso saldría verde sin haber traducido nada.
        ejercidas.ShouldBe(
            Declaradas().Count * (ComposicionDeLoBloqueado.CamposOrdenables.Count * 2) + Declaradas().Count,
            "el barrido no ha ejercido el producto entero de proyecciones por órdenes");

        rotos.ShouldBeEmpty(
            "estas combinaciones del listado del art. 32 no se traducen a SQL, así que son un 500 " +
            "en la pantalla que se abre para rectificar un bloqueo hecho por error:" +
            Environment.NewLine + string.Join(Environment.NewLine, rotos));
    }

    /// <summary>
    /// Y el contraejemplo: una consulta que EF Core no sabe traducir SÍ revienta por aquí.
    /// </summary>
    /// <remarks>
    /// Sin esto, el caso de arriba sería una afirmación que no puede fallar: si
    /// <c>ToQueryString()</c> dejara de traducir de verdad —porque cambiara de comportamiento, o
    /// porque el modelo se construyera vacío—, no lanzaría nada y las cuarenta y dos combinaciones
    /// saldrían verdes sin haber comprobado nada. Es la misma pareja de afirmaciones que
    /// <c>LasCapasVanHaciaDentroTests</c>: la prohibición y su contraejemplo, o ninguna de las dos
    /// vale.
    /// </remarks>
    [Fact]
    public void La_comprobacion_puede_dispararse()
    {
        using OrganizacionDbContext contexto = new(
            Opciones<OrganizacionDbContext>(OrganizacionDbContext.Configurar),
            new InquilinoDeMentira(),
            new AmbitoAbierto());

        // `Nif` es un objeto de valor con conversor: comparar su envoltorio con una cadena no se
        // traduce, y EF Core lo dice al generar el SQL, no al compilar.
        Should.Throw<InvalidOperationException>(
            () => contexto.Empresas
                .Where(empresa => empresa.Nif.Valor == "esto no es un identificador")
                .ToQueryString());
    }

    /// <summary>El orden de omisión y cada campo ordenable en los dos sentidos, con filtro.</summary>
    private static IEnumerable<CriterioDeLoBloqueado> Criterios()
    {
        const int Cuantos = 40;
        const string Filtro = "texto de prueba";

        yield return new CriterioDeLoBloqueado(
            Filtro,
            ComposicionDeLoBloqueado.CampoPorOmision,
            ComposicionDeLoBloqueado.DescendentePorOmision,
            Cuantos);

        foreach (string campo in ComposicionDeLoBloqueado.CamposOrdenables.Order(StringComparer.Ordinal))
        {
            foreach (bool descendente in new[] { false, true })
            {
                yield return new CriterioDeLoBloqueado(Filtro, campo, descendente, Cuantos);
            }
        }
    }

    private static string Etiqueta(CriterioDeLoBloqueado criterio) =>
        (criterio.Descendente ? "-" : string.Empty) + criterio.Campo;

    private static DbContextOptions<T> Opciones<T>(Action<DbContextOptionsBuilder, string> configurar)
        where T : DbContext
    {
        DbContextOptionsBuilder<T> opciones = new();
        configurar(opciones, HaciaNingunSitio);

        return opciones.Options;
    }

    /// <summary>Un módulo y cómo se le pide su proyección de lo bloqueado.</summary>
    private sealed record Proyeccion(string Modulo, Func<IQueryable<RecursoBloqueado>> Abrir);

    private sealed class InquilinoDeMentira : IInquilinoActual
    {
        private static readonly Guid s_empresa = Guid.CreateVersion7();

        public Guid? EmpresaDelFiltro => s_empresa;

        public bool HayEmpresaActiva => true;

        public MotivoSinInquilino? MotivoDelAmbito => null;

        public IDisposable SinInquilino(MotivoSinInquilino motivo) => throw new NotSupportedException(
            "Aquí no se suspende el inquilinato: lo que se prueba es que el filtro se TRADUCE.");
    }

    /// <summary>
    /// El ámbito del art. 32, <b>abierto</b>, que es la diferencia con el barrido de Organización.
    /// </summary>
    /// <remarks>
    /// Cerrado, el filtro global de bloqueo entra en la consulta y el SQL que se traduce no es el
    /// del listado del art. 32: es el de cualquier otro. Abierto, se traduce la forma que la
    /// pantalla ejecuta de verdad, que es la que tiene que traducirse.
    /// </remarks>
    private sealed class AmbitoAbierto : IAccesoALoBloqueado
    {
        public bool Abierto => true;

        public MotivoParaVerLoBloqueado? MotivoDelAmbito =>
            MotivoParaVerLoBloqueado.AccesoReservadoDelArticulo32;

        public IDisposable ViendoLoBloqueado(MotivoParaVerLoBloqueado motivo) =>
            throw new NotSupportedException(
                "Aquí el ámbito ya está abierto: lo que se prueba es que su consulta se TRADUCE.");
    }
}
