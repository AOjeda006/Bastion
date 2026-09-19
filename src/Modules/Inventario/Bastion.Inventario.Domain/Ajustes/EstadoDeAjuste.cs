namespace Bastion.Inventario.Domain.Ajustes;

/// <summary>Los tres estados por los que pasa un ajuste de existencias.</summary>
/// <remarks>
/// Es un enumerado <b>propio</b> y no uno compartido entre documentos: el recuento tendrá
/// <c>EnCurso</c> porque contar lleva tiempo y la transferencia tendrá <c>Enviada</c> y
/// <c>Recibida</c> porque el stock en tránsito existe mientras vuela. Compartir los estados
/// obligaría a cada documento a arrastrar valores que no puede alcanzar, y convertiría «esta
/// transición no existe» —un error del compilador— en «no está permitida para este tipo», que se
/// configura mal en silencio.
/// </remarks>
public enum EstadoDeAjuste
{
    /// <summary>Se está escribiendo. No ha movido el libro y no tiene número.</summary>
    Borrador = 1,

    /// <summary>Confirmado: sus líneas ya son filas del libro, en la misma transacción.</summary>
    Confirmado = 2,

    /// <summary>
    /// <b>Anulado por un inverso</b>, no borrado. Un ajuste confirmado no se deshace: se corrige
    /// con otro documento que mueve el libro al revés (R2), y el original se queda visible con
    /// este estado. El inverso es un documento confirmado de pleno derecho, no una marca.
    /// </summary>
    Anulado = 3,
}
