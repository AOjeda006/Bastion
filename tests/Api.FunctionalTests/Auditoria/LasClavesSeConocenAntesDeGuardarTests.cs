using Bastion.Api.FunctionalTests.Persistencia;
using Bastion.Api.FunctionalTests.Salud;
using Bastion.BuildingBlocks.Domain.Entidades;
using Bastion.BuildingBlocks.Infrastructure.Auditoria;
using Bastion.BuildingBlocks.Infrastructure.Concurrencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Shouldly;

namespace Bastion.Api.FunctionalTests.Auditoria;

/// <summary>
/// La premisa de la que depende que el interceptor de auditoría sea de <b>una sola fase</b>, y el
/// inventario cerrado de lo que en este modelo genera el servidor.
/// </summary>
/// <remarks>
/// <para>
/// La receta canónica del interceptor de auditoría en EF Core es de dos fases: recoger en
/// <c>SavingChanges</c>, completar en <c>SavedChanges</c> las claves que ha generado la base y
/// volver a guardar. Existe porque en el caso general la clave de un <c>INSERT</c> no se sabe
/// hasta después de mandarlo.
/// </para>
/// <para>
/// <b>Este fichero se puso rojo en el 0.9, como estaba anunciado</b>, en cuanto el testigo de
/// concurrencia entró en el modelo: <c>xmin</c> lo genera PostgreSQL en cada escritura. La
/// premisa se reenunció —ADR-0015, que sustituye al punto 2 del ADR-0012— y la comprobación se
/// partió en dos, porque una sola habría tenido que aflojarse.
/// </para>
/// <para>
/// <b>Por qué dos y no una más laxa.</b> Pasar de «ninguna propiedad viene del servidor» a
/// «ninguna propiedad AUDITADA viene del servidor» habría dejado de mirar todo lo demás: un
/// <c>DEFAULT gen_random_uuid()</c> en una columna no auditada habría entrado sin que nadie se
/// enterase. Las dos de aquí, juntas, afirman al menos tanto como la de antes: la primera cubre
/// exactamente lo que el interceptor necesita, y la segunda enumera <b>por nombre</b> lo único
/// que puede venir del servidor. Una forma nueva de generar valor —una sexta, o una séptima—
/// aparece en la lista real, no está en la declarada, y esto se pone rojo igual que antes.
/// </para>
/// <para>
/// Si se pone rojo, <b>no se añade a la lista sin más</b>: se mira si la premisa del interceptor
/// sigue en pie, y si no, se reabre la decisión con un ADR que sustituya al vigente.
/// </para>
/// <para>
/// <b>Se volvió a poner rojo en el 2.7</b>, con la primera columna que calcula el motor: el
/// disponible de una existencia. Se miró la premisa y sigue en pie —la columna no es clave, no se
/// audita y ninguna escritura del rastreador la toca—, y el ADR-0044 enmienda el punto 2 del
/// ADR-0015: lo que genera el servidor son los testigos <b>y</b> las columnas calculadas, cada una
/// en su lista y por su nombre, y cada una comprobada por lo que la hace ser lo que se declara.
/// </para>
/// <para>
/// <b>Y esa columna se fue en el 2.13</b>, con su migración: lo reservado se suma al leer, de las
/// reservas, y el disponible deja de guardarse (ADR-0059, que enmienda el §8 del ADR-0044). La
/// lista de las calculadas se queda vacía, y se queda: la siguiente entra declarándose.
/// </para>
/// </remarks>
public sealed class LasClavesSeConocenAntesDeGuardarTests : IDisposable
{
    // El inventario declarado, ENTERO y por nombre. No es «lo que hay»: es lo que se ha decidido
    // que haya. Por eso se compara la lista completa y no se pregunta si cada una está permitida.
    private static readonly string[] s_generadasPorElServidor =
    [
        // La del ítem 2.3, y es el primer DOCUMENTO de la lista: todas las de arriba y las de
        // abajo son maestros. Lleva testigo porque es un agregado que CAMBIA de estado, y la
        // transición lee y luego escribe: sin él, dos confirmaciones simultáneas del mismo ajuste
        // leerían las dos `Borrador` y escribirían las dos su tanda de movimientos — el stock
        // movido dos veces, sin error y sin rastro. No cuesta una columna: el testigo es `xmin`.
        "Ajuste.Version",

        "Almacen.Version",
        "Articulo.Version",

        // La del ítem 1.10, y lleva testigo por lo mismo que la línea de tarifa: el suministro
        // NO es un hijo del artículo. Tiene su propia ruta —`/articulos/proveedores/{id}`—, su
        // propio `PUT`, su propio `DELETE` y su propio ETag, porque dos personas que corrigen la
        // referencia de dos proveedores distintos del mismo artículo no se están pisando. Con un
        // testigo único en el artículo, la segunda se llevaría un 412 por tocar otra fila.
        "ArticuloProveedor.Version",
        "Categoria.Version",

        // La del ítem 2.10, por la regla uniforme y no por una carrera: la fila del código de
        // barras no cambia nunca, pero su baja exige el `If-Match`, y nadie borra lo que no ha
        // visto (ADR-0051 §6). Sin testigo, la ETag no tendría de dónde salir.
        "CodigoBarras.Version",

        // La del ítem 2.4, y la primera que NO es un recurso de la API: la fila del contador
        // de una serie. Lleva testigo por una razón que no tiene ninguna de las demás -ninguna
        // ruta pide su `ETag`, porque no hay ruta-: es lo que sostiene la carrera
        // suprimir-contra-confirmar. Al salir el contador de `series` (ADR-0039), numerar dejó
        // de mover el `xmin` de la serie, que era lo que hacía fallar el borrado de quien la
        // había leído antes. Ahora quien numera mueve ESTE testigo, y el `DELETE` que el ORM
        // arrastra al borrar la serie lo lleva dentro.
        "ContadorDeSerie.Version",
        "ConversionUM.Version",
        "Divisa.Version",
        "Ejercicio.Version",
        "Empresa.Version",
        "Impuesto.Version",

        // La del ítem 2.12, y la primera línea de un DOCUMENTO que lleva testigo: se cuenta sola,
        // por su ruta y con su `If-Match`, por el argumento de la línea de tarifa. Dos personas que
        // cuentan dos estanterías del mismo almacén no se están pisando (ADR-0055 §4).
        "LineaDeRecuento.Version",

        // Las dos del ítem 1.9, y las dos llevan testigo a propósito: la línea de tarifa NO es un
        // hijo del agregado como lo son el contacto o la cuenta bancaria. Tiene su propia ruta,
        // su propio `PUT` y su propio ETag, porque una tabla de precios se mantiene línea a línea
        // —dos personas cambiando el precio de dos artículos distintos de la misma tarifa no se
        // están pisando— y un testigo único en la tarifa haría que la segunda se llevara un 412
        // por tocar otra fila. Es la decisión contraria a la de los tres hijos del tercero, y lo
        // que la invierte es que allí el agregado entero es una ficha que se edita de una vez.
        "LineaTarifa.Version",

        // La del ítem 2.12, por lo mismo que los otros dos documentos, y con un trabajo más: toda
        // escritura en una de sus líneas la toca, así que resume el documento entero.
        "Recuento.Version",

        // La del ítem 2.13, y no la pide ninguna carrera: toda escritura sobre una reserva bloquea
        // antes la valoración de su clave, así que dos no se pisan (ADR-0059 §2). La lleva por lo
        // mismo que cualquier cosa que se modifica, y es el seguro del día que alguien la escriba
        // sin ese cerrojo (ADR-0059 §10).
        "Reserva.Version",
        "Rol.Version",
        "Serie.Version",
        "Tarifa.Version",
        "Tercero.Version",
        "TipoCambio.Version",

        // La del ítem 2.11, el segundo documento, y lleva testigo por lo mismo que el ajuste con
        // una transición más: enviar, recibir y anular leen y luego escriben. Recibir y anular la
        // misma transferencia a la vez leerían las dos `Enviada`, y sin testigo las dos restarían
        // el tránsito.
        "Transferencia.Version",
        "Ubicacion.Version",
        "UnidadMedida.Version",
        "Usuario.Version",
    ];

    // Heredan del tipo base y NO llevan testigo de concurrencia, a propósito y con su motivo.
    // Los tres son hijos del agregado del tercero: no son recursos que se editen por su cuenta,
    // así que el testigo que gobierna su edición es el del TERCERO —que es el que la ruta exige
    // en el cuerpo—. Un testigo por hijo dejaría pasar el caso que de verdad hay que detectar:
    // dos ediciones simultáneas de la MISMA ficha, una que cambia la razón social y otra que
    // cuelga un contacto. El motivo largo está en `ConfiguracionDeContacto`.
    //
    // Las dos del ítem 2.3 amplían la lista con un motivo distinto del de los tres hijos del
    // tercero, y por eso llevan el suyo. `LineaDeAjuste` sí es de esa familia: cuelga del ajuste,
    // no tiene ruta propia y lo que gobierna su edición es el testigo del documento. Y
    // `LineaDeTransferencia`, del 2.11, por lo mismo con la transferencia. Y `ConsumoDeReserva`,
    // del 2.13, por lo mismo con la reserva: nace al consumir y no cambia nunca (ADR-0059 §10).
    // `MovimientoStock` no: no lleva testigo porque NO SE MODIFICA NUNCA, ni por una ruta ni por
    // ninguna otra vía —la tabla rechaza `UPDATE` en el motor—, y un testigo de concurrencia
    // sobre una fila que nadie puede escribir dos veces no protege de nada. Es el caso que este
    // mismo fichero anunciaba: «podría haber una entidad de solo-inserción con marcas de tiempo
    // y sin testigo».
    private static readonly string[] s_delTipoBaseSinTestigo =
    [
        "CondicionPago",
        "ConsumoDeReserva",
        "Contacto",
        "CuentaBancaria",
        "LineaDeAjuste",
        "LineaDeTransferencia",
        "MovimientoStock",
    ];

    // Llevan testigo y NO heredan del tipo base, a propósito y con su motivo. Es la divergencia
    // contraria a la de `s_delTipoBaseSinTestigo`, y hasta el ítem 2.4 no existía ninguna.
    //
    // `ContadorDeSerie` no hereda porque no puede sostener lo que el tipo base promete:
    // `ModificadoEn` la pone el interceptor de marcas de tiempo al guardar, y a esta fila no la
    // guarda nadie por el rastreador -la escribe la sentencia de numeración en crudo, que es la
    // única forma de tomar el cerrojo-. Heredar dejaría dos columnas congeladas en el instante
    // de crearse mientras el número sube todos los días: una marca que MIENTE es peor que no
    // tenerla, porque se lee igual. Lo que sí lleva es el testigo, porque ese lo mueve
    // PostgreSQL solo y sin pasar por nadie.
    private static readonly string[] s_conTestigoFueraDelTipoBase =
    [
        "ContadorDeSerie",
    ];

    // Las columnas que CALCULA EL MOTOR, y es la segunda cosa que el servidor genera (ADR-0044,
    // que enmienda el punto 2 del ADR-0015). Van en una lista aparte y no en la de los testigos
    // porque son otra cosa y se comprueban por otra cosa: una calculada no se mete en el `WHERE`
    // de nadie, y un testigo no se calcula de otras columnas.
    //
    // HOY ESTÁ VACÍA, y se queda (ADR-0059). La primera fue `Existencia.Disponible`, del ítem 2.7:
    // el físico menos lo reservado, guardado por el motor. Se fue en el 2.13 con su columna,
    // porque lo reservado pasó a sumarse al leer, de las reservas. La siguiente calculada entra
    // aquí declarándose, sin reabrir el ADR-0015, si la premisa del interceptor sigue en pie para
    // ella: que no sea clave, que no se audite, y que ninguna escritura del rastreador la toque.
    private static readonly string[] s_calculadasPorElMotor = [];

    private readonly ApiSinDependencias _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public void Ninguna_propiedad_auditada_la_pone_la_base_de_datos()
    {
        List<string> generadas = [.. Modelos().SelectMany(modelo => Donde(modelo, EsAuditadaYDelServidor))];

        generadas.ShouldBeEmpty(
            "si la base genera un valor QUE VA A LA TRAZA, no se conoce hasta después del INSERT " +
            "y el interceptor de auditoría necesita una segunda fase. Reabre el ADR-0015 antes " +
            "de tocar esta lista.");
    }

    [Fact]
    public void Lo_unico_que_genera_el_servidor_es_lo_declarado()
    {
        List<string> generadas = [.. Modelos().SelectMany(modelo => Donde(modelo, EsDelServidor))];

        generadas.Sort(StringComparer.Ordinal);

        List<string> declaradas = [.. s_generadasPorElServidor, .. s_calculadasPorElMotor];

        declaradas.Sort(StringComparer.Ordinal);

        // Las listas ENTERAS y en el mismo orden, no «lo que sobra»: un testigo que DESAPARECE
        // del modelo deja ese recurso sin control de concurrencia, y eso también tiene que verse
        // aquí.
        string.Join(", ", generadas).ShouldBe(
            string.Join(", ", declaradas),
            "el servidor solo genera los testigos de concurrencia del R11 y las columnas " +
            "calculadas declaradas del ADR-0044. Cualquier otra cosa —un DEFAULT, un IDENTITY, " +
            "una calculada sin declarar— vuelve a poner en duda la fase única del interceptor de " +
            "auditoría, y eso se decide en un ADR, no aquí.");
    }

    [Fact]
    public void Cada_cosa_que_genera_el_servidor_es_de_verdad_lo_que_se_declaro()
    {
        // Y no algo que se le PAREZCA. Las listas de arriba se comparan por nombre, así que una
        // propiedad llamada `Version` con un DEFAULT en la base pasaría por testigo sin serlo:
        // aquí se comprueba que cada una lo es por lo que la hace serlo. Un testigo es uint,
        // generado en cada escritura y marcado como testigo, que es lo que mete el valor en el
        // WHERE del UPDATE. Una calculada lleva su expresión, GUARDADA —no al vuelo: así vale
        // igual para el ORM, para una consulta cruda y para un informe— y nada más: ni un
        // DEFAULT, ni un IDENTITY, ni el papel de testigo.
        List<string> impostoras = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => EsDelServidor(par.Propiedad))
                .Select(par => (Nombre: $"{tipo.ShortName()}.{par.Camino}", par.Propiedad)))
            .Where(generada => !(s_calculadasPorElMotor.Contains(generada.Nombre, StringComparer.Ordinal)
                    ? EsUnaCalculadaGuardada(generada.Propiedad)
                    : generada.Propiedad.EsElTestigo()))
            .Select(generada => generada.Nombre)];

        impostoras.ShouldBeEmpty(
            "esto lo genera el servidor y no es lo que su lista dice: ni el testigo de " +
            "concurrencia, ni una columna calculada y guardada");
    }

    [Fact]
    public void Las_entidades_del_tipo_base_y_las_que_llevan_testigo_son_las_MISMAS()
    {
        // El ítem 0.10 extrajo `EntidadBase`, y con él las dos preguntas se juntaron: hoy las seis
        // entidades que heredan del tipo base son exactamente las seis que llevan testigo. Que
        // coincidan no es casualidad —son las que se modifican, y lo que se modifica necesita
        // saber cuándo y contra qué versión— pero tampoco es una ley: podría haber una entidad de
        // solo-inserción con marcas de tiempo y sin testigo.
        //
        // Por eso esto no PROHÍBE la divergencia, la hace visible. Si un día una entidad hereda del
        // base y no lleva testigo, este caso se pone rojo y hay que decir por qué en el ADR-0015,
        // que es donde vive la lista. Sin este caso, la entidad nueva entraría sin control de
        // concurrencia y sin que nada lo dijera.
        List<string> delTipoBase = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .Where(tipo => typeof(EntidadBase).IsAssignableFrom(tipo.ClrType))
            .Select(tipo => tipo.ShortName())];

        List<string> conTestigo = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .Where(tipo => tipo.GetProperties().Any(propiedad => propiedad.EsElTestigo()))
            .Select(tipo => tipo.ShortName())];

        delTipoBase.Sort(StringComparer.Ordinal);
        conTestigo.Sort(StringComparer.Ordinal);

        // Las que llevan testigo SIN heredar del tipo base se descuentan, porque no tienen por
        // qué aparecer en `delTipoBase`; y se descuentan de una lista declarada, no con un
        // filtro que las adivine, para que la siguiente que aparezca siga poniendo esto rojo.
        List<string> esperadas =
        [
            .. conTestigo.Where(nombre => !s_conTestigoFueraDelTipoBase.Contains(nombre, StringComparer.Ordinal)),
            .. s_delTipoBaseSinTestigo,
        ];
        esperadas.Sort(StringComparer.Ordinal);

        // La divergencia que este caso anunciaba llevó hasta el ítem 1.6 en ponerse: los tres
        // hijos del tercero heredan del tipo base y no llevan testigo. No se permite en blanco,
        // se DECLARA —arriba, con su motivo—, que es lo que este caso pedía que se hiciera.
        string.Join(", ", delTipoBase).ShouldBe(
            string.Join(", ", esperadas),
            "una entidad hereda del tipo base y no lleva testigo sin estar declarada arriba, o al " +
            "revés. Si es nueva: o le pones testigo, o dices por qué no lo lleva");

        string.Join(", ", conTestigo).ShouldBe(
            string.Join(", ", s_generadasPorElServidor.Select(nombre => nombre.Split('.')[0])),
            "las entidades con testigo son las del ADR-0015, ni una más ni una menos");
    }

    /// <summary>
    /// Ninguna clave se genera al insertar: <b>todas</b> vienen puestas de la fábrica del dominio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Esta regla existía y daba por buena justamente la configuración que falló.</b> Miraba
    /// esta misma propiedad y eximia en bloque a toda clave <c>Guid</c>, con el motivo escrito de
    /// que <c>ValueGenerated.OnAdd</c> en un <c>Guid</c> no significa que la ponga la base. Eso es
    /// cierto <b>del INSERT</b>, y es todo lo que la regla miraba. Pero <c>OnAdd</c> es también lo
    /// que EF Core consulta para decidir algo muy distinto: si una entidad recién aparecida en la
    /// colección de un padre ya seguido es un ALTA o una fila que ya existía. Con la clave puesta
    /// por el dominio, la respuesta es siempre «ya existía», sale un <c>UPDATE</c> contra una fila
    /// que no está, y de ahí un <b>412</b> sobre un alta correcta. El ítem 1.6 lo cobró en siete
    /// casos del carril de integración.
    /// </para>
    /// <para>
    /// Por eso ya no se exime nada: se exige la declaración <b>positiva</b>, que es la verdad del
    /// sistema —aquí la clave la pone siempre el dominio— y la que apaga el otro uso de
    /// <c>OnAdd</c>. La pone la convención <c>LaClaveLaPoneElDominio</c>, y el efecto sobre el
    /// seguidor de cambios lo ejerce <c>LoQueCuelgaNaceComoAltaTests</c>, sin contenedor.
    /// </para>
    /// </remarks>
    [Fact]
    public void Toda_entidad_tiene_su_clave_completa_antes_de_guardar()
    {
        List<string> generadasAlInsertar = [.. Modelos()
            .SelectMany(modelo => modelo.GetEntityTypes())
            .SelectMany(tipo => (tipo.FindPrimaryKey()?.Properties ?? [])
                .Where(clave => clave.ValueGenerated != ValueGenerated.Never)
                .Select(clave =>
                    $"{tipo.ShortName()}.{clave.Name} ({clave.ClrType.Name}, {clave.ValueGenerated})"))];

        generadasAlInsertar.ShouldBeEmpty(
            "ninguna clave de este sistema se genera al insertar: la pone la fábrica del dominio " +
            "antes de que EF vea nada. Una clave `OnAdd` no solo afecta al INSERT —ahí EF respeta " +
            "el valor que llega—: es lo que EF mira para decidir si un hijo que aparece en la " +
            "colección de un padre ya seguido es un ALTA o una fila que ya existía. Con la clave " +
            "siempre puesta, decide «ya existía», emite un UPDATE contra una fila que no está y " +
            "eso acaba en un 412 sobre un alta correcta. Lo declara la convención " +
            "`LaClaveLaPoneElDominio`; si algo se le escapa, aparece aquí.");
    }

    /// <summary>
    /// El universo de modelos es el declarado, entero.
    /// </summary>
    /// <remarks>
    /// Sin esto, todas las reglas de este fichero recorren la lista que el descubrimiento
    /// encuentre, y una lista que se queda corta —o vacía— las deja a todas en verde sin haber
    /// mirado nada. Es exactamente lo que pasó hasta el 1.6: Terceros entró en la fase 1 y no
    /// estaba en ninguna de las listas escritas a mano.
    /// </remarks>
    [Fact]
    public void El_universo_de_modelos_es_el_declarado()
    {
        List<string> encontrados = [.. LosModelosDeCadaModulo.Contextos().Select(tipo => tipo.Name)];
        encontrados.Sort(StringComparer.Ordinal);

        string.Join(", ", encontrados).ShouldBe(
            string.Join(", ", LosModelosDeCadaModulo.Declarados),
            "los contextos de este sistema son los declarados, ni uno más ni uno menos. Uno de " +
            "más: añádelo a la lista y comprueba que las reglas de modelo le valen. Uno de menos: " +
            "el descubrimiento se ha quedado corto y todas las reglas de este fichero están " +
            "diciendo «cada entidad declara…» de menos módulos de los que hay.");

        Modelos().Count().ShouldBe(
            LosModelosDeCadaModulo.Declarados.Length,
            "hay un contexto descubierto que el contenedor no sabe resolver");
    }

    // Con camino, para que una propiedad de un tipo complejo no se escape: `GetProperties()` no
    // las devuelve, y un DEFAULT puesto ahí dentro se saltaría este inventario entero.
    private static IEnumerable<string> Donde(IModel modelo, Func<IReadOnlyProperty, bool> condicion) =>
        modelo.GetEntityTypes()
            .SelectMany(tipo => tipo.PropiedadesConCamino()
                .Where(par => condicion(par.Propiedad))
                .Select(par => $"{tipo.ShortName()}.{par.Camino}"));

    private static bool EsUnaCalculadaGuardada(IReadOnlyProperty propiedad) =>
        propiedad.GetComputedColumnSql() is not null
        && propiedad.GetIsStored() == true
        && propiedad.GetDefaultValueSql() is null
        && propiedad.GetValueGenerationStrategy() == NpgsqlValueGenerationStrategy.None
        && !propiedad.IsConcurrencyToken;

    private static bool EsAuditadaYDelServidor(IReadOnlyProperty propiedad) =>
        propiedad.Auditoria().Que == ClasificacionDeAuditoria.Auditada && EsDelServidor(propiedad);

    // Las cinco formas que tiene un valor de venir del servidor, cada una con su nombre: un
    // DEFAULT, una columna calculada, una columna IDENTITY o serial —esta es de Npgsql, y es la
    // que de verdad distingue «la pone la base» de «la pone el cliente al insertar»—, algo que se
    // regenera en cada UPDATE (la forma de un `rowversion`), y el testigo de concurrencia, que
    // en PostgreSQL es `xmin`.
    private static bool EsDelServidor(IReadOnlyProperty propiedad) =>
        propiedad.GetDefaultValueSql() is not null
        || propiedad.GetComputedColumnSql() is not null
        || propiedad.GetValueGenerationStrategy() != NpgsqlValueGenerationStrategy.None
        || propiedad.ValueGenerated == ValueGenerated.OnAddOrUpdate
        || propiedad.IsConcurrencyToken;

    private IEnumerable<IModel> Modelos() => LosModelosDeCadaModulo.De(_api.Services);
}
