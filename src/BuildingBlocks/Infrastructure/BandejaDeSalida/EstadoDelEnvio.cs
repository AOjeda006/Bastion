namespace Bastion.BuildingBlocks.Infrastructure.BandejaDeSalida;

/// <summary>En qué punto está un evento de la bandeja de salida.</summary>
/// <remarks>
/// Se guarda como <b>texto</b>, igual que el resto de enumerados del sistema: por su ordinal
/// dejaría de significar nada en cuanto alguien reordenase esta lista, y estas filas duran más que
/// el código que las escribió.
/// </remarks>
public enum EstadoDelEnvio
{
    /// <summary>Escrito y todavía sin publicar. Es el estado con el que nace.</summary>
    Pendiente,

    /// <summary>Entregado a todos sus manejadores sin que ninguno fallara.</summary>
    Publicado,

    /// <summary>
    /// Ha fallado tantas veces seguidas que se le deja de dar vueltas.
    /// </summary>
    /// <remarks>
    /// Es la unidad de aislamiento del fallo: sin ella, un evento que su manejador nunca podrá
    /// atender —un dato que no existe, un error de programación— o bloquea la cola para siempre,
    /// o se reintenta en bucle a razón de una vuelta cada pocos segundos hasta que alguien mira
    /// el registro. Aparcarlo cuesta el orden <b>de ese evento</b> y salva el de todos los demás.
    /// </remarks>
    Aparcado,
}
