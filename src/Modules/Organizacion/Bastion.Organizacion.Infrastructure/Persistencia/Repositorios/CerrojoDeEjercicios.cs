using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.Organizacion.Application.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;
using Microsoft.EntityFrameworkCore;

namespace Bastion.Organizacion.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="ICerrojoDeEjercicios"/>
/// <remarks>
/// <para>
/// <b>Está en un fichero propio, y a propósito</b>, como el cerrojo de la bandeja: la excepción al
/// SQL crudo se lee entera de una vez y no escondida entre los métodos normales de un repositorio.
/// Todo lo que hay aquí es la sentencia que toma el cerrojo.
/// </para>
/// <para>
/// <b>Es SQL crudo porque <c>FOR UPDATE</c> no tiene traducción en EF Core</b>, ni la tiene ninguna
/// cláusula de bloqueo. Las alternativas sin él no son alternativas: un <c>UPDATE</c> tonto sobre
/// la propia fila tomaría el cerrojo, sí, y de paso escribiría —y auditaría— un cambio que nadie
/// ha pedido; y leer por el ORM y confiar en el testigo de concurrencia no sirve aquí, porque quien
/// confirma un documento <b>no escribe esta fila</b> y nunca chocaría con su versión.
/// </para>
/// <para>
/// <b>La empresa la comprueba la sentencia, con el valor de <see cref="IInquilinoActual"/></b> —el
/// mismo del que lo toma el filtro global, nunca de la petición—, porque el filtro no alcanza al
/// SQL crudo y el identificador sí viene de la ruta. Sin esa comparación, quien conociera el
/// identificador de un ejercicio ajeno podría dejar su fila bloqueada desde otra empresa.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto de Organización. La transacción va en él.</param>
/// <param name="inquilino">De donde sale la empresa: el mismo sitio del que la toma el filtro.</param>
internal sealed class CerrojoDeEjercicios(
    OrganizacionDbContext contexto, IInquilinoActual inquilino) : ICerrojoDeEjercicios
{
    /// <summary>El esquema del módulo, nombrado aquí porque la sentencia lo escribe en crudo.</summary>
    public const string Esquema = "organizacion";

    /// <summary>La tabla de ejercicios.</summary>
    public const string Tabla = "ejercicios";

    /// <summary>La columna del estado, que se guarda como texto.</summary>
    public const string ColumnaDeEstado = "estado";

    /// <summary>La columna de la empresa, que la sentencia compara ella misma.</summary>
    public const string ColumnaDeEmpresa = "empresa_id";

    /// <summary>
    /// <b>La lectura que decide el cierre</b>: bloquea la fila en exclusiva y trae el estado que
    /// tenía al bloquearla.
    /// </summary>
    /// <remarks>
    /// <b>Sin punto y coma final</b>: EF Core compone esta cadena dentro de otra sentencia, y un
    /// punto y coma ahí dentro es un error de sintaxis en tiempo de ejecución.
    /// </remarks>
    public const string SqlDelEstadoConCerrojo =
        "SELECT e." + ColumnaDeEstado + " AS \"Value\"" +
        " FROM " + Esquema + "." + Tabla + " AS e" +
        " WHERE e.id = {0}" +
        " AND e." + ColumnaDeEmpresa + " = {1}" +
        " FOR UPDATE";

    /// <inheritdoc />
    public async Task<EstadoDeEjercicio?> TomarEnExclusivaAsync(
        Guid id, CancellationToken cancelacion)
    {
        // REVIENTA SI NO HAY TRANSACCION, por lo mismo que el numerador y que el puerto de
        // Inventario: EF Core abre una transaccion IMPLICITA por cada orden que ejecuta, asi que
        // sin esto el `FOR UPDATE` se soltaria al acabar esta misma lectura y el cierre volveria a
        // tener delante la ventana que viene a cerrar -con la diferencia de que ahora pareceria
        // protegido-.
        //
        // Lanza y no devuelve un fallo de negocio (ADR-0004): quien llama sin transaccion no se ha
        // equivocado de datos, esta mal cableado.
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "No hay transacción abierta en el contexto de Organización, así que el cerrojo " +
                "exclusivo sobre la fila del ejercicio se soltaría al acabar esta lectura y una " +
                "confirmación podría colarse entre la comprobación y el `COMMIT` del cierre. El " +
                "dueño de la transacción es el filtro de idempotencia: la acción que cierra tiene " +
                "que declarar la Idempotency-Key obligatoria.");
        }

        Guid empresaId = inquilino.EmpresaDelFiltro ?? throw new InvalidOperationException(
            "Se está cerrando un ejercicio dentro de un ámbito sin inquilino, y un ejercicio es " +
            "siempre de una empresa: sin ella la sentencia bloquearía el de cualquiera.");

        List<string> estados = await contexto.Database
            .SqlQueryRaw<string>(SqlDelEstadoConCerrojo, id, empresaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (estados.Count == 0)
        {
            return null;
        }

        // EL TEXTO SE TRADUCE POR SUS NOMBRES, y lo que no sea uno de ellos revienta: la columna
        // guarda el enumerado convertido a cadena (`HasConversion<string>`), asi que un valor que
        // no case significa que el mapeo y esto han dejado de decir lo mismo.
        return Enum.TryParse(estados[0], out EstadoDeEjercicio estado)
            && Enum.IsDefined(estado)
            ? estado
            : throw new InvalidOperationException(
                $"La columna `{ColumnaDeEstado}` del ejercicio dice «{estados[0]}», que no es " +
                $"ninguno de los valores de `{nameof(EstadoDeEjercicio)}`.");
    }
}
