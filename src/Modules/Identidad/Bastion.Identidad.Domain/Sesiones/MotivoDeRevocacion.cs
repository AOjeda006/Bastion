namespace Bastion.Identidad.Domain.Sesiones;

/// <summary>Por qué dejó de valer un token de refresco.</summary>
/// <remarks>
/// Se guarda porque es información de seguridad, no contabilidad interna:
/// <see cref="ReutilizacionDetectada"/> en una familia es la huella de un token robado, y sin el
/// motivo escrito en la fila eso se pierde. Lo que NO se hace es contárselo a quien presenta el
/// token: fuera, todos los desenlaces son el mismo <c>401</c>.
/// </remarks>
public enum MotivoDeRevocacion
{
    /// <summary>El usuario cerró sesión.</summary>
    CierreDeSesion,

    /// <summary>Se presentó un token ya canjeado: hay una copia por ahí.</summary>
    ReutilizacionDetectada,

    /// <summary>La cuenta se dio de baja o cambió de contraseña.</summary>
    CuentaAlterada,

    /// <summary>La sesión cambió de empresa activa y se reemitió (§9).</summary>
    CambioDeEmpresa,
}
