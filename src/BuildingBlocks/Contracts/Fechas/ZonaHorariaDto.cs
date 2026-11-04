using System.Text.Json.Serialization;

namespace Bastion.BuildingBlocks.Contracts.Fechas;

/// <summary>Una de las zonas horarias que admite el negocio, por su identificador IANA (ADR-0079).</summary>
/// <remarks>
/// <para>
/// <b>Es un enumerado del contrato, y no texto libre</b>, por lo mismo que
/// <c>MotivoDeRechazo</c>: la lista cerrada es la promesa, y un cliente generado tiene que poder
/// comprobarla al compilar. Se publica con el identificador IANA, que es el que guarda la base y el
/// que entiende el sistema, y no con el nombre del miembro.
/// </para>
/// <para>
/// <b>La lista que manda es la del dominio</b>, <c>ZonaHoraria.Admitidas</c>, que es quien valida.
/// Esta la repite porque el contrato no ve el dominio, y un caso del carril rápido compara las dos
/// en los dos sentidos: una zona que se añadiera a una sola de las dos lo pondría rojo.
/// </para>
/// <para>
/// <b>Y no admite números</b>, al revés que el conversor por omisión, que lee <c>0</c> como la
/// primera y <c>7</c> como un valor que no existe: ese llegaría al dominio sin zona, y la
/// petición contestaría un <c>500</c> en vez de un <c>400</c>. Ojo: en la API manda el conversor
/// que registra el host para todos los enumerados, no este, porque el de las opciones gana al del
/// tipo. El del host tampoco admite números, y lo vigila un caso de integración; este vale para
/// todo lo que serialice el tipo fuera de MVC.
/// </para>
/// </remarks>
[JsonConverter(typeof(ConversorDeZonaHoraria))]
public enum ZonaHorariaDto
{
    /// <summary>La peninsular, y la de Baleares.</summary>
    [JsonStringEnumMemberName("Europe/Madrid")]
    Madrid,

    /// <summary>La de Canarias.</summary>
    [JsonStringEnumMemberName("Atlantic/Canary")]
    Canarias,

    /// <summary>La de Ceuta y Melilla.</summary>
    [JsonStringEnumMemberName("Africa/Ceuta")]
    Ceuta,
}

/// <summary>Lee y escribe la zona por su identificador, y nunca como número.</summary>
public sealed class ConversorDeZonaHoraria() : JsonStringEnumConverter<ZonaHorariaDto>(
    namingPolicy: null, allowIntegerValues: false);
