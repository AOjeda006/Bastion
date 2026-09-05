using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.Organizacion.Application.Comun;
using Bastion.Organizacion.Domain.Ejercicios;

namespace Bastion.Organizacion.Application.Ejercicios;

/// <summary>
/// Comprueba las fechas de un ejercicio ANTES de dárselas al dominio, para poder decir cuál de
/// las dos está mal.
/// </summary>
/// <remarks>
/// El dominio lanza —dentro, un ejercicio de catorce meses es una invariante rota— y no dice de
/// qué campo del formulario se trata, porque no sabe que existe un formulario. Aquí sí se sabe.
/// Las dos comprobaciones son la misma regla escrita dos veces, y eso es a propósito: la de
/// dentro protege al modelo de cualquier llamada, la de aquí traduce para quien rellenó la
/// pantalla. Quitar la de dentro dejaría el modelo indefenso ante otro caso de uso.
/// </remarks>
internal static class ReglasDeFechas
{
    /// <summary>Apunta en <paramref name="errores"/> lo que no cuadre.</summary>
    internal static void Comprobar(DateOnly inicio, DateOnly fin, ErroresPorCampo errores)
    {
        if (fin < inicio)
        {
            errores.Agregar("fechaDeFin", "No puede ser anterior a la fecha de inicio.");
            return;
        }

        // Art. 26 de la Ley del Impuesto sobre Sociedades: el ejercicio no puede exceder de doce
        // meses. Sí puede ser MÁS CORTO y sí puede ir a caballo de dos años naturales, y por eso
        // no se comprueba contra el 1 de enero.
        if (fin > inicio.AddMonths(Ejercicio.MesesMaximos).AddDays(-1))
        {
            errores.Agregar(
                "fechaDeFin",
                $"Un ejercicio no puede durar más de {Ejercicio.MesesMaximos} meses (art. 26 LIS).");
        }
    }
}
