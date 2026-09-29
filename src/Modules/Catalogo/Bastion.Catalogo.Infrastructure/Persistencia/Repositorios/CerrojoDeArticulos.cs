using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Catalogo.Application.Catalogo;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Catalogo.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="ICerrojoDeArticulos"/>
/// <remarks>
/// <para>
/// <b>Está en un fichero propio, y a propósito</b>, como el cerrojo del ejercicio: la excepción al
/// SQL crudo se lee entera de una vez. Todo lo que hay aquí es la sentencia que toma el cerrojo.
/// </para>
/// <para>
/// <b>Es SQL crudo porque ninguna cláusula de bloqueo tiene traducción en EF Core.</b> Leer por el
/// ORM y confiar en el testigo de concurrencia no sirve: quien confirma un ajuste lee la marca y
/// <b>no escribe esta fila</b>, así que nunca chocaría con su versión.
/// </para>
/// <para>
/// <b>La empresa la comprueba la sentencia, con el valor de <see cref="IInquilinoActual"/></b>, el
/// mismo del que la toma el filtro global. El filtro no alcanza al SQL crudo, y el identificador sí
/// viene de la ruta: sin esa comparación, quien conociera el de un artículo ajeno podría dejar su
/// fila bloqueada desde otra empresa.
/// </para>
/// <para>
/// <b>No mira el borrado lógico.</b> Bloquear una fila borrada no hace daño, y quien decide si el
/// artículo existe para el caso de uso es el repositorio, que lo lee justo después con sus
/// filtros.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Catálogo. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
internal sealed class CerrojoDeArticulos(
    CatalogoDbContext contexto, IInquilinoActual inquilino) : ICerrojoDeArticulos
{
    /// <summary>El esquema del módulo, nombrado aquí porque la sentencia lo escribe en crudo.</summary>
    public const string Esquema = "catalogo";

    /// <summary>La tabla de artículos.</summary>
    public const string Tabla = "articulos";

    /// <summary>La columna de la empresa, que la sentencia compara ella misma.</summary>
    public const string ColumnaDeEmpresa = "empresa_id";

    /// <summary>
    /// <b>La sentencia que bloquea</b>: la fila del artículo, con el cerrojo del <c>UPDATE</c>.
    /// </summary>
    /// <remarks>
    /// <b>Sin punto y coma final</b>: EF Core compone esta cadena dentro de otra sentencia, y un
    /// punto y coma ahí dentro es un error de sintaxis en tiempo de ejecución (ADR-0043).
    /// </remarks>
    public const string SqlDelCerrojo =
        "SELECT a.id AS \"Value\"" +
        " FROM " + Esquema + "." + Tabla + " AS a" +
        " WHERE a.id = {0}" +
        " AND a." + ColumnaDeEmpresa + " = {1}" +
        " FOR NO KEY UPDATE";

    /// <inheritdoc />
    public async Task<bool> TomarEnExclusivaAsync(Guid id, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCION, como el cerrojo del ejercicio: EF Core abre una
        // transaccion IMPLICITA por cada orden, asi que sin esto el cerrojo se soltaria al acabar
        // esta lectura y una confirmacion podria colarse entre la pregunta a Inventario y el
        // `COMMIT`. Lanza y no devuelve un fallo de negocio (ADR-0004): esta mal cableado.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Catálogo, así que el cerrojo sobre " +
                "la fila del artículo se soltaría al acabar esta lectura y una confirmación de " +
                "Inventario podría colarse entre la pregunta y el `COMMIT`. Quien lo pide tiene " +
                "que ir dentro de `EnTransaccionAsync` de la unidad de trabajo del módulo.");
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está bloqueando un artículo dentro de un ámbito sin inquilino, y un artículo es " +
            "siempre de una empresa: sin ella la sentencia bloquearía el de cualquiera.");

        List<Guid> filas = await contexto.Database
            .SqlQueryRaw<Guid>(SqlDelCerrojo, id, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return filas.Count > 0;
    }
}
