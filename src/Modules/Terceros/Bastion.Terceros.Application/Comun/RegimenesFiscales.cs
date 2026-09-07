using Bastion.BuildingBlocks.Application.Validacion;
using Bastion.Terceros.Contracts.Terceros;
using Bastion.Terceros.Domain.Terceros;

namespace Bastion.Terceros.Application.Comun;

/// <summary>
/// Lee el régimen fiscal que llega por la API y lo convierte en el del dominio, anotando por campo
/// lo que no cuadre.
/// </summary>
/// <remarks>
/// <para>
/// Vive aquí por lo mismo que <see cref="Identificaciones"/>: lo que produce no es un régimen, es
/// un régimen <b>o</b> una lista de errores por campo, y eso es un concepto del borde (ADR-0004).
/// </para>
/// <para>
/// <b>El territorio llega como texto y el enumerado no se ve desde el contrato</b>, así que la
/// traducción es aquí, que es el primer sitio que ve las dos orillas. Un territorio que no exista
/// tiene que salir como error <b>de ese campo</b>, no como un 500: quien lo escribe mal es quien
/// rellena el formulario, y el formulario tiene que poder pintarlo donde toca.
/// </para>
/// </remarks>
internal static class RegimenesFiscales
{
    /// <summary>
    /// Convierte el DTO en un <see cref="RegimenFiscal"/>, o devuelve nulo dejando en
    /// <paramref name="errores"/> el campo que falla.
    /// </summary>
    /// <param name="dto">El régimen tal como llegó.</param>
    /// <param name="prefijo">Con qué se nombran los campos en el contrato de quien llama.</param>
    /// <param name="errores">Dónde se apunta lo que no cuadra.</param>
    internal static RegimenFiscal? Leer(
        RegimenFiscalDeAltaDto? dto,
        string prefijo,
        ErroresPorCampo errores)
    {
        // Sin régimen, el común: el DTO ya lo trae por omisión, y llegar aquí con nulo solo pasa
        // si el cuerpo trae `"regimenFiscal": null` escrito a mano. Se trata igual que no decirlo.
        if (dto is null)
        {
            return RegimenFiscal.Comun();
        }

        // Se compara contra los NOMBRES, y no con `Enum.TryParse`, por dos motivos que no son
        // de estilo: `TryParse` acepta el ordinal en texto —«3» entraría como `UnionEuropea`,
        // justo el acoplamiento al orden del enumerado que el contrato dice no tener— y acepta
        // listas separadas por comas. Comparando con `GetNames` en ordinal, lo único que entra es
        // uno de los cinco, escrito tal cual.
        if (!Array.Exists(
                Enum.GetNames<TerritorioFiscal>(),
                nombre => string.Equals(nombre, dto.Territorio, StringComparison.Ordinal)))
        {
            errores.Agregar(
                prefijo + "territorio",
                "El territorio fiscal tiene que ser uno de estos cinco: " +
                string.Join(", ", Enum.GetNames<TerritorioFiscal>()) + ".");

            return null;
        }

        return RegimenFiscal.De(
            Enum.Parse<TerritorioFiscal>(dto.Territorio),
            dto.RecargoDeEquivalencia,
            dto.CriterioDeCaja,
            dto.SujetoARetencionIrpf);
    }
}
