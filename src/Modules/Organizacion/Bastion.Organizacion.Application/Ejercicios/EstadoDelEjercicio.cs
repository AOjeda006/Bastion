using Bastion.BuildingBlocks.Application.Concurrencia;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Organizacion.Contracts.Ejercicios;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// Cierra un ejercicio contable (R9).
/// </summary>
/// <remarks>
/// Cerrar impide imputar operaciones al intervalo y bloquea el cambio de fechas. Es la operación
/// que convierte un periodo en definitivo, así que lleva su propio permiso: quien lleva el día a
/// día crea y modifica ejercicios; cerrar el año lo decide quien responde de las cuentas.
/// </remarks>
public interface ICerrarEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
}

/// <summary>
/// Reabre un ejercicio cerrado (R9).
/// </summary>
/// <remarks>
/// <para>
/// <b>El permiso más restrictivo de los cuatro de este recurso</b>, y separado del de cerrar a
/// propósito: reabrir vuelve a admitir apuntes en un periodo que ya se dio por cerrado y del que,
/// probablemente, ya se presentaron modelos. Que sea posible es necesario —una subsanación existe—;
/// que lo pueda hacer cualquiera que sepa cerrar, no.
/// </para>
/// <para>
/// <b>Y las otras dos cosas que lo separan de cerrar: el motivo y el evento.</b> Cerrar deja el
/// estado y basta con él. Reabrir deja un estado que <b>no distingue</b> un ejercicio que nunca se
/// cerró de uno que se cerró y se reabrió: los dos ponen «abierto». Lo que hace falta para
/// contarlo dentro de dos años no está en la fila, así que se emite
/// <see cref="EjercicioReabierto"/> con el motivo dentro.
/// </para>
/// </remarks>
public interface IReabrirEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="peticion">El motivo por el que se reabre.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(
        Guid id,
        ReabrirEjercicioDto peticion,
        VersionDeRecurso version,
        CancellationToken cancelacion);
}

/// <inheritdoc cref="ICerrarEjercicio"/>
/// <remarks>
/// <b>Recorre los módulos inscritos, y antes afirma que hay alguno.</b> Organización no sabe qué
/// módulos tienen documentos —no puede saberlo sin cruzar de esquema, que es lo que el §4
/// prohibe—: pregunta a los que se hayan inscrito como <see cref="IDocumentosDeUnPeriodo"/>. El
/// modo de fallo de eso es el silencio, porque una colección vacía no da error: el bucle no
/// recorre nada, no hay borradores que encontrar y el cierre pasa habiendo preguntado a nadie.
/// Por eso la colección vacía <b>revienta</b> en vez de dejar cerrar (ADR-0020).
/// </remarks>
internal sealed class CerrarEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones,
    IEnumerable<IDocumentosDeUnPeriodo> documentos) : ICerrarEjercicio
{
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo(ErroresDeEjercicio.NoEncontrado(id));
        }

        versiones.Exigir(ejercicio, version);

        // Desde el ítem 2.6 cerrar lo cerrado es un 409 y no un 200 mudo: ver `Ejercicio.Cerrar`.
        if (ejercicio.Estado == EstadoDeEjercicio.Cerrado)
        {
            return Resultado.Fallo(ErroresDeEjercicio.YaCerrado(id));
        }

        IReadOnlyList<string> conBorradores = await LosModulosConDocumentos
            .QuienTieneBorradoresAsync(
                LosModulosConDocumentos.Inscritos(documentos, "cerrar este ejercicio"),
                ejercicio.EmpresaId,
                ejercicio.FechaDeInicio,
                ejercicio.FechaDeFin,
                cancelacion)
            .ConfigureAwait(false);

        if (conBorradores.Count > 0)
        {
            return Resultado.Fallo(ErroresDeEjercicio.ConBorradores(conBorradores));
        }

        ejercicio.Cerrar();
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}

/// <inheritdoc cref="IReabrirEjercicio"/>
internal sealed class ReabrirEjercicio(
    IRepositorioDeEjercicios ejercicios,
    IUnidadTrabajoDeOrganizacion unidadTrabajo,
    IVersionesDeOrganizacion versiones) : IReabrirEjercicio
{
    public async Task<Resultado> EjecutarAsync(
        Guid id,
        ReabrirEjercicioDto peticion,
        VersionDeRecurso version,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        // EL MOTIVO SE MIRA AQUÍ, y antes de leer nada. Un `[Required]` sobre una cadena da por
        // buena «   », y lo que hace falta es que quede escrito algo legible. Es el mismo trato
        // que el motivo de una anulación, y por el mismo argumento: lo que el borde necesita para
        // un cuerpo mal escrito es un 400 con su código, no un 500 (ADR-0004).
        string motivo = (peticion.Motivo ?? string.Empty).Trim();

        if (motivo.Length is 0 or > Ejercicio.LargoDelMotivo)
        {
            return Resultado.Fallo(ErroresDeEjercicio.MotivoNoValido());
        }

        Ejercicio? ejercicio = await ejercicios.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (ejercicio is null)
        {
            return Resultado.Fallo(ErroresDeEjercicio.NoEncontrado(id));
        }

        versiones.Exigir(ejercicio, version);

        // Reabrir lo ya abierto tampoco es un 200 mudo: ver `Ejercicio.Reabrir`.
        if (ejercicio.Estado == EstadoDeEjercicio.Abierto)
        {
            return Resultado.Fallo(ErroresDeEjercicio.YaAbierto(id));
        }

        ejercicio.Reabrir();

        // El evento se registra en el agregado y no se publica aquí: así no puede existir sin su
        // escritura. Si el `ConfirmarAsync` de abajo fallara, no habría reapertura y tampoco
        // quedaría dicho que la hubo.
        ejercicio.Registrar(new EjercicioReabierto(
            ejercicio.Id, ejercicio.EmpresaId, ejercicio.Anio, motivo));

        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
