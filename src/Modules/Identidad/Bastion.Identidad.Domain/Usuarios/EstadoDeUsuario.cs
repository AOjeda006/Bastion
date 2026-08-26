namespace Bastion.Identidad.Domain.Usuarios;

/// <summary>Estado de una cuenta de usuario (R16).</summary>
/// <remarks>
/// Dos estados, no un booleano <c>activo</c>: el bloqueo lleva fecha y la lleva porque el plazo
/// de conservación se cuenta desde ella. Y no hay un tercer estado «borrado», porque una cuenta
/// no se borra: un usuario es una persona física, así que el artículo 32 de la LOPDGDD alcanza
/// de lleno a esta tabla —hay que poder demostrar quién hizo qué durante el plazo de
/// prescripción, y eso exige que la fila siga existiendo—.
/// </remarks>
public enum EstadoDeUsuario
{
    /// <summary>La cuenta puede iniciar sesión.</summary>
    Activo,

    /// <summary>Baja lógica: la cuenta existe, conserva su rastro y no puede iniciar sesión.</summary>
    Bloqueado,
}
