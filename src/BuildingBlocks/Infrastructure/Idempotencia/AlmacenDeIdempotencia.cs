using Bastion.BuildingBlocks.Application.Idempotencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bastion.BuildingBlocks.Infrastructure.Idempotencia;

/// <summary>
/// El almacén de claves sobre el <c>DbContext</c> de un módulo. Cada módulo deriva el suyo.
/// </summary>
/// <remarks>
/// <para>
/// <b>La única sentencia cruda del mecanismo, y por qué.</b> Reclamar la clave es un
/// <c>INSERT … ON CONFLICT DO NOTHING</c>, que EF Core no sabe traducir: no hay <i>upsert</i> en el
/// proveedor. Las dos alternativas que no son SQL crudo son peores, y no por poco:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Mirar y luego insertar</b> deja una ventana entre las dos consultas. Dos peticiones con la
/// misma clave la cruzan a la vez, las dos ven «no está» y las dos hacen el trabajo. Es el fallo
/// que el mecanismo entero viene a impedir, reintroducido en su propia implementación.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Insertar y atrapar la violación del índice</b> usa una excepción como flujo de control, y
/// además una que llega desde dentro de una llamada transaccional: en PostgreSQL, un error dentro
/// de una transacción la deja <b>abortada</b>, y todo lo que se intente después falla con
/// «current transaction is aborted». O sea, el <i>catch</i> no puede seguir trabajando; solo puede
/// deshacerlo todo y volver a empezar.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>La excepción al barrido del 0.6 es estrecha, y lo es por su argumento.</b> Esta sentencia
/// <b>no lee ninguna tabla</b>: es una escritura de una fila cuya clave primaria completa se le
/// entrega, y esa clave lleva dentro <c>empresa_id</c>, tomado del <i>claim</i> —nunca de la
/// petición—, exactamente igual que haría el filtro. No hay ninguna fila que un filtro de empresa
/// hubiera protegido y esta sentencia alcance. <b>Todo lo que se LEE de esta tabla</b> —lo que hace
/// <see cref="BuscarAsync"/>— pasa por EF Core con su filtro global puesto. Quien quiera SQL crudo
/// para leer no puede acogerse a esto, porque el argumento entero es que aquí no se lee.
/// </para>
/// </remarks>
/// <param name="contexto">El contexto del módulo. La transacción y el trabajo van en él.</param>
public abstract class AlmacenDeIdempotencia(DbContext contexto) : IAlmacenDeIdempotencia
{
    /// <summary>
    /// La sentencia que reclama la clave. Se expone para que un test pueda leerla y comprobar que
    /// sigue nombrando <c>empresa_id</c> en las columnas y en el objetivo del conflicto, que es de
    /// lo que depende el argumento de arriba.
    /// </summary>
    public const string SqlDeLaReclamacion =
        "INSERT INTO " + ConfiguracionDeIdempotencia.Esquema + "." + ConfiguracionDeIdempotencia.Tabla +
        " (empresa_id, usuario_id, metodo, ruta, clave, huella, creada_en)" +
        " VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})" +
        " ON CONFLICT (empresa_id, usuario_id, metodo, ruta, clave) DO NOTHING";

    private IDbContextTransaction? _transaccion;
    private RegistroDeIdempotencia? _reclamada;

    /// <inheritdoc />
    public async Task AbrirTransaccionAsync(CancellationToken cancelacion)
    {
        if (contexto.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "Ya hay una transacción abierta en el contexto de este módulo. El filtro de " +
                "idempotencia es el dueño de la transacción de la petición: si alguien más la " +
                "abre, la clave y el trabajo pueden acabar en transacciones distintas.");
        }

        // SIN PUNTOS DE GUARDADO, y esto no es afinar: es la diferencia entre que la atomicidad la
        // garantice la base o la recuerde el filtro.
        //
        // Con una transacción abierta, EF Core pone un `SAVEPOINT` delante de cada `SaveChanges` y
        // vuelve a él si falla. Eso deja la transacción VIVA después de un fallo, con la clave ya
        // reclamada dentro: alguien que la confirmara —hoy nadie, mañana cualquiera— dejaría el
        // recibo de un trabajo que se deshizo, y el reintento devolvería un 201 que no creó nada.
        // Sin puntos de guardado, un `SaveChanges` que falla aborta la transacción entera y ya no
        // hay nada que confirmar: la invariante deja de depender de que el filtro se acuerde.
        //
        // Y además la hace VISIBLE. Cada punto de guardado abre una subtransacción, que en
        // PostgreSQL toma su propio identificador, así que las filas de un mismo trabajo salían con
        // `xmin` distintos —comprobado: 759 la de negocio, 760 el recibo, consecutivos— y la prueba
        // que usaron el 0.7 y el 0.8 no valía aquí. Sin ellos, todas llevan el mismo número.
        //
        // El precio, dicho: dentro de una petición idempotente ya no se puede seguir trabajando
        // tras un fallo de guardado. Nadie lo hace, y hay un caso que lo notaría —un choque de
        // concurrencia, porque el manejador del 412 consulta la fila actual y la transacción
        // abortada se lo impediría—; por eso ninguna acción pide los dos mecanismos a la vez, y hay
        // un barrido que lo mantiene así.
        contexto.Database.AutoSavepointsEnabled = false;

        _transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ConfirmarAsync(CancellationToken cancelacion)
    {
        if (_transaccion is not null)
        {
            await _transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
            await _transaccion.DisposeAsync().ConfigureAwait(false);
            _transaccion = null;
        }
    }

    /// <inheritdoc />
    public async Task DeshacerAsync(CancellationToken cancelacion)
    {
        if (_transaccion is not null)
        {
            await _transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);
            await _transaccion.DisposeAsync().ConfigureAwait(false);
            _transaccion = null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReclamarAsync(
        ClaveDeIdempotencia clave, string huella, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(clave);

        int filas = await contexto.Database.ExecuteSqlRawAsync(
            SqlDeLaReclamacion,
            [clave.EmpresaId, clave.UsuarioId, clave.Metodo, clave.Ruta, clave.Clave, huella, ahora],
            cancelacion).ConfigureAwait(false);

        if (filas == 0)
        {
            return false;
        }

        // La fila que se acaba de escribir, adjunta como YA EXISTENTE —que es lo que es— para
        // poder completarla luego con la respuesta sin volver a leerla. El `UPDATE` que salga de
        // ahí cae en esta misma transacción, así que la fila conserva el `xmin` del `INSERT`: el
        // mismo número que llevan las filas de negocio de este trabajo.
        _reclamada = RegistroDeIdempotencia.Reclamada(clave, huella, ahora);
        contexto.Attach(_reclamada);

        return true;
    }

    /// <inheritdoc />
    public Task<RegistroDeIdempotencia?> BuscarAsync(
        ClaveDeIdempotencia clave, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(clave);

        return contexto.Set<RegistroDeIdempotencia>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                fila => fila.EmpresaId == clave.EmpresaId
                    && fila.UsuarioId == clave.UsuarioId
                    && fila.Metodo == clave.Metodo
                    && fila.Ruta == clave.Ruta
                    && fila.Clave == clave.Clave,
                cancelacion);
    }

    /// <inheritdoc />
    public async Task GuardarRespuestaAsync(RespuestaGuardada respuesta, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        if (_reclamada is null)
        {
            throw new InvalidOperationException(
                "No hay ninguna clave reclamada en esta petición: guardar una respuesta sin " +
                "haberla reclamado dejaría un recibo de un trabajo que nadie reservó.");
        }

        _reclamada.Guardar(respuesta);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }
}
