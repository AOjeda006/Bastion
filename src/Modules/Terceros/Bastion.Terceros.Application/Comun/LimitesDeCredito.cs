using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.BuildingBlocks.Domain.Dinero;
using Bastion.Terceros.Contracts.Terceros;

namespace Bastion.Terceros.Application.Comun;

/// <summary>
/// Lee el límite de crédito que llega por la API y lo convierte en un importe, anotando por campo lo
/// que no cuadre.
/// </summary>
/// <remarks>
/// Vivía dentro de <c>FijarLimiteCredito</c> hasta el ítem 1.11, y salió de allí cuando la importación
/// de terceros tuvo que leer lo mismo: dos copias de estas reglas serían dos sitios donde la divisa
/// podría empezar a heredarse en silencio, y uno solo de ellos con la advertencia escrita.
/// </remarks>
internal static class LimitesDeCredito
{
    /// <summary>
    /// Convierte el par (cantidad, divisa) en un importe, o en errores por campo.
    /// </summary>
    /// <remarks>
    /// <b>LA DIVISA NO SE HEREDA EN SILENCIO DE LA EMPRESA</b>, y esta función es donde esa
    /// decisión se puede desobedecer, así que queda escrita aquí. Heredarla —leer
    /// <c>Empresa.DivisaBase</c> cuando el cuerpo no la traiga— parece cómodo y tiene un modo de
    /// fallo mudo: el día que alguien cambie la divisa base de la empresa, todos los límites ya
    /// guardados cambiarían de significado sin que se toque ninguna fila y sin que nada falle. Lo
    /// que la pantalla ofrece por omisión es otra cosa, y es de la pantalla.
    /// </remarks>
    internal static Importe? Leer(LimiteCreditoDeAltaDto peticion, ErroresPorCampo errores)
    {
        // Sin cantidad, no hay límite. Que es distinto de un límite de cero: cero es «no se le
        // fía ni un euro», y no tener límite es «no se le controla el crédito».
        if (peticion.Cantidad is null)
        {
            if (!string.IsNullOrWhiteSpace(peticion.Divisa))
            {
                errores.Agregar(
                    "cantidad",
                    "Ha llegado una divisa sin importe. Para retirar el límite, envíe los dos " +
                    "campos vacíos.");
            }

            return null;
        }

        if (peticion.Cantidad < 0m)
        {
            errores.Agregar("cantidad", "Un límite de crédito no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(peticion.Divisa))
        {
            errores.Agregar(
                "divisa",
                "Un límite de crédito lleva su divisa (ISO 4217). No se hereda de la empresa: un " +
                "importe que no dice de qué es sería la única cantidad de dinero del sistema que " +
                "no lo dice.");

            return null;
        }

        // `EsConocida` y no `Normalizar`: la que PREGUNTA, no la que EXIGE. Una divisa que el
        // catálogo no sabe redondear es un error del formulario y tiene que salir por el campo
        // `divisa`; dentro, en cambio, lanzaría — es la puerta doble del ADR-0004.
        if (!CatalogoDeDivisas.EsConocida(peticion.Divisa))
        {
            errores.Agregar(
                "divisa",
                "No es una divisa de las que el sistema sabe redondear (ISO 4217).");

            return null;
        }

        return errores.Hay ? null : Importe.De(peticion.Cantidad.Value, peticion.Divisa);
    }
}
