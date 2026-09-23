using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Inventario.Infrastructure.Persistencia;
using Bastion.Organizacion.Contracts.Ejercicios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// El puerto de ejercicios contestado <b>desde la transacción de Inventario</b>, con el cerrojo
/// compartido puesto en la misma lectura.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no lo contesta Organización.</b> Existe <c>OrganizacionDbContext</c> y sabría leer la
/// fila perfectamente, pero sería <b>otra conexión y otra transacción</b>: el cerrojo se soltaría
/// al acabar esa lectura y no al confirmar el documento, que es justo lo contrario de lo que hace
/// falta. Un cerrojo que no dura hasta el <c>COMMIT</c> del que escribe no serializa nada. Así que
/// la consulta corre sobre el contexto del módulo que confirma, que es el que lleva la transacción.
/// </para>
/// <para>
/// <b>Y por eso es SQL crudo sobre el esquema de otro módulo</b>, que es la misma excepción que se
/// abrió para la numeración y con el mismo criterio: vale cuando el efecto tiene que caer en la
/// MISMA transacción que el documento y no hay forma de pedirlo por el ORM. <c>FOR SHARE</c> no
/// tiene traducción en EF Core. Está en la lista cerrada de
/// <c>ElFiltroNoSeSaltaPorAhiTests</c> con su argumento escrito.
/// </para>
/// <para>
/// <b>El filtro global de empresa no se aplica al SQL crudo, así que la sentencia lo hace ella
/// misma</b>, con el valor que sale de <see cref="IInquilinoActual"/> —el mismo del que lo toma el
/// filtro, nunca de la petición—. No es una imitación del filtro: es la misma comparación, escrita
/// a mano porque aquí no hay traductor. Sin ella, confirmar con una fecha cualquiera leería el
/// ejercicio de otra sociedad.
/// </para>
/// <para>
/// <b>Las cuatro cadenas del esquema van escritas a mano y comparadas contra el modelo.</b> Desde
/// aquí no se ve <c>Ejercicio</c> —es de <c>Organizacion.Domain</c>—, así que la tabla y sus
/// columnas se nombran en crudo; que sigan siendo las del mapeo de verdad lo comprueba
/// <c>LaSentenciaDelEjercicioNombraLaTablaDeVerdadTests</c>, que las lee del modelo de EF Core.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
internal sealed class LosEjerciciosDesdeInventario(
    InventarioDbContext contexto, IInquilinoActual inquilino) : IConsultaDeEjercicios
{
    /// <summary>El esquema de Organización, nombrado aquí porque la sentencia lo escribe en crudo.</summary>
    public const string Esquema = "organizacion";

    /// <summary>La tabla de ejercicios.</summary>
    public const string Tabla = "ejercicios";

    /// <summary>La columna del primer día, incluido.</summary>
    public const string ColumnaDeInicio = "fecha_de_inicio";

    /// <summary>La columna del último día, incluido.</summary>
    public const string ColumnaDeFin = "fecha_de_fin";

    /// <summary>La columna del estado, que se guarda como texto.</summary>
    public const string ColumnaDeEstado = "estado";

    /// <summary>
    /// <b>La lectura que decide</b>: trae el estado del ejercicio que comprende la fecha y deja la
    /// fila bloqueada en compartido hasta el <c>COMMIT</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>FOR SHARE</c> y no <c>FOR UPDATE</c></b>: varias confirmaciones del mismo ejercicio
    /// tienen que poder ir a la vez —son la operación normal, y serializarlas entre sí no protege
    /// de nada—. Lo que no puede ir a la vez es una confirmación y un cierre, y el exclusivo que
    /// toma el cierre no convive con ningún compartido: espera.
    /// </para>
    /// <para>
    /// <b>El estado viaja en la misma fila que el cerrojo.</b> Leer el estado y bloquear después
    /// —o al revés, bloquear y volver a leer— deja entre las dos órdenes exactamente la ventana
    /// que se viene a cerrar.
    /// </para>
    /// <para>
    /// <b>Los dos extremos, incluidos</b>, como el intervalo del ejercicio: el 1 de enero y el 31
    /// de diciembre son días del ejercicio, y un <c>&lt;</c> de más dejaría el último día de cada
    /// año sin ejercicio al que imputarse.
    /// </para>
    /// <para>
    /// <b>Sin punto y coma final</b>: EF Core compone esta cadena dentro de otra sentencia, y un
    /// punto y coma ahí dentro es un error de sintaxis en tiempo de ejecución.
    /// </para>
    /// </remarks>
    public const string SqlDelEstadoConCerrojo =
        "SELECT e." + ColumnaDeEstado + " AS \"Value\"" +
        " FROM " + Esquema + "." + Tabla + " AS e" +
        " WHERE e.empresa_id = {1}" +
        " AND e." + ColumnaDeInicio + " <= {0}" +
        " AND e." + ColumnaDeFin + " >= {0}" +
        " FOR SHARE";

    /// <inheritdoc />
    public async Task<EstadoDelEjercicioParaEscribir> ParaEscribirEnAsync(
        DateOnly fecha, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCION, por lo mismo que el numerador: EF Core abre una
        // transaccion IMPLICITA por cada orden que ejecuta, asi que sin esta comprobacion el
        // `FOR SHARE` se soltaria al acabar esta misma lectura. El estado que devolviera seria
        // cierto en el instante de leerlo y mentira un milisegundo despues, que es peor que no
        // preguntar: parece una guarda y no lo es.
        //
        // Lanza y no devuelve un fallo de negocio (ADR-0004): quien llama sin transaccion no se ha
        // equivocado de datos, esta mal cableado.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que el cerrojo " +
                "sobre la fila del ejercicio se soltaría al acabar esta lectura y un cierre " +
                "podría colarse entre la comprobación y el `COMMIT` del documento. El dueño de la " +
                "transacción es el filtro de idempotencia: la acción que confirma tiene que " +
                "declarar la Idempotency-Key obligatoria.");
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está preguntando por el ejercicio dentro de un ámbito sin inquilino, y un " +
            "ejercicio es siempre de una empresa: sin ella la sentencia leería el de cualquiera.");

        List<string> estados = await contexto.Database
            .SqlQueryRaw<string>(SqlDelEstadoConCerrojo, fecha, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // NINGUNA FILA ES `SinEjercicio`, Y NO UN FALLO. Es el valor propio del enumerado, y el
        // motivo por el que existe: una fecha fuera de todo ejercicio es un desenlace que hay que
        // contestar, no una consulta que salió mal.
        if (estados.Count == 0)
        {
            return EstadoDelEjercicioParaEscribir.SinEjercicio;
        }

        // Y MAS DE UNA TAMPOCO SE ELIGE: la restriccion de exclusion de la base garantiza que los
        // intervalos de una empresa no se solapan, asi que dos filas aqui significan que esa
        // garantia se ha caido. Quedarse con la primera escogeria en silencio a cual de los dos
        // ejercicios se imputa el documento.
        if (estados.Count > 1)
        {
            throw new InvalidOperationException(
                $"La fecha {fecha:O} cae en {estados.Count} ejercicios de la misma empresa, y la " +
                "restricción de exclusión de `organizacion.ejercicios` existe para que eso sea " +
                "imposible. Elegir uno aquí decidiría en silencio a qué periodo se imputa el " +
                "documento.");
        }

        // EL TEXTO SE TRADUCE POR SUS DOS NOMBRES Y NO POR «lo que no sea Abierto es Cerrado».
        // Ese atajo parece seguro -rechaza la escritura- y es el que convierte un estado nuevo en
        // una decision tomada en silencio: el dia que `EstadoDeEjercicio` tenga un tercer valor,
        // los documentos de ese periodo dejarian de confirmarse sin que nadie hubiera escrito esa
        // regla en ningun sitio.
        return estados[0] switch
        {
            nameof(EstadoDelEjercicioParaEscribir.Abierto) => EstadoDelEjercicioParaEscribir.Abierto,
            nameof(EstadoDelEjercicioParaEscribir.Cerrado) => EstadoDelEjercicioParaEscribir.Cerrado,
            _ => throw new InvalidOperationException(
                $"La columna `{ColumnaDeEstado}` del ejercicio dice «{estados[0]}», que no es " +
                "ninguno de los dos estados que este puerto sabe traducir. Un estado nuevo en " +
                "`EstadoDeEjercicio` es una decisión sobre si ese periodo admite escrituras, y " +
                "esa decisión se escribe aquí; no se hereda del valor por omisión."),
        };
    }
}
