using System.Text.Json.Serialization;

namespace Bastion.BuildingBlocks.Contracts.Importacion;

/// <summary>
/// Lo que contesta una importación que ha llegado a mirar las filas: cuántas había, cuántas han
/// entrado, y dónde están las que no (ADR-0034 §2).
/// </summary>
/// <remarks>
/// <para>
/// <b>No lleva ningún valor del fichero.</b> Ni el NIF de la fila repetida ni el importe mal
/// escrito: la línea, la columna y el motivo bastan para corregirlo, porque el fichero lo tiene
/// quien lo mandó. Y esta respuesta se guarda entera en el recibo de idempotencia, así que lo que
/// no esté aquí tampoco se queda allí.
/// </para>
/// <para>
/// <b>Agrupado por columna y motivo, con la lista de líneas</b>, y no fila por fila. El tamaño crece
/// con el número de pares (fila, motivo), a unos seis bytes cada uno, y no con el texto: el peor caso
/// que admiten los topes ronda los 530 KiB, y lo normal es un kilobyte.
/// </para>
/// </remarks>
/// <param name="Leidas">Filas con algún dato. Las que traen todos los campos vacíos no cuentan.</param>
/// <param name="Importadas">Filas que han entrado. Las demás, rechazadas, no han escrito nada.</param>
/// <param name="Rechazadas">Filas con al menos un motivo. Siempre <c>Leidas - Importadas</c>.</param>
/// <param name="Rechazos">Cada par de columna y motivo con las líneas en las que ocurre.</param>
public sealed record InformeDeImportacionDto(
    int Leidas,
    int Importadas,
    int Rechazadas,
    IReadOnlyList<RechazoDto> Rechazos);

/// <summary>Un motivo de rechazo en una columna, y las líneas del fichero en las que se da.</summary>
/// <param name="Columna">
/// El nombre de la columna tal como va en la cabecera, o nulo si el motivo es de la fila entera.
/// </param>
/// <param name="Motivo">Por qué, de una lista cerrada.</param>
/// <param name="Lineas">
/// Las líneas, en orden y sin repetir, contadas como las enseña una hoja de cálculo: la cabecera es
/// la 1, y un campo entre comillas con un salto de línea dentro sigue siendo una sola.
/// </param>
public sealed record RechazoDto(string? Columna, MotivoDeRechazo Motivo, IReadOnlyList<int> Lineas);

/// <summary>Por qué se rechaza una fila, o una columna de una fila.</summary>
/// <remarks>
/// <para>
/// <b>Se publica con los nombres del ADR-0034 y no con los del enumerado</b>: estos no se enseñan, se
/// traducen, igual que los <c>type</c> de un error, y el cliente los busca con la misma forma.
/// </para>
/// <para>
/// <b>Y es el primer enumerado del contrato</b>: los demás viajan como texto libre con su lista en la
/// documentación. Este no, porque la lista cerrada es la promesa —el informe no puede decir nada que
/// no esté en ella— y un cliente generado tiene que poder comprobarlo al compilar. El conversor va en
/// el propio tipo y no en las opciones de MVC porque el generador del documento no lee esas: sin él,
/// el contrato publicaría un número.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<MotivoDeRechazo>))]
public enum MotivoDeRechazo
{
    /// <summary>La fila no trae tantos campos como la cabecera. Es de la fila entera.</summary>
    [JsonStringEnumMemberName("numero-de-campos-distinto")]
    NumeroDeCamposDistinto,

    /// <summary>Una comilla en mitad de un campo sin comillas, o texto detrás de la que cierra.</summary>
    [JsonStringEnumMemberName("comillas-mal-colocadas")]
    ComillasMalColocadas,

    /// <summary>El campo es obligatorio y viene vacío.</summary>
    [JsonStringEnumMemberName("obligatorio")]
    Obligatorio,

    /// <summary>El campo pasa de la longitud que admite el contrato.</summary>
    [JsonStringEnumMemberName("demasiado-largo")]
    DemasiadoLargo,

    /// <summary>El texto no se puede leer como lo que la columna espera: un importe, un sí o un no.</summary>
    [JsonStringEnumMemberName("formato-no-valido")]
    FormatoNoValido,

    /// <summary>Se lee, pero la regla no lo admite: un NIF sin su carácter de control, una divisa sin redondeo.</summary>
    [JsonStringEnumMemberName("no-valido")]
    NoValido,

    /// <summary>Ni cliente ni proveedor: a un tercero se le vende, se le compra, o las dos cosas.</summary>
    [JsonStringEnumMemberName("ni-cliente-ni-proveedor")]
    NiClienteNiProveedor,

    /// <summary>
    /// Lo que la fila da de alta ya existe. Se contesta igual si lo que estorba está activo que si
    /// está bloqueado (art. 32 LOPDGDD, ADR-0027).
    /// </summary>
    [JsonStringEnumMemberName("ya-existe")]
    YaExiste,

    /// <summary>Otra fila anterior del mismo fichero da de alta lo mismo.</summary>
    [JsonStringEnumMemberName("repetida-en-el-fichero")]
    RepetidaEnElFichero,
}
