using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Catalogo.Contracts.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Inventario.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// El puerto de la marca de trazabilidad contestado <b>desde la transacción de Inventario</b>, con
/// el cerrojo compartido puesto en la misma lectura (ADR-0048 §4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no lo contesta Catálogo</b>, que es lo mismo que en <see cref="LosEjerciciosDesdeInventario"/>:
/// sería otra conexión y otra transacción, y el cerrojo se soltaría al acabar la lectura y no al
/// confirmar el documento. Un cambio de marca que llegara entre las dos decidiría sobre una marca que
/// el documento ya está usando.
/// </para>
/// <para>
/// <b>SQL crudo sobre el esquema de otro módulo</b>, con el mismo criterio que el ejercicio: el
/// efecto tiene que caer en la transacción del documento y <c>FOR SHARE</c> no tiene traducción en
/// EF Core. Está en la lista cerrada de <c>ElFiltroNoSeSaltaPorAhiTests</c> con su argumento.
/// </para>
/// <para>
/// <b>La empresa la compara la sentencia</b>, con el valor de <see cref="IInquilinoActual"/>: el filtro
/// global no alcanza al SQL crudo. Un artículo de otra empresa no vuelve, y quien pregunta lo trata
/// como lo que es, un defecto.
/// </para>
/// <para>
/// <b>La tabla y sus columnas van escritas a mano y comparadas contra el modelo</b> de Catálogo, que
/// desde aquí no se ve: lo comprueba <c>LaSentenciaDeLaMarcaNombraLaTablaYLaEmpresaTests</c>.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Inventario. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
internal sealed class LaTrazabilidadDesdeInventario(
    InventarioDbContext contexto, IInquilinoActual inquilino) : IConsultaDeTrazabilidad
{
    /// <summary>El esquema de Catálogo, nombrado aquí porque la sentencia lo escribe en crudo.</summary>
    public const string Esquema = "catalogo";

    /// <summary>La tabla de artículos.</summary>
    public const string Tabla = "articulos";

    /// <summary>La columna de la marca, que se guarda como texto con el nombre del valor.</summary>
    public const string ColumnaDeTrazabilidad = "trazabilidad";

    /// <summary>La cortesía de abrir: la marca de cada artículo, sin cerrojo.</summary>
    /// <remarks>
    /// <b>Sin punto y coma final</b>: EF Core compone esta cadena dentro de otra sentencia.
    /// </remarks>
    public const string SqlDeLasMarcas =
        "SELECT a.id, a." + ColumnaDeTrazabilidad + " AS trazabilidad" +
        " FROM " + Esquema + "." + Tabla + " AS a" +
        " WHERE a.empresa_id = {1}" +
        " AND a.id = ANY({0})";

    /// <summary>
    /// <b>La lectura que decide</b>: la marca de cada artículo, con su fila bloqueada en compartido
    /// hasta el <c>COMMIT</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>FOR SHARE</c></b>, como el ejercicio: varias confirmaciones del mismo artículo van a la
    /// vez, y lo que no puede ir con ellas es el cambio de marca, que toma la fila con
    /// <c>FOR NO KEY UPDATE</c> y espera.
    /// </para>
    /// <para>
    /// <b>En orden de identificador</b>, que PostgreSQL aplica antes de bloquear. Los cerrojos
    /// compartidos no chocan entre sí, y el cambio de marca toma una sola fila, así que no hay ciclo
    /// posible; el orden deja la sentencia igual de pie el día que alguien bloquee dos.
    /// </para>
    /// </remarks>
    public const string SqlDeLasMarcasConCerrojo = SqlDeLasMarcas + " ORDER BY a.id FOR SHARE";

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, MarcaDeTrazabilidad>> MarcasDeAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion) =>
        LeerAsync(SqlDeLasMarcas, articulos, cancelacion);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, MarcaDeTrazabilidad>> MarcasParaMoverAsync(
        IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCIÓN, por lo mismo que el ejercicio: el `FOR SHARE` se soltaría
        // al acabar esta lectura, y la marca que devolviera sería cierta en el instante de leerla y
        // mentira un milisegundo después. Lanza y no devuelve un fallo de negocio (ADR-0004): quien
        // llama sin transacción no se ha equivocado de datos, está mal cableado.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Inventario, así que el cerrojo sobre " +
                "la fila del artículo se soltaría al acabar esta lectura y un cambio de marca podría " +
                "colarse entre la comprobación y el `COMMIT` del documento. El dueño de la " +
                "transacción es el filtro de idempotencia: la acción que confirma tiene que declarar " +
                "la Idempotency-Key obligatoria.");
        }

        return LeerAsync(SqlDeLasMarcasConCerrojo, articulos, cancelacion);
    }

    private async Task<IReadOnlyDictionary<Guid, MarcaDeTrazabilidad>> LeerAsync(
        string sql, IReadOnlyCollection<Guid> articulos, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(articulos);

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está preguntando por la marca de un artículo dentro de un ámbito sin inquilino, y un " +
            "artículo es siempre de una empresa: sin ella la sentencia leería el de cualquiera.");

        Guid[] distintos = [.. articulos.Distinct()];

        if (distintos.Length == 0)
        {
            return new Dictionary<Guid, MarcaDeTrazabilidad>();
        }

        List<FilaDeMarca> filas = await contexto.Database
            .SqlQueryRaw<FilaDeMarca>(sql, distintos, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas.ToDictionary(fila => fila.Id, fila => Traducir(fila));
    }

    /// <summary>
    /// El texto se traduce por sus tres nombres, y cualquier otro revienta: un valor nuevo de la marca
    /// es una decisión sobre qué líneas admite, y se escribe aquí, no se hereda de un valor por omisión.
    /// </summary>
    private static MarcaDeTrazabilidad Traducir(FilaDeMarca fila) => fila.Trazabilidad switch
    {
        nameof(MarcaDeTrazabilidad.Ninguna) => MarcaDeTrazabilidad.Ninguna,
        nameof(MarcaDeTrazabilidad.PorLote) => MarcaDeTrazabilidad.PorLote,
        nameof(MarcaDeTrazabilidad.PorNumeroSerie) => MarcaDeTrazabilidad.PorNumeroSerie,
        _ => throw new InvalidOperationException(
            $"La columna `{ColumnaDeTrazabilidad}` del artículo {fila.Id} dice «{fila.Trazabilidad}», " +
            "que no es ninguna de las tres marcas que este puerto sabe traducir."),
    };
}

/// <summary>Una fila de la sentencia de la marca.</summary>
/// <remarks>
/// Las columnas se leen por el nombre que les da la convención del contexto, en <c>snake_case</c>,
/// que es el mismo que llevan los alias de la consulta.
/// </remarks>
internal sealed class FilaDeMarca
{
    public Guid Id { get; init; }

    public string Trazabilidad { get; init; } = string.Empty;
}
