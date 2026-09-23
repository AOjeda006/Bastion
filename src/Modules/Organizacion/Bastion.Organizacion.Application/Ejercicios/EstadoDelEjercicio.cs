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
/// <b>El permiso más restrictivo de los cuatro de este recurso</b>, y separado del de cerrar a
/// propósito: reabrir vuelve a admitir apuntes en un periodo que ya se dio por cerrado y del que,
/// probablemente, ya se presentaron modelos. Que sea posible es necesario —una subsanación existe—;
/// que lo pueda hacer cualquiera que sepa cerrar, no.
/// </remarks>
public interface IReabrirEjercicio
{
    /// <summary>Ejecuta el caso de uso.</summary>
    /// <param name="id">Identificador del ejercicio.</param>
    /// <param name="version">La versión que el cliente dice tener (<c>If-Match</c>).</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion);
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

        IReadOnlyList<IDocumentosDeUnPeriodo> inscritos = [.. documentos];

        if (inscritos.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay ningún módulo inscrito como `IDocumentosDeUnPeriodo`, así que cerrar este " +
                "ejercicio no comprobaría si queda algún borrador dentro y contestaría que el " +
                "periodo está limpio sin haber preguntado a nadie. Es un fallo de composición del " +
                "host, no de la petición.");
        }

        // Se pregunta a TODOS y no se para en el primero que diga que sí: el error nombra a todos
        // los módulos que tienen borradores dentro, y parar antes obligaría a cerrar otras tantas
        // veces para ir descubriéndolos de uno en uno.
        List<string> conBorradores = [];

        foreach (IDocumentosDeUnPeriodo modulo in inscritos)
        {
            bool hay = await modulo
                .HayBorradoresEnAsync(
                    ejercicio.EmpresaId, ejercicio.FechaDeInicio, ejercicio.FechaDeFin, cancelacion)
                .ConfigureAwait(false);

            if (hay)
            {
                conBorradores.Add(modulo.Modulo);
            }
        }

        if (conBorradores.Count > 0)
        {
            // Ordinal, para que el mensaje no dependa del orden en que el contenedor devuelva las
            // inscripciones ni de la cultura del proceso que lo escribe.
            conBorradores.Sort(StringComparer.Ordinal);

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
    public async Task<Resultado> EjecutarAsync(Guid id, VersionDeRecurso version, CancellationToken cancelacion)
    {
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
        await unidadTrabajo.ConfirmarAsync(cancelacion).ConfigureAwait(false);

        return Resultado.Correcto();
    }
}
