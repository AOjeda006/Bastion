using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Bloqueos;
using Bastion.BuildingBlocks.Domain.Direcciones;
using Bastion.BuildingBlocks.Domain.Identificacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Persistencia;

/// <summary>
/// La respuesta a la pregunta del artículo 32 para Catálogo, comprobada en vez de supuesta: no hay
/// aquí un dato de nadie, y por eso ni el artículo ni la categoría se bloquean.
/// </summary>
/// <remarks>
/// <para>
/// <b>La pregunta se hace, no se supone.</b> A cada agregado nuevo hay que contestarle si es
/// bloqueable, y en el ítem 1.7 se le contestó a <c>Impuesto</c>, a <c>Divisa</c> y a
/// <c>UnidadMedida</c>: no lo son, porque el art. 32 de la LOPDGDD obliga a <i>reservar los datos
/// personales</i> y un kilo no tiene ninguno. Aquí la respuesta es la misma y el motivo también,
/// pero la respuesta que vale es la que alguien puede volver a comprobar dentro de dos fases.
/// </para>
/// <para>
/// <b>Por qué la respuesta puede caducar.</b> «Un artículo no tiene datos personales» es cierto de
/// la ficha de hoy, no del concepto: cuélgale un responsable de compras, el contacto del
/// fabricante o el comercial que lo trae, y deja de serlo. Ese día esto se pone rojo y hay que
/// volver a contestar la pregunta — que es el único momento en el que la respuesta importa.
/// </para>
/// <para>
/// <b>Y si la respuesta cambiara, no bastaría con marcar la clase.</b> Poner <c>IBloqueable</c> en
/// <c>Articulo</c> pone rojo por su cuenta a <c>LoBloqueadoSeVeEnteroTests</c>, que compara lo
/// bloqueable del dominio contra <c>TipoDeRecursoBloqueado</c>: el enumerado se quedaría corto,
/// haría falta el puerto del art. 32 en la infraestructura de Catálogo, y el listado nominativo
/// del ADR-0027 tendría que enseñarlo. Esa cadena de rojos es la que convierte «lo marco y ya» en
/// una decisión con trabajo detrás.
/// </para>
/// </remarks>
public sealed class ElCatalogoNoGuardaDatosDeNadieTests : IDisposable
{
    /// <summary>El esquema por cuyas entidades se pregunta.</summary>
    private const string EsquemaDeCatalogo = "catalogo";

    /// <summary>
    /// Los objetos de valor que <b>son</b> un dato de una persona identificada o identificable.
    /// </summary>
    /// <remarks>
    /// Se pregunta por TIPO y no solo por nombre porque el tipo es lo que no se puede maquillar:
    /// una columna llamada <c>referencia</c> de tipo <see cref="Nif"/> sigue siendo un
    /// identificador fiscal. El <see cref="Iban"/> entra por lo mismo: el de un autónomo es un
    /// dato personal, y quien lo mira no sabe si el titular es una sociedad o una persona.
    /// </remarks>
    private static readonly Type[] s_tiposDeDatoPersonal =
    [
        typeof(Nif),
        typeof(Correo),
        typeof(Iban),
        typeof(Direccion),
    ];

    /// <summary>
    /// Trozos de nombre que delatan un dato de una persona aunque el tipo sea un <c>string</c>.
    /// </summary>
    /// <remarks>
    /// La otra mitad, y hace falta: el caso que de verdad va a pasar no es que alguien escriba
    /// <c>Nif</c>, es que alguien añada <c>ContactoDelFabricante</c> como texto libre. Ninguno de
    /// estos trozos casa con lo que Catálogo tiene hoy —<c>Codigo</c>, <c>Descripcion</c>,
    /// <c>Nombre</c> de la categoría—, y <c>Nombre</c> a secas se queda fuera a propósito: el
    /// nombre de una rama de clasificación no es el de nadie.
    /// </remarks>
    private static readonly string[] s_nombresQueDelatan =
    [
        "apellido",
        "cif",
        "contacto",
        "correo",
        "direccion",
        "domicilio",
        "email",
        "iban",
        "movil",
        "nie",
        "nif",
        "persona",
        "razonsocial",
        "responsable",
        "telefono",
    ];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_entidad_de_catalogo_es_bloqueable()
    {
        List<string> bloqueables = [.. EntidadesDeCatalogo()
            .Where(tipo => typeof(IBloqueable).IsAssignableFrom(tipo.ClrType))
            .Select(tipo => tipo.ShortName())];

        bloqueables.ShouldBeEmpty(
            "hay entidades de Catálogo marcadas como bloqueables. Si es que ahora guardan datos " +
            "de alguien, la respuesta del art. 32 para este módulo ha cambiado y hay que " +
            "escribirla entera: el valor en TipoDeRecursoBloqueado, el puerto de lo bloqueado en " +
            "su infraestructura y la ruta del ADR-0027. Si no los guardan, la marca sobra: " +
            string.Join(", ", bloqueables));
    }

    [Fact]
    public void Ninguna_propiedad_de_catalogo_guarda_un_dato_de_una_persona()
    {
        List<string> personales = [.. Sospechosas(EntidadesDeCatalogo())];
        personales.Sort(StringComparer.Ordinal);

        // Este es el hecho del que cuelga la respuesta de arriba, y por eso son dos casos y no
        // uno: «no es bloqueable» sin esto sería una afirmación sobre una marca —trivial de
        // cumplir borrando la marca— en vez de sobre lo que hay guardado.
        personales.ShouldBeEmpty(
            "Catálogo ha empezado a guardar datos de personas, así que la respuesta que este " +
            "fichero da al art. 32 ya no vale y hay que volver a contestarla:" +
            Environment.NewLine + string.Join(Environment.NewLine, personales));
    }

    /// <summary>
    /// El arnés: que haya entidades de Catálogo que mirar, y que el detector detecte de verdad.
    /// </summary>
    /// <remarks>
    /// Un silencio solo vale si quien calla sabe hablar. Las dos reglas de arriba salen verdes de
    /// dos maneras que no tienen ningún otro síntoma: si el filtro por esquema deja de encontrar
    /// entidades de Catálogo —renombrar el esquema basta—, y si el detector de datos personales no
    /// reconoce ninguno. Lo segundo se comprueba apuntándolo al modelo <b>entero</b>, donde sí los
    /// hay: Terceros guarda un NIF, un correo y un domicilio, y si esta lista sale vacía es que el
    /// detector no funciona y el verde de arriba no dice nada.
    /// </remarks>
    [Fact]
    public void El_barrido_ve_catalogo_y_sabe_reconocer_un_dato_personal()
    {
        List<string> deCatalogo = [.. EntidadesDeCatalogo().Select(tipo => tipo.ShortName())];
        deCatalogo.Sort(StringComparer.Ordinal);

        deCatalogo.ShouldBe(
            ["Articulo", "Categoria"],
            "las entidades propias de Catálogo son esas dos. Si aparece una más, hay que " +
            "contestarle la pregunta del art. 32 también a ella; si falta alguna, el filtro por " +
            "esquema ha dejado de encontrarlas y las dos reglas de arriba están mirando al vacío");

        List<string> enTodoElSistema = [.. Sospechosas(Todas())];

        enTodoElSistema.ShouldNotBeEmpty(
            "el detector de datos personales no encuentra ninguno en el sistema entero, cuando " +
            "un tercero guarda identificación fiscal, correo y domicilio. Está roto, y con él " +
            "el verde de Ninguna_propiedad_de_catalogo_guarda_un_dato_de_una_persona");
    }

    /// <summary>Las propiedades que son un dato personal, por tipo o por nombre.</summary>
    private static IEnumerable<string> Sospechosas(IEnumerable<IEntityType> entidades) =>
        from tipo in entidades
        from propiedad in tipo.GetProperties()
        let cual = $"{tipo.ShortName()}.{propiedad.Name}"
        where s_tiposDeDatoPersonal.Contains(
                  Nullable.GetUnderlyingType(propiedad.ClrType) ?? propiedad.ClrType)
           || s_nombresQueDelatan.Any(trozo =>
                  propiedad.Name.Contains(trozo, StringComparison.OrdinalIgnoreCase))
        select cual;

    /// <summary>Las entidades que viven en el esquema de Catálogo.</summary>
    /// <remarks>
    /// Por esquema y no por espacio de nombres: las tablas compartidas —la auditoría, la bandeja y
    /// la idempotencia— están mapeadas también en el contexto de Catálogo, y no son suyas. La
    /// auditoría, además, guarda el nombre de quien hizo el cambio: contarla como de Catálogo
    /// pondría esto rojo por un dato que es de Auditoría y que ya tiene su propia decisión escrita.
    /// </remarks>
    private IEnumerable<IEntityType> EntidadesDeCatalogo() =>
        Todas().Where(tipo => string.Equals(
            tipo.GetSchema(), EsquemaDeCatalogo, StringComparison.Ordinal));

    private IEnumerable<IEntityType> Todas() => LosModelosDeCadaModulo.Entidades(_api.Services);
}
