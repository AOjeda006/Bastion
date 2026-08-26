using System.ComponentModel.DataAnnotations;

namespace Bastion.Organizacion.Contracts.Ejercicios;

/// <summary>Un ejercicio contable, tal como sale de la API.</summary>
/// <param name="Id">Identificador del ejercicio.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="Anio">Año con el que se le conoce.</param>
/// <param name="FechaDeInicio">Primer día del ejercicio.</param>
/// <param name="FechaDeFin">Último día del ejercicio.</param>
/// <param name="Estado">Estado del ejercicio, como texto.</param>
public sealed record EjercicioDto(
    Guid Id,
    Guid EmpresaId,
    int Anio,
    DateOnly FechaDeInicio,
    DateOnly FechaDeFin,
    string Estado);

/// <summary>Lo que hace falta para abrir un ejercicio.</summary>
/// <remarks>
/// Las fechas son <see cref="DateOnly"/> y no instantes: el ejercicio 2026 empieza el 1 de enero
/// de 2026 en Madrid y en Canarias. Un instante obligaría a elegir una zona horaria para algo que
/// no la tiene, y el cliente acabaría mandando medianoches que en UTC caen el día anterior.
/// </remarks>
public sealed record CrearEjercicioDto
{
    /// <summary>Empresa a la que pertenece el ejercicio.</summary>
    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public Guid EmpresaId { get; init; }

    /// <summary>Año con el que se le conoce. No tiene por qué ser el del año natural.</summary>
    [Range(1900, 2999, ErrorMessage = "El año va de {1} a {2}.")]
    public int Anio { get; init; }

    /// <summary>Primer día del ejercicio.</summary>
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaDeInicio { get; init; }

    /// <summary>Último día del ejercicio.</summary>
    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaDeFin { get; init; }
}

/// <summary>
/// Lo que se puede cambiar de un ejercicio abierto: sus fechas.
/// </summary>
/// <remarks>
/// Ni la empresa ni el año: mover un ejercicio de empresa arrastraría todo lo registrado en él a
/// otra contabilidad, y el año es como se le llama en cada libro ya impreso.
/// </remarks>
public sealed record ModificarEjercicioDto
{
    /// <summary>Primer día del ejercicio.</summary>
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaDeInicio { get; init; }

    /// <summary>Último día del ejercicio.</summary>
    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaDeFin { get; init; }
}
