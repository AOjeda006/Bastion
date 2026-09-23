using Bastion.Organizacion.Contracts.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// La pregunta que cerrar, mover y borrar un ejercicio le hacen a los módulos inscritos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe para que la guarda del conjunto vacío esté escrita UNA vez.</b> Los tres casos de uso
/// recorren la misma colección inyectada y los tres tienen el mismo modo de fallo: una colección
/// vacía no da error, el bucle no recorre nada, y los tres contestan que el periodo está limpio
/// habiendo preguntado a nadie. Tres copias de esa comprobación son tres sitios donde se puede
/// borrar una y que las otras dos sigan verdes (ADR-0020).
/// </para>
/// <para>
/// <b>Lanza, y no devuelve un error de operación</b>, porque no es un fallo de la petición: quien
/// la manda no puede hacer nada distinto. Es el host mal compuesto —un módulo con documentos que
/// no se inscribió—, y eso tiene que reventar y salir en el registro, no convertirse en un 409 que
/// alguien intente arreglar cambiando las fechas.
/// </para>
/// </remarks>
internal static class LosModulosConDocumentos
{
    /// <summary>Los módulos inscritos, exigiendo que haya alguno.</summary>
    /// <param name="documentos">La colección que inyecta el contenedor.</param>
    /// <param name="operacion">Qué se estaba intentando, para que el mensaje lo diga.</param>
    /// <exception cref="InvalidOperationException">Si no hay ni un módulo inscrito.</exception>
    internal static IReadOnlyList<IDocumentosDeUnPeriodo> Inscritos(
        IEnumerable<IDocumentosDeUnPeriodo> documentos, string operacion)
    {
        IReadOnlyList<IDocumentosDeUnPeriodo> inscritos = [.. documentos];

        if (inscritos.Count == 0)
        {
            throw new InvalidOperationException(
                $"No hay ningún módulo inscrito como `{nameof(IDocumentosDeUnPeriodo)}`, así que " +
                $"{operacion} no comprobaría si queda algún documento dentro del intervalo y " +
                "contestaría que el periodo está limpio sin haber preguntado a nadie. Es un fallo " +
                "de composición del host, no de la petición.");
        }

        return inscritos;
    }

    /// <summary>Qué módulos tienen algún <b>borrador</b> dentro del intervalo.</summary>
    /// <remarks>
    /// Se pregunta a TODOS y no se para en el primero que diga que sí: el error nombra a todos los
    /// que se niegan, y parar antes obligaría a reintentar otras tantas veces para ir
    /// descubriéndolos de uno en uno.
    /// </remarks>
    /// <param name="inscritos">Los módulos, ya comprobados con <see cref="Inscritos"/>.</param>
    /// <param name="empresaId">Empresa por la que se pregunta (R8).</param>
    /// <param name="desde">Primer día del intervalo, incluido.</param>
    /// <param name="hasta">Último día del intervalo, incluido.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<IReadOnlyList<string>> QuienTieneBorradoresAsync(
        IReadOnlyList<IDocumentosDeUnPeriodo> inscritos,
        Guid empresaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion)
    {
        List<string> conBorradores = [];

        foreach (IDocumentosDeUnPeriodo modulo in inscritos)
        {
            bool hay = await modulo
                .HayBorradoresEnAsync(empresaId, desde, hasta, cancelacion)
                .ConfigureAwait(false);

            if (hay)
            {
                conBorradores.Add(modulo.Modulo);
            }
        }

        return Ordenados(conBorradores);
    }

    /// <summary>Qué módulos tienen algún documento —en el estado que sea— en alguno de los tramos.</summary>
    /// <remarks>
    /// Los <b>tramos</b> son varios porque encoger un ejercicio por los dos lados deja fuera dos
    /// trozos, uno por delante y otro por detrás, y los dos cuentan. Preguntar por el intervalo
    /// entero en vez de por los trozos dejaría sin poder moverse cualquier ejercicio con un solo
    /// movimiento dentro, que es todos.
    /// </remarks>
    /// <param name="inscritos">Los módulos, ya comprobados con <see cref="Inscritos"/>.</param>
    /// <param name="empresaId">Empresa por la que se pregunta (R8).</param>
    /// <param name="tramos">Los intervalos por los que se pregunta, cerrados por los dos lados.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    internal static async Task<IReadOnlyList<string>> QuienTieneDocumentosAsync(
        IReadOnlyList<IDocumentosDeUnPeriodo> inscritos,
        Guid empresaId,
        IReadOnlyList<(DateOnly Desde, DateOnly Hasta)> tramos,
        CancellationToken cancelacion)
    {
        List<string> conDocumentos = [];

        foreach (IDocumentosDeUnPeriodo modulo in inscritos)
        {
            foreach ((DateOnly desde, DateOnly hasta) in tramos)
            {
                bool hay = await modulo
                    .HayDocumentosEnAsync(empresaId, desde, hasta, cancelacion)
                    .ConfigureAwait(false);

                if (hay)
                {
                    conDocumentos.Add(modulo.Modulo);
                    break;
                }
            }
        }

        return Ordenados(conDocumentos);
    }

    // Ordinal, para que el mensaje no dependa del orden en que el contenedor devuelva las
    // inscripciones ni de la cultura del proceso que lo escribe.
    private static List<string> Ordenados(List<string> modulos)
    {
        modulos.Sort(StringComparer.Ordinal);

        return modulos;
    }
}
