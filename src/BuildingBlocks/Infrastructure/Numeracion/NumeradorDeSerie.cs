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
/// ahí una sentencia que escribe. Hay una tercera, <see cref="SqlDelMotivo"/>, que solo corre
/// cuando el incremento no subió nada y tampoco decide: elige cuál de los errores contar.
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
/// <typeparam name="TDocumento">Las clases de documento que numera el módulo.</typeparam>
/// <param name="contexto">El contexto del módulo que confirma. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
public abstract class NumeradorDeSerie<TDocumento>(DbContext contexto, IInquilinoActual inquilino)
    : INumeradorDeSerie<TDocumento>
    where TDocumento : struct, Enum
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

    /// <summary>La tabla de ejercicios, que es de la que cuelga cada serie.</summary>
    public const string TablaDeEjercicios = "ejercicios";

    /// <summary>
    /// <b>La sentencia que decide</b>: sube el contador de una serie, y solo si esa serie está
    /// activa, es de esta empresa, numera esta clase de documento y cuelga del ejercicio de la
    /// fecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Las dos condiciones sobre el estado y la empresa sustituyen a las dos guardas que se
    /// fueron con <c>Serie.RegistrarNumeroAsignado</c>.</b> El estado, porque una serie cerrada
    /// dice de sí misma que no asigna más números; y la empresa, porque el SQL crudo deja atrás el
    /// filtro global de inquilinato y <c>Serie</c> es <c>IDeInquilino</c>: sin esa comparación,
    /// quien confirmara con el identificador de una serie ajena gastaría un número de otra
    /// sociedad.
    /// </para>
    /// <para>
    /// <b>Las dos siguientes son la R5 entera, «por serie y ejercicio».</b> El tipo, porque una serie
    /// de facturas que numerara un ajuste le haría un hueco a la factura siguiente. Y el ejercicio,
    /// porque una serie del año pasado que siguiera activa numeraría documentos de este año y el
    /// correlativo mezclaría los dos. Los dos extremos del ejercicio van incluidos, como en su
    /// intervalo. <b>No se mira si el ejercicio está abierto</b>, y es a propósito: eso lo pregunta
    /// la R9 por la fecha del documento, y el inverso de un ajuste numera en la serie de su
    /// original aunque su ejercicio ya esté cerrado (ver el puerto).
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
        " JOIN " + Esquema + "." + TablaDeEjercicios + " AS e ON e.id = s.ejercicio_id" +
        " WHERE s.id = c.serie_id" +
        " AND c.serie_id = {0}" +
        " AND s.empresa_id = {1}" +
        " AND s.estado = 'Activa'" +
        " AND s.tipo_de_documento = {2}" +
        " AND {3} BETWEEN e.fecha_de_inicio AND e.fecha_de_fin";

    /// <summary>
    /// Por qué no numeró, cuando la sentencia de arriba no subió nada. <b>No decide nada</b>: solo
    /// elige qué error contar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Filtra por empresa y estado igual que el incremento</b>, y eso es lo que impide que los dos
    /// errores nuevos abran el oráculo que <c>serie-no-numera</c> cierra: una serie ajena, cerrada o
    /// inexistente no devuelve fila, y se contesta lo de siempre. Solo una serie de esta empresa y
    /// activa llega a decir de qué documento es o de qué ejercicio cuelga.
    /// </para>
    /// <para>
    /// <b>Corre después y puede ver otra cosa</b>, si alguien cambió la serie entre las dos
    /// sentencias. Da igual: el número ya no se ha dado, y si esta lectura no encuentra motivo se
    /// contesta <c>serie-no-numera</c>.
    /// </para>
    /// <para>
    /// <b>Devuelve un escalar, el código, y no una fila con dos columnas.</b> Una fila se lee a
    /// través de la convención de nombres del contexto de quien numera —el de Inventario pasa todo
    /// a <c>snake_case</c>, así que esperaba <c>cae_en_su_ejercicio</c> y no el alias que la cadena
    /// escribía—, y el bloque común no sabe qué convención tiene cada módulo. El escalar se lee por
    /// la columna <c>Value</c>, que es de EF Core y no pasa por ninguna convención. Y el orden de
    /// las dos ramas del <c>CASE</c> es el de prioridad: una serie de otro documento lo dice aunque
    /// además sea de otro ejercicio, porque se arregla cambiando de serie y eso arregla las dos.
    /// </para>
    /// </remarks>
    public const string SqlDelMotivo =
        "SELECT CASE" +
        " WHEN s.tipo_de_documento <> {2}" +
        " THEN '" + ErroresDeNumeracion.CodigoDeSerieDeOtroDocumento + "'" +
        " WHEN {3} NOT BETWEEN e.fecha_de_inicio AND e.fecha_de_fin" +
        " THEN '" + ErroresDeNumeracion.CodigoDeFechaFueraDelEjercicioDeLaSerie + "'" +
        " ELSE '' END AS \"Value\"" +
        " FROM " + Esquema + "." + TablaDeSeries + " AS s" +
        " JOIN " + Esquema + "." + TablaDeEjercicios + " AS e ON e.id = s.ejercicio_id" +
        " WHERE s.id = {0}" +
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
    public async Task<Resultado<long>> TomarNumeroAsync(
        Guid serieId,
        TDocumento documento,
        DateOnly fechaQueDecideElEjercicio,
        CancellationToken cancelacion)
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

        // El tipo lo traduce el numerador del modulo, que es el unico que sabe como llama
        // Organizacion a sus documentos. Una sola vez, y el mismo valor para las dos sentencias.
        string tipoDeSerie = TipoDeSerieQueNumera(documento);

        int filas = await contexto.Database
            .ExecuteSqlRawAsync(
                SqlDelIncremento,
                [serieId, empresaId, tipoDeSerie, fechaQueDecideElEjercicio],
                cancelacion)
            .ConfigureAwait(false);

        // NINGUNA FILA DEVUELTA ES UN FALLO, NO UN CERO. Una escritura que no casa con nada no
        // lanza por su cuenta: contesta «cero filas» y sigue. Si eso se dejara pasar, el documento
        // se confirmaria con el numero que hubiera en la variable -cero, o el de nadie- y la serie
        // no se habria enterado.
        if (filas == 0)
        {
            return Resultado.Fallo<long>(await PorQueNoNumeraAsync(
                    serieId, empresaId, tipoDeSerie, fechaQueDecideElEjercicio, cancelacion)
                .ConfigureAwait(false));
        }

        long numero = await contexto.Database
            .SqlQueryRaw<long>(SqlDelNumeroTomado, serieId)
            .SingleAsync(cancelacion)
            .ConfigureAwait(false);

        return Resultado.Correcto(numero);
    }

    /// <summary>
    /// El valor de <c>tipo_de_documento</c> de las series que numeran este documento, tal y como
    /// lo guarda Organización.
    /// </summary>
    /// <remarks>
    /// Lo pone el numerador de cada módulo, porque es el único que conoce a la vez sus documentos
    /// y el nombre que Organización les da; que ese nombre siga siendo el que se guarda no se
    /// confía, lo compara un caso contra el modelo.
    /// </remarks>
    /// <param name="documento">La clase de documento que pide el número.</param>
    /// <returns>El texto que la columna guarda para ese tipo de serie.</returns>
    protected abstract string TipoDeSerieQueNumera(TDocumento documento);

    private async Task<ErrorDeOperacion> PorQueNoNumeraAsync(
        Guid serieId,
        Guid empresaId,
        string tipoDeSerie,
        DateOnly fecha,
        CancellationToken cancelacion)
    {
        // Sin fila -serie ajena, cerrada o inexistente- o con la rama vacia del CASE, es el error
        // de siempre: los dos nuevos solo salen de una serie de esta empresa y activa.
        string? motivo = await contexto.Database
            .SqlQueryRaw<string>(SqlDelMotivo, serieId, empresaId, tipoDeSerie, fecha)
            .SingleOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return motivo switch
        {
            ErroresDeNumeracion.CodigoDeSerieDeOtroDocumento =>
                ErroresDeNumeracion.SerieDeOtroDocumento(serieId),
            ErroresDeNumeracion.CodigoDeFechaFueraDelEjercicioDeLaSerie =>
                ErroresDeNumeracion.FechaFueraDelEjercicioDeLaSerie(serieId, fecha),
            _ => ErroresDeNumeracion.SerieNoNumera(serieId),
        };
    }
}
