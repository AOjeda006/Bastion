using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bastion.BuildingBlocks.Contracts.Importacion;

namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>
/// Pasa a un DTO construido desde una fila las mismas anotaciones de validación que el borde le pasa
/// cuando llega en JSON, y dice qué campo incumple y con qué motivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Las anotaciones del DTO, y no una copia de sus topes.</b> Una fila de CSV y un cuerpo JSON son
/// dos formas de pedir la misma alta; si la importación llevara sus propias longitudes, el día que el
/// contrato cambiara una, las dos admitirían cosas distintas y nada lo diría.
/// </para>
/// <para>
/// <b>Solo el primer incumplimiento de cada campo</b>, y sin su texto: el texto lo escribe el contrato
/// para quien rellena un formulario, y el informe de una importación solo lleva motivos de una lista
/// cerrada (ADR-0034 §2).
/// </para>
/// </remarks>
public static class ContratoDeLaFila
{
    /// <summary>Los campos del DTO que incumplen alguna anotación, sin bajar a los DTO anidados.</summary>
    /// <param name="dto">El DTO, ya relleno.</param>
    /// <param name="prefijo">
    /// Con qué se nombra el DTO en el contrato de quien llama, con su punto: <c>domicilioFiscal.</c>.
    /// </param>
    /// <returns>El nombre del campo como viaja en JSON, con el prefijo, y el motivo.</returns>
    public static IEnumerable<(string Campo, MotivoDeRechazo Motivo)> Incumplimientos(object dto, string prefijo)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(prefijo);

        return IncumplimientosDe(dto, prefijo);
    }

    private static IEnumerable<(string Campo, MotivoDeRechazo Motivo)> IncumplimientosDe(object dto, string prefijo)
    {
        foreach (PropertyInfo propiedad in dto.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? valor = propiedad.GetValue(dto);
            var contexto = new ValidationContext(dto) { MemberName = propiedad.Name };

            ValidationAttribute? incumplida = propiedad
                .GetCustomAttributes<ValidationAttribute>(inherit: true)
                .FirstOrDefault(atributo => atributo.GetValidationResult(valor, contexto) != ValidationResult.Success);

            if (incumplida is not null)
            {
                yield return (prefijo + EnCamello(propiedad.Name), MotivoDe(incumplida, valor));
            }
        }
    }

    private static MotivoDeRechazo MotivoDe(ValidationAttribute atributo, object? valor) => atributo switch
    {
        RequiredAttribute => MotivoDeRechazo.Obligatorio,
        StringLengthAttribute longitud when valor is string texto && texto.Length > longitud.MaximumLength =>
            MotivoDeRechazo.DemasiadoLargo,
        MaxLengthAttribute longitud when valor is string texto && texto.Length > longitud.Length =>
            MotivoDeRechazo.DemasiadoLargo,
        _ => MotivoDeRechazo.NoValido,
    };

    // Como lo nombra la política JSON de la API, que es como lo nombran los errores por campo.
    private static string EnCamello(string nombre) => string.Concat(char.ToLowerInvariant(nombre[0]).ToString(), nombre[1..]);
}
