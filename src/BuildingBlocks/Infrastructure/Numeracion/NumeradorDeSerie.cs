using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Application.Numeracion;
using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Bastion.BuildingBlocks.Infrastructure.Numeracion;

/// <summary>
/// El numerador sobre el <c>DbContext</c> de un módulo. Cada módulo que numera deriva el suyo.
/// </summary>
/// <remarks>
/// <para>
/// <b>El número se toma con un incremento condicionado, no leyendo y sumando.</b> Leer el contador,
/// sumarle uno y guardarlo deja una ventana entre la lectura y la escritura, y dos confirmaciones
/// que la crucen a la vez se llevan el mismo número: dos documentos con el mismo número es
/// exactamente lo que la R5 prohíbe, y no se arregla después. El incremento en la propia sentencia
/// toma el cerrojo de la fila en el motor y lo suelta al confirmar la transacción, así que la
/// segunda confirmación espera y se lleva el siguiente.
/// </para>
/// <para>
/// <b>Y no es una secuencia de PostgreSQL</b>, que sería lo cómodo: <c>nextval</c> no se revierte
/// al deshacer la transacción, así que una confirmación que falla dejaría el número gastado y un
/// hueco permanente en la numeración. El artículo 6.1.a del RD 1619/2012 pide «correlativa y sin
/// huecos», y esa es la diferencia entera entre esta fila y una secuencia (ADR-0007, ADR-0039).
/// </para>
/// <para>
/// <b>Dos sentencias y no una, y la de arriba es la que decide.</b> El incremento se lleva todo el
/// <c>WHERE</c>; la segunda solo lee lo que el incremento acaba de escribir, y es segura sin
/// condición ninguna porque corre <b>dentro de la misma transacción</b> y sobre una fila que el
/// incremento anterior mantiene bloqueada hasta el <c>COMMIT</c>: nadie puede haberla movido en
/// medio. Se parten porque una escritura con <c>RETURNING</c> no cabe en las consultas crudas de EF
/// Core, que <b>componen</b> el SQL dentro de un <c>SELECT … FROM (…)</c>, y PostgreSQL no admite
/// ahí una sentencia que escribe.
/// </para>
/// <para>
/// <b>Escribe en el esquema de otro módulo, y eso es la segunda excepción del ADR-0013.</b> La
/// primera —la bandeja— existe porque un módulo no puede tocar las tablas de otro; esta existe
/// porque <b>tampoco puede esperar</b>: el número y el documento que lo lleva tienen que confirmarse
/// en la misma transacción, y la bandeja es asíncrona por definición. Un número que llega después
/// es un documento confirmado sin número mientras tanto, o para siempre si el suscriptor falla. El
/// criterio que acota la excepción, en el ADR de la numeración.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo que confirma. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
public abstract class NumeradorDeSerie(DbContext contexto, IInquilinoActual inquilino) : INumeradorDeSerie
{
    /// <summary>El esquema de Organización, nombrado aquí porque la sentencia lo escribe en crudo.</summary>
    /// <remarks>
    /// <b>Escrito a mano y no tomado del modelo</b>, porque el bloque común no ve el interior de
    /// ningún módulo: <c>ContadorDeSerie</c> es de <c>Organizacion.Domain</c> y desde aquí no
    /// existe. Que estas cuatro cadenas sigan siendo las del mapeo de verdad no se confía:
    /// <c>LaSentenciaDeNumeracionNombraLaTablaDeVerdadTests</c> las compara contra el modelo de EF
    /// Core, que es quien manda.
    /// </remarks>
    public const string Esquema = "organizacion";

    /// <summary>La tabla del contador, que desde el ADR-0039 no es la de la serie.</summary>
    public const string TablaDeContadores = "contadores_de_serie";

    /// <summary>La tabla de series, que es la que condiciona el incremento.</summary>
    public const string TablaDeSeries = "series";

    /// <summary>La columna que guarda el último número entregado.</summary>
    public const string ColumnaDelNumero = "ultimo_numero";

    /// <summary>
    /// <b>La sentencia que decide</b>: sube el contador de una serie, y solo si esa serie está
    /// activa y es de esta empresa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las dos condiciones sobre las series sustituyen a las dos guardas que se fueron con
    /// <c>Serie.RegistrarNumeroAsignado</c>.</b> El estado, porque una serie cerrada dice de sí
    /// misma que no asigna más números; y la empresa, porque el SQL crudo deja atrás el filtro
    /// global de inquilinato y <c>Serie</c> es <c>IDeInquilino</c>: sin esa comparación, quien
    /// confirmara con el identificador de una serie ajena gastaría un número de otra sociedad.
    /// </para>
    /// <para>
    /// <b>Sin punto y coma final</b>: EF Core compone estas cadenas dentro de otra sentencia, y un
    /// punto y coma ahí dentro es un error de sintaxis en tiempo de ejecución.
    /// </para>
    /// </remarks>
    public const string SqlDelIncremento =
        "UPDATE " + Esquema + "." + TablaDeContadores + " AS c" +
        " SET " + ColumnaDelNumero + " = c." + ColumnaDelNumero + " + 1" +
        " FROM " + Esquema + "." + TablaDeSeries + " AS s" +
        " WHERE s.id = c.serie_id" +
        " AND c.serie_id = {0}" +
        " AND s.empresa_id = {1}" +
        " AND s.estado = 'Activa'";

    /// <summary>
    /// El número que el incremento acaba de escribir. Sin condiciones: la fila está bloqueada por
    /// la sentencia de arriba y la transacción es la misma.
    /// </summary>
    public const string SqlDelNumeroTomado =
        "SELECT c." + ColumnaDelNumero + " AS \"Value\"" +
        " FROM " + Esquema + "." + TablaDeContadores + " AS c" +
        " WHERE c.serie_id = {0}";

    /// <inheritdoc />
    public async Task<Resultado<long>> TomarNumeroAsync(Guid serieId, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCION, y se pregunta por `CurrentTransaction` y no por una
        // bandera propia: EF Core abre una transaccion IMPLICITA por cada orden que ejecuta, asi
        // que sin esta comprobacion el incremento se confirmaria solo y no se notaria nada. El
        // sintoma llegaria mucho despues y en otro sitio: un documento que falla al guardarse deja
        // el numero gastado -o sea, un hueco-, que es justo lo que la R5 prohibe.
        //
        // Lanza y no devuelve un fallo de negocio (ADR-0004): quien llama sin transaccion no se ha
        // equivocado de datos, esta mal cableado, y eso no es un desenlace que contarle a nadie.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de este módulo, así que el número se " +
                "confirmaría por su cuenta y quedaría gastado si el documento no llega a " +
                "guardarse. El dueño de la transacción es el filtro de idempotencia: la acción " +
                "que confirma tiene que declarar la Idempotency-Key obligatoria.");
        }

        // La empresa sale de donde la toma el filtro global, no de quien llama. En un ambito sin
        // inquilino no hay ninguna, y numerar ahi no significa nada: un documento pertenece
        // siempre a una sociedad.
        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está numerando dentro de un ámbito sin inquilino, y un documento pertenece " +
            "siempre a una empresa: sin ella la sentencia no podría comprobar que la serie es de " +
            "quien confirma.");

        int filas = await contexto.Database
            .ExecuteSqlRawAsync(SqlDelIncremento, [serieId, empresaId], cancelacion)
            .ConfigureAwait(false);

        // NINGUNA FILA DEVUELTA ES UN FALLO, NO UN CERO. Una escritura que no casa con nada no
        // lanza por su cuenta: contesta «cero filas» y sigue. Si eso se dejara pasar, el documento
        // se confirmaria con el numero que hubiera en la variable -cero, o el de nadie- y la serie
        // no se habria enterado.
        if (filas == 0)
        {
            return Resultado.Fallo<long>(ErroresDeNumeracion.SerieNoNumera(serieId));
        }

        long numero = await contexto.Database
            .SqlQueryRaw<long>(SqlDelNumeroTomado, serieId)
            .SingleAsync(cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(numero);
    }
}
