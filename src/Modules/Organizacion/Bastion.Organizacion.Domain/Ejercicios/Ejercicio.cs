using Bastion.BuildingBlocks.Domain.Eventos;
using Bastion.BuildingBlocks.Domain.Multiempresa;

namespace Bastion.Organizacion.Domain.Ejercicios;

/// <summary>
/// Ejercicio contable de una empresa: el intervalo de fechas al que se imputan las operaciones.
/// </summary>
/// <remarks>
/// <para>
/// Sus dos fechas son <see cref="DateOnly"/>, no instantes. El ejercicio 2026 empieza el 1 de
/// enero de 2026 en Madrid y en Canarias; un <c>timestamptz</c> obligaría a elegir una zona
/// horaria para algo que no la tiene, y el 1 de enero a las 00:00 en Madrid ya es el 31 de
/// diciembre en UTC-1. En PostgreSQL son columnas <c>date</c>.
/// </para>
/// <para>
/// Lleva <c>empresa_id</c> desde la primera tabla (R8). El filtro global que lo aplica siempre
/// es del ítem 0.6; la columna es de hoy, porque añadirla después obliga a tocar todas las
/// tablas y todas las consultas.
/// </para>
/// </remarks>
// HEREDA DE `RaizAgregado` DESDE EL 2.6, y solo por la reapertura. Heredarla significa
// exactamente una cosa —«de esta raíz salen eventos»— y hasta este ítem no salía ninguno: cerrar
// un ejercicio es el curso normal de las cosas. Reabrirlo no lo es, y el estado de la fila solo
// dice cómo está AHORA: que estuvo cerrado y volvió a abrirse no se puede preguntar a la fila.
public sealed class Ejercicio : RaizAgregado, IDeInquilino
{
    /// <summary>Duración máxima de un ejercicio: doce meses (art. 26 de la LIS).</summary>
    public const int MesesMaximos = 12;

    private Ejercicio(
        Guid id, Guid empresaId, int anio, DateOnly inicio, DateOnly fin, DateTimeOffset momento)
        : base(momento)
    {
        Id = id;
        EmpresaId = empresaId;
        Anio = anio;
        FechaDeInicio = inicio;
        FechaDeFin = fin;
        Estado = EstadoDeEjercicio.Abierto;
    }

    private Ejercicio()
    {
    }

    /// <summary>Identificador del ejercicio.</summary>
    public Guid Id { get; private set; }

    /// <summary>Empresa a la que pertenece (R8).</summary>
    public Guid EmpresaId { get; private set; }

    /// <summary>Año con el que se nombra el ejercicio.</summary>
    public int Anio { get; private set; }

    /// <summary>Primer día del ejercicio. Fecha de calendario, sin hora ni zona.</summary>
    public DateOnly FechaDeInicio { get; private set; }

    /// <summary>Último día del ejercicio, incluido.</summary>
    public DateOnly FechaDeFin { get; private set; }

    /// <summary>Abierto o cerrado (R9).</summary>
    public EstadoDeEjercicio Estado { get; private set; }

    /// <summary>Longitud máxima del motivo de una reapertura.</summary>
    /// <remarks>
    /// La misma que el motivo de una anulación, y por el mismo motivo: lo que cabe en un renglón
    /// de un listado y basta para entender la decisión dentro de dos años.
    /// </remarks>
    public const int LargoDelMotivo = 300;

    /// <summary>Crea un ejercicio abierto.</summary>
    /// <remarks>
    /// El <c>momento</c> es un instante —cuándo se dio de alta la ficha— y no una de las dos
    /// fechas del ejercicio, que son días de calendario. Las tres van juntas en la firma y son de
    /// tipos distintos a propósito: es R14 vigilado por el compilador.
    /// </remarks>
    public static Ejercicio Crear(
        Guid empresaId, int anio, DateOnly inicio, DateOnly fin, DateTimeOffset momento)
    {
        ExigirEmpresa(empresaId);
        ExigirIntervaloValido(inicio, fin);

        return new Ejercicio(Guid.CreateVersion7(), empresaId, anio, inicio, fin, momento);
    }

    /// <summary>Cambia el intervalo del ejercicio. Solo si sigue abierto.</summary>
    public void Modificar(DateOnly inicio, DateOnly fin)
    {
        if (Estado == EstadoDeEjercicio.Cerrado)
        {
            throw new InvalidOperationException(
                "Un ejercicio cerrado no admite cambio de fechas: mover el intervalo movería " +
                "las operaciones que caen dentro (R9).");
        }

        ExigirIntervaloValido(inicio, fin);

        FechaDeInicio = inicio;
        FechaDeFin = fin;
    }

    /// <summary>Cierra el ejercicio. Solo si está abierto.</summary>
    /// <remarks>
    /// <b>Dejó de ser idempotente en el ítem 2.6, y el cambio tiene motivo.</b> Mientras cerrar
    /// solo asignaba un estado, repetirlo era inofensivo y ahorrarse la comprobación salía barato.
    /// Ahora cerrar es una operación con precondiciones —no puede quedar ningún borrador con fecha
    /// dentro— y con consecuencias: reabrir lleva permiso propio, motivo obligatorio y evento
    /// auditado. Un segundo cierre que contesta «hecho» sin hacer nada le dice a quien lo pide que
    /// su petición hizo algo, y esconde justo el caso que importa: dos personas cerrando a la vez,
    /// o un cierre contra un ejercicio que otro reabrió y volvió a cerrar por en medio.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Si el ejercicio ya estaba cerrado.</exception>
    public void Cerrar()
    {
        if (Estado == EstadoDeEjercicio.Cerrado)
        {
            throw new InvalidOperationException(
                "El ejercicio ya estaba cerrado. Cerrar dos veces no es lo mismo que cerrar una: " +
                "quien lo pide tiene que enterarse de que otro se le adelantó (R9).");
        }

        Estado = EstadoDeEjercicio.Cerrado;
    }

    /// <summary>Reabre el ejercicio. Solo si está cerrado.</summary>
    /// <remarks>
    /// Por lo mismo que <see cref="Cerrar"/>, y con más motivo: reabrir vuelve a admitir apuntes en
    /// un periodo que ya se dio por definitivo, así que es un hecho que se audita. Reabrir lo ya
    /// abierto no es un hecho, y contestar que sí dejaría en la traza una reapertura que no ocurrió.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Si el ejercicio ya estaba abierto.</exception>
    public void Reabrir()
    {
        if (Estado == EstadoDeEjercicio.Abierto)
        {
            throw new InvalidOperationException(
                "El ejercicio ya estaba abierto. Una reapertura que no reabre nada no se audita " +
                "como si lo hubiera hecho (R9).");
        }

        Estado = EstadoDeEjercicio.Abierto;
    }

    /// <summary>Indica si una fecha cae dentro del ejercicio, extremos incluidos.</summary>
    public bool Comprende(DateOnly fecha) => fecha >= FechaDeInicio && fecha <= FechaDeFin;

    /// <summary>
    /// Los trozos del intervalo <b>actual</b> que un intervalo nuevo dejaría fuera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Vive aquí y no en el caso de uso porque es aritmética de fechas, no una decisión.</b>
    /// Y porque así se puede comprobar entera sin una base de datos: los casos que importan son
    /// los extremos —encoger por un lado, por los dos, mover el intervalo entero fuera— y son
    /// justo los que nadie escribe si hay que levantar un contenedor para cada uno.
    /// </para>
    /// <para>
    /// <b>Cero, uno o dos trozos.</b> Cero cuando el intervalo nuevo cubre al actual —no deja
    /// nada fuera, aunque absorba días que antes no eran suyos—; dos cuando encoge por los dos
    /// lados; uno en los demás. Si el intervalo nuevo no toca al actual, el trozo es el actual
    /// entero, que es lo que tiene que ser: mover un ejercicio a otro año deja fuera todo lo que
    /// había dentro.
    /// </para>
    /// <para>
    /// <b>Cerrados por los dos lados</b>, como <see cref="Comprende"/> y como el
    /// <c>daterange(…, '[]')</c> de la restricción de exclusión: los tres tienen que decir lo
    /// mismo del primer y del último día.
    /// </para>
    /// </remarks>
    /// <param name="inicio">Primer día del intervalo nuevo.</param>
    /// <param name="fin">Último día del intervalo nuevo.</param>
    /// <returns>Los trozos que quedarían fuera, en orden.</returns>
    public IReadOnlyList<(DateOnly Desde, DateOnly Hasta)> LoQueDejariaFuera(
        DateOnly inicio, DateOnly fin)
    {
        List<(DateOnly Desde, DateOnly Hasta)> fuera = [];

        if (inicio > FechaDeInicio)
        {
            fuera.Add((FechaDeInicio, Antes(FechaDeFin, inicio.AddDays(-1))));
        }

        if (fin < FechaDeFin)
        {
            fuera.Add((Despues(FechaDeInicio, fin.AddDays(1)), FechaDeFin));
        }

        return fuera;
    }

    private static DateOnly Antes(DateOnly una, DateOnly otra) => una < otra ? una : otra;

    private static DateOnly Despues(DateOnly una, DateOnly otra) => una > otra ? una : otra;

    private static void ExigirEmpresa(Guid empresaId)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException(
                "Un ejercicio pertenece siempre a una empresa (R8).", nameof(empresaId));
        }
    }

    private static void ExigirIntervaloValido(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
        {
            throw new ArgumentException(
                $"El ejercicio acabaría ({fin:yyyy-MM-dd}) antes de empezar ({inicio:yyyy-MM-dd}).",
                nameof(fin));
        }

        // El art. 26 de la Ley del Impuesto sobre Sociedades permite un ejercicio partido —no
        // tiene por qué coincidir con el año natural—, pero no uno de más de doce meses.
        if (fin > inicio.AddMonths(MesesMaximos).AddDays(-1))
        {
            throw new ArgumentException(
                $"Un ejercicio no puede durar más de {MesesMaximos} meses (art. 26 de la LIS).",
                nameof(fin));
        }
    }
}
