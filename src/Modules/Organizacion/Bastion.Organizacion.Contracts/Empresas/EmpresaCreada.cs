using Bastion.BuildingBlocks.Domain.Eventos;

namespace Bastion.Organizacion.Contracts.Empresas;

/// <summary>
/// Se ha dado de alta una empresa. El primer evento de integración real del sistema (R12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué este y no otro.</b> Es el hecho al que más módulos van a querer reaccionar en cuanto
/// existan: Contabilidad tiene que sembrarle su plan contable, Organización sus series y su
/// ejercicio, Notificaciones dar la bienvenida. Ninguno de esos existe todavía, y por eso hoy este
/// evento se publica <b>sin que nadie lo escuche</b> — que es el comportamiento correcto y está
/// probado: quien decide contar lo que le ha pasado es el emisor, no el receptor.
/// </para>
/// <para>
/// <b>Lleva lo que un consumidor necesita para trabajar sin volver a preguntar</b>, y nada más. No
/// lleva el domicilio fiscal: es un dato que cambia y que quien lo necesite debe leer al usarlo,
/// no una copia congelada dentro de un mensaje. Y no lleva el estado, porque una empresa recién
/// creada está activa por definición.
/// </para>
/// </remarks>
/// <param name="EmpresaId">Identificador de la empresa recién creada.</param>
/// <param name="Nif">NIF, ya normalizado y con su carácter de control comprobado.</param>
/// <param name="RazonSocial">Razón social o nombre del empresario individual.</param>
/// <param name="DivisaBase">Divisa base en ISO 4217.</param>
public sealed record EmpresaCreada(
    Guid EmpresaId,
    string Nif,
    string RazonSocial,
    string DivisaBase) : EventoDeIntegracion
{
    /// <summary>
    /// Con qué nombre viaja este hecho en la cola.
    /// </summary>
    /// <remarks>
    /// Está aquí, al lado del evento, y no suelto en el <c>Modulo…</c> que lo declara: el nombre
    /// es tan contrato como los campos —es lo que hay escrito en las filas que ya están en la
    /// tabla— y separarlo del tipo permitiría cambiarlo sin ver de qué se está hablando. Quien lo
    /// declara sigue siendo el módulo que lo emite; lo que no hace es inventarse la cadena.
    /// </remarks>
    public const string Nombre = "organizacion.empresa-creada";
}
