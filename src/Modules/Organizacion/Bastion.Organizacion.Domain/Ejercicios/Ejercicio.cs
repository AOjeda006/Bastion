using Bastion.BuildingBlocks.Domain.Entidades;
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
public sealed class Ejercicio : EntidadBase, IDeInquilino
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

    /// <summary>Cierra el ejercicio. Idempotente.</summary>
    public void Cerrar() => Estado = EstadoDeEjercicio.Cerrado;

    /// <summary>Reabre el ejercicio. Idempotente.</summary>
    public void Reabrir() => Estado = EstadoDeEjercicio.Abierto;

    /// <summary>Indica si una fecha cae dentro del ejercicio, extremos incluidos.</summary>
    public bool Comprende(DateOnly fecha) => fecha >= FechaDeInicio && fecha <= FechaDeFin;

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
