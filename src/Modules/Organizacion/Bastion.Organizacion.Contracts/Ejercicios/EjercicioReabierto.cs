using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Organizacion.Contracts.Ejercicios;

/// <summary>
/// Un ejercicio que estaba cerrado ha vuelto a admitir apuntes (R9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es el hecho que hay que poder contar dentro de dos años.</b> Cerrar un ejercicio es el curso
/// normal de las cosas y no sorprende a nadie; reabrirlo, no: vuelve a admitir movimientos en un
/// periodo que ya se dio por definitivo y del que probablemente ya se presentaron modelos. Quien
/// revise las cuentas necesita saber que ocurrió, cuándo y por qué, y esa pregunta no se le hace a
/// la fila del ejercicio —que solo dice el estado de <b>ahora</b>— sino a la traza.
/// </para>
/// <para>
/// <b>Lleva el motivo, y por eso el motivo es obligatorio.</b> Un evento de reapertura sin motivo
/// deja constancia de que pasó y ninguna de por qué, que es justo la mitad que importa. El resto de
/// las circunstancias —quién y cuándo— las pone la bandeja al volcarlo; lo que el caso de uso tiene
/// que aportar es lo que solo él sabe.
/// </para>
/// <para>
/// <b>Hoy no lo escucha nadie</b>, igual que <c>organizacion.empresa-creada</c> el día que nació, y
/// eso es correcto: quien decide contar lo que le ha pasado es el emisor. Contabilidad y el futuro
/// registro de auditoría son los consumidores previsibles, y ninguno existe.
/// </para>
/// </remarks>
/// <param name="EjercicioId">Identificador del ejercicio reabierto.</param>
/// <param name="EmpresaId">Empresa a la que pertenece (R8).</param>
/// <param name="Anio">Año con el que se conoce el ejercicio.</param>
/// <param name="Motivo">Por qué se reabrió, escrito por quien lo pidió.</param>
public sealed record EjercicioReabierto(
    Guid EjercicioId,
    Guid EmpresaId,
    int Anio,
    string Motivo) : EventoDeIntegracion
{
    /// <summary>Con qué nombre viaja este hecho en la cola.</summary>
    /// <remarks>
    /// Al lado del evento y no suelto en el <c>Modulo…</c> que lo declara, por lo mismo que
    /// <c>EmpresaCreada.Nombre</c>: el nombre es tan contrato como los campos.
    /// </remarks>
    public const string Nombre = "organizacion.ejercicio-reabierto";
}
