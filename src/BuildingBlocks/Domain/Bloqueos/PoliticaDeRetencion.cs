using System.Globalization;

namespace Bastion.BuildingBlocks.Domain.Bloqueos;

/// <summary>
/// Cuánto dura un bloqueo del artículo 32 antes de vencer, y qué bloqueos no vencen nunca.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un bloqueo sin fin no cumple el artículo 32: lo incumple por el otro lado.</b> Ese artículo
/// obliga a reservar los datos cuya supresión procede «durante el plazo de prescripción de las
/// acciones que pudieran derivarse», y un plazo que no termina convierte una conservación acotada
/// en una conservación indefinida — que es exactamente lo que la supresión venía a evitar. Por eso
/// el bloqueo tiene fecha de vencimiento y no solo fecha de inicio.
/// </para>
/// <para>
/// <b>El plazo cuelga del motivo y no de un número global</b>, porque los dos motivos que hay son
/// de naturalezas distintas. <see cref="MotivoDeBloqueo.SupresionSolicitada"/> reserva datos
/// personales y vence: pasado el plazo, procede destruirlos.
/// <see cref="MotivoDeBloqueo.CeseDeUso"/> <b>no vence nunca</b>, y eso no es una infracción: un
/// almacén retirado se conserva por razón contable —el histórico de valoración apunta a él para
/// siempre— y sus datos no son de nadie. Poner fecha de caducidad a los dos por igual pondría a
/// destruir un dato mercantil que hay que guardar.
/// </para>
/// <para>
/// <b>Seis años por omisión, y el número tiene procedencia.</b> Es el plazo del artículo 30 del
/// Código de Comercio para libros y documentación del negocio, que es el suelo más largo de los que
/// le aplican a una pyme española: la prescripción tributaria son cuatro (art. 66 LGT) y las
/// acciones personales cinco (art. 1964 CC). Se elige el más largo porque un bloqueo que venciera
/// antes que la obligación de conservar destruiría lo que todavía hay que poder enseñar.
/// </para>
/// <para>
/// <b>Y es configurable por instalación</b>, a diferencia de <c>OpcionesDeLaBandeja</c>, que dice
/// por escrito que no hay variable para sus valores «porque nadie ha necesitado cambiarlos sin
/// recompilar». Aquí el caso existe y no es hipotético: el plazo depende del asesoramiento legal de
/// cada empresa y del sector, y una instalación no puede necesitar recompilar el ERP para ajustar
/// un plazo de retención que le ha dicho su abogado.
/// </para>
/// </remarks>
public sealed class PoliticaDeRetencion
{
    /// <summary>Nombre de la variable con el plazo, en años.</summary>
    public const string VariableDelPlazo = "BASTION_PLAZO_DE_SUPRESION_ANIOS";

    /// <summary>Plazo por omisión, en años: el del artículo 30 del Código de Comercio.</summary>
    public const int AniosPorOmision = 6;

    /// <summary>Plazo mínimo admitido, en años.</summary>
    /// <remarks>
    /// Cero significaría «destrúyelo ya», que no es un plazo de prescripción sino su ausencia, y
    /// dejaría el bloqueo vencido en el instante de nacer.
    /// </remarks>
    public const int AniosMinimos = 1;

    /// <summary>Plazo máximo admitido, en años.</summary>
    /// <remarks>
    /// Treinta años no es un límite legal: es el tope por encima del cual un número casi seguro que
    /// es un error de tecleo —un plazo en meses metido en la casilla de los años— y no una decisión.
    /// Arrancar con él puesto guardaría datos personales tres décadas sin que nadie lo hubiera
    /// decidido.
    /// </remarks>
    public const int AniosMaximos = 30;

    private PoliticaDeRetencion(int anios) => AniosDeSupresion = anios;

    /// <summary>Años que dura la reserva del artículo 32 desde la fecha del bloqueo.</summary>
    public int AniosDeSupresion { get; }

    /// <summary>La política por omisión, sin nada configurado.</summary>
    public static PoliticaDeRetencion PorOmision() => new(AniosPorOmision);

    /// <summary>Construye la política a partir del valor ya leído de la configuración.</summary>
    /// <remarks>
    /// Ausente o en blanco vale y significa «el de omisión»: a diferencia de un secreto, aquí un
    /// valor por omisión es una decisión defendible y escrita, no una puerta abierta. Lo que no
    /// vale es un valor <b>puesto y mal</b>: eso es alguien intentando configurar algo y
    /// consiguiendo otra cosa, y se para al arrancar.
    /// </remarks>
    /// <param name="plazo">Valor de <see cref="VariableDelPlazo"/>, o <c>null</c> si no está.</param>
    /// <exception cref="InvalidOperationException">Si está puesto y no es un entero del rango.</exception>
    public static PoliticaDeRetencion De(string? plazo)
    {
        if (string.IsNullOrWhiteSpace(plazo))
        {
            return PorOmision();
        }

        // Cultura invariante y no la de la máquina: el mismo despliegue tiene que leer «6» igual
        // en un servidor en castellano que en uno en inglés.
        if (!int.TryParse(plazo, NumberStyles.None, CultureInfo.InvariantCulture, out int anios)
            || anios < AniosMinimos
            || anios > AniosMaximos)
        {
            throw new InvalidOperationException(
                $"La variable {VariableDelPlazo} tiene que ser un número entero de años entre " +
                $"{AniosMinimos.ToString(CultureInfo.InvariantCulture)} y " +
                $"{AniosMaximos.ToString(CultureInfo.InvariantCulture)}. Sin ella, el plazo es de " +
                $"{AniosPorOmision.ToString(CultureInfo.InvariantCulture)} años (art. 30 del " +
                "Código de Comercio).");
        }

        return new PoliticaDeRetencion(anios);
    }

    /// <summary>Cuándo vence ese bloqueo, o <c>null</c> si no vence.</summary>
    /// <remarks>
    /// <b>El <c>null</c> es información, no un hueco.</b> Dice «este bloqueo no caduca», que es
    /// cierto y distinto de «no se sabe cuándo caduca». Quien lo pinte tiene que escribir eso y no
    /// dejar la celda vacía.
    /// </remarks>
    /// <param name="bloqueo">El estado de bloqueo de una entidad.</param>
    public DateTimeOffset? VenceEn(Bloqueo bloqueo)
    {
        ArgumentNullException.ThrowIfNull(bloqueo);

        if (!bloqueo.EstaBloqueado || bloqueo.Desde is not DateTimeOffset desde)
        {
            return null;
        }

        // Un `switch` exhaustivo y no un `if` sobre el motivo que hoy vence: el día que aparezca un
        // tercer motivo de bloqueo, esto lanza en su primera ejecución y obliga a decidir si vence
        // o no. Con un `if`, el motivo nuevo heredaría en silencio el «no vence», que es la
        // respuesta que nadie habría tomado.
        return bloqueo.Motivo switch
        {
            MotivoDeBloqueo.SupresionSolicitada => desde.AddYears(AniosDeSupresion),
            MotivoDeBloqueo.CeseDeUso => null,
            _ => throw new InvalidOperationException(
                $"El motivo de bloqueo {bloqueo.Motivo} no dice si su bloqueo vence. Un motivo " +
                "nuevo tiene que decidirlo aquí: el art. 32 obliga a acotar la conservación de lo " +
                "que reserva, y heredar «no vence» sin decidirlo la deja indefinida."),
        };
    }
}
