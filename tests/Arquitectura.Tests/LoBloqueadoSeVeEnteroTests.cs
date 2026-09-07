using System.Reflection;
using Bastion.BuildingBlocks.Application.Bloqueos;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El listado del artículo 32 tiene que ver <b>todo</b> lo que se bloquea, no una parte.
/// </summary>
/// <remarks>
/// <para>
/// <b>La doctrina estaba escrita y el barrido no existía.</b> El comentario de
/// <see cref="IBloqueable"/> lo dice con todas las letras desde el día que se escribió: «es la
/// marca que hace de lista […] sin ella, "cuáles se bloquean" sería una lista escrita en un test,
/// que es una lista que se queda vieja». <see cref="TipoDeRecursoBloqueado"/> es exactamente esa
/// lista, y nadie la comparaba contra la marca. Se quedó vieja dos veces seguidas sin que nada se
/// pusiera rojo: <c>Usuario</c> desde el 1.4, <c>Tercero</c> desde el 1.5.
/// </para>
/// <para>
/// <b>Por qué importa más que una lista desincronizada.</b> Lo que el listado no enseña no es un
/// dato cualquiera: es el registro de qué se ha reservado en vez de borrarse, que el art. 32 del
/// RGPD y el 32 de la LOPDGDD obligan a poder consultar. Un usuario bloqueado es una
/// <b>persona física</b> —el caso más nítido del artículo— y no asomaba por ningún sitio. El
/// defecto no es de código bonito: es de cumplimiento.
/// </para>
/// <para>
/// <b>Y no era un olvido, era estructural.</b> El enumerado vive en <c>Organizacion.Application</c>
/// y el repositorio que lo sirve unía tres tablas del esquema <c>organizacion</c>. Una obligación
/// <b>transversal</b> montada dentro de un módulo no alcanza a los demás por construcción, así que
/// añadir dos valores al enumerado no habría bastado: hacía falta que cada módulo que bloquea
/// contestara por su cuenta, que es lo que hace el puerto del ADR-0024 y el §4.
/// </para>
/// <para>
/// <b>Las dos direcciones, y no es simetría por gusto.</b> Que todo lo bloqueable esté en la lista
/// caza el olvido de hoy; que ningún valor de la lista sobre caza el de mañana, cuando un agregado
/// deje de ser bloqueable y su valor se quede publicado en el contrato de la API apuntando a nada.
/// </para>
/// </remarks>
public sealed class LoBloqueadoSeVeEnteroTests
{
    /// <summary>
    /// Los agregados que implementan <see cref="IBloqueable"/> en el dominio compilado, por nombre.
    /// </summary>
    /// <remarks>
    /// Sale del dominio y no de una lista porque la lista es justo lo que se está comprobando. Se
    /// descubre sobre los ensamblados <c>Domain</c> que el inventario declara con tipos, que es el
    /// alcance que ya usan las demás reglas de este carril.
    /// </remarks>
    private static IReadOnlyList<string> Bloqueables() =>
    [
        .. from ensamblado in Ensamblados.ConTipos("Domain")
           from tipo in ensamblado.GetTypes()
           where tipo is { IsAbstract: false, IsInterface: false }
              && typeof(IBloqueable).IsAssignableFrom(tipo)
           orderby tipo.Name, StringComparer.Ordinal
           select tipo.Name,
    ];

    /// <summary>Los valores de la lista cerrada, por nombre.</summary>
    private static IReadOnlyList<string> Declarados() =>
    [
        .. Enum.GetNames<TipoDeRecursoBloqueado>().Order(StringComparer.Ordinal),
    ];

    /// <summary>Los módulos cuyo dominio tiene al menos un agregado bloqueable.</summary>
    /// <remarks>
    /// El nombre del módulo sale del ensamblado —<c>Modulo.Capa</c>— y no de una lista: es el
    /// mismo descubrimiento que usa el resto del carril, así que un módulo nuevo entra solo.
    /// </remarks>
    private static IReadOnlyList<string> ModulosQueBloquean() =>
    [
        .. from ensamblado in Ensamblados.ConTipos("Domain")
           where ensamblado.GetTypes().Any(tipo =>
               tipo is { IsAbstract: false, IsInterface: false }
               && typeof(IBloqueable).IsAssignableFrom(tipo))
           orderby Modulo(ensamblado), StringComparer.Ordinal
           select Modulo(ensamblado),
    ];

    /// <summary>Los módulos cuya infraestructura implementa el puerto del art. 32.</summary>
    private static IReadOnlyList<string> ModulosConPuerto() =>
    [
        .. from ensamblado in Ensamblados.ConTipos("Infrastructure")
           where ensamblado.GetTypes().Any(tipo =>
               tipo is { IsAbstract: false, IsInterface: false }
               && typeof(IConsultaDeLoBloqueado).IsAssignableFrom(tipo))
           orderby Modulo(ensamblado), StringComparer.Ordinal
           select Modulo(ensamblado),
    ];

    /// <summary><c>Bastion.Terceros.Infrastructure</c> -&gt; <c>Terceros</c>.</summary>
    private static string Modulo(Assembly ensamblado) =>
        ensamblado.GetName().Name!.Split('.')[1];

    /// <summary>
    /// El ancla: si el descubrimiento no encuentra agregados bloqueables, las dos comparaciones de
    /// abajo se cumplen en vacío y este fichero entero deja de significar nada.
    /// </summary>
    /// <remarks>
    /// No es una formalidad. La consulta atraviesa ensamblados por reflexión, y basta con que
    /// <see cref="IBloqueable"/> cambie de sitio, o con que el inventario deje de declarar los
    /// <c>Domain</c> con tipos, para que devuelva la lista vacía — y una lista vacía está contenida
    /// en cualquier otra, así que las dos reglas siguientes saldrían <b>verdes</b>.
    /// </remarks>
    [Fact]
    public void El_barrido_ve_los_agregados_que_se_bloquean()
    {
        IReadOnlyList<string> encontrados = Bloqueables();

        encontrados.ShouldNotBeEmpty(
            "el descubrimiento no ha encontrado ni un agregado bloqueable, así que las dos reglas " +
            "de este fichero se están cumpliendo en vacío. Mira si `IBloqueable` sigue donde " +
            "estaba y si el inventario declara los ensamblados de dominio con tipos");

        // Y que vea MÁS DE UNO, en más de un módulo: un descubrimiento que solo alcanzara el
        // ensamblado propio también devolvería algo, y también sería un falso verde.
        encontrados.Count.ShouldBeGreaterThanOrEqualTo(
            2,
            $"el barrido solo ve {encontrados.Count} agregado bloqueable, y hay más de un módulo " +
            $"que bloquea. Lo encontrado: {string.Join(", ", encontrados)}");
    }

    /// <summary>
    /// Todo agregado que se puede bloquear tiene su valor en la lista que el art. 32 publica.
    /// </summary>
    [Fact]
    public void Todo_agregado_bloqueable_esta_en_la_lista_del_articulo_32()
    {
        IReadOnlyList<string> sinSitio =
        [
            .. from nombre in Bloqueables()
               where !Declarados().Contains(nombre, StringComparer.Ordinal)
               orderby nombre, StringComparer.Ordinal
               select nombre,
        ];

        sinSitio.ShouldBeEmpty(
            "estos agregados se pueden bloquear y NO aparecen en `TipoDeRecursoBloqueado`, así " +
            "que lo que se les reserva no sale en el listado del art. 32 y nadie puede " +
            "consultarlo. No basta con añadir el valor: el enumerado vive en un módulo y la " +
            "obligación es transversal, así que el módulo dueño tiene que exponer su puerto:" +
            Environment.NewLine + "· " + string.Join(Environment.NewLine + "· ", sinSitio));
    }

    /// <summary>
    /// Y ningún valor de la lista nombra algo que ya no se bloquea.
    /// </summary>
    /// <remarks>
    /// La mitad de mañana. Sin ella, el día que un agregado deje de ser bloqueable su valor se
    /// queda publicado en el contrato de la API —donde lo lee un cliente ya desplegado— apuntando
    /// a un tipo de recurso que no existe, y el listado nunca devolvería una fila con ese valor
    /// sin que nada lo dijera.
    /// </remarks>
    [Fact]
    public void Ningun_valor_de_la_lista_nombra_algo_que_ya_no_se_bloquea()
    {
        IReadOnlyList<string> sobran =
        [
            .. from nombre in Declarados()
               where !Bloqueables().Contains(nombre, StringComparer.Ordinal)
               orderby nombre, StringComparer.Ordinal
               select nombre,
        ];

        sobran.ShouldBeEmpty(
            "`TipoDeRecursoBloqueado` declara estos valores y ningún agregado del dominio los " +
            "corresponde. Un valor de más se publica en el contrato de la API y no lo devuelve " +
            "nunca nadie:" +
            Environment.NewLine + "· " + string.Join(Environment.NewLine + "· ", sobran));
    }

    /// <summary>
    /// Y todo módulo que bloquea expone su puerto, que es la otra mitad del arreglo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin esta regla, la de arriba se puede cumplir sin arreglar nada.</b> El enumerado es una
    /// lista de nombres: añadirle <c>Usuario</c> la pone verde en el acto y el listado sigue sin
    /// enseñar ni un usuario bloqueado, porque las filas las trae quien implementa el puerto. Un
    /// valor declarado sin módulo que lo conteste es <b>peor</b> que el agujero de partida: el
    /// contrato de la API promete un tipo de recurso que no aparece nunca, así que quien audite el
    /// listado leerá que no hay usuarios bloqueados en vez de que nadie los está buscando.
    /// </para>
    /// <para>
    /// <b>Y se compara en las dos direcciones.</b> Un módulo con puerto y sin nada bloqueable es
    /// una consulta que devuelve la lista vacía para siempre — un coste por página y una respuesta
    /// que parece un hecho.
    /// </para>
    /// </remarks>
    [Fact]
    public void Todo_modulo_que_bloquea_contesta_por_lo_suyo()
    {
        IReadOnlyList<string> bloquean = ModulosQueBloquean();
        IReadOnlyList<string> conPuerto = ModulosConPuerto();

        // El ancla de esta pareja, y va aquí y no en un caso aparte porque las dos listas se
        // descubren igual: si las dos salieran vacías, las dos comparaciones de abajo pasarían.
        bloquean.ShouldNotBeEmpty(
            "el barrido no ha encontrado NINGÚN módulo con agregados bloqueables, así que las dos " +
            "comparaciones de este caso se cumplen en vacío");

        conPuerto.ShouldNotBeEmpty(
            "el barrido no ha encontrado NINGUNA implementación de `IConsultaDeLoBloqueado`, así " +
            "que el listado del art. 32 no tiene de dónde sacar filas y aun así estaría verde");

        IReadOnlyList<string> mudos =
        [
            .. from modulo in bloquean
               where !conPuerto.Contains(modulo, StringComparer.Ordinal)
               select modulo,
        ];

        mudos.ShouldBeEmpty(
            "estos módulos bloquean agregados y no implementan `IConsultaDeLoBloqueado`, así que " +
            "lo que reservan no llega al listado del art. 32 por mucho que su valor esté en el " +
            "enumerado:" + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", mudos));

        IReadOnlyList<string> deMas =
        [
            .. from modulo in conPuerto
               where !bloquean.Contains(modulo, StringComparer.Ordinal)
               select modulo,
        ];

        deMas.ShouldBeEmpty(
            "estos módulos implementan `IConsultaDeLoBloqueado` y no tienen ningún agregado " +
            "bloqueable, así que su puerto devuelve la lista vacía en cada página y esa respuesta " +
            "no se distingue de «no hay nada bloqueado»:" + Environment.NewLine + "· " +
            string.Join(Environment.NewLine + "· ", deMas));
    }
}
