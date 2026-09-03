namespace Bastion.Organizacion.Contracts.Comun;

/// <summary>
/// Qué se puede hacer hoy con una fila de un maestro de Organización a la que otro módulo apunta.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son dos preguntas y no una</b>, y por eso la respuesta tiene tres valores en vez de ser un
/// <c>bool</c>. El ADR-0023 lo dejó escrito para los cuatro maestros de instalación: una fila
/// retirada «no se ofrece para operaciones nuevas, pero <b>sigue resolviendo</b> para lo que ya
/// apunta a ella». Un puerto que solo contestara «existe» dejaría dar de alta artículos con
/// unidades retiradas; uno que solo contestara «se puede usar» dejaría al histórico sin poder
/// resolver la unidad de un albarán de hace tres años. Las dos hacen falta, así que se contestan
/// juntas.
/// </para>
/// <para>
/// La forma se fija <b>aquí y ahora</b> aunque la retirada llegue en el ítem 1.7: el consumidor de
/// estos puertos es Catálogo, en el 1.8, y descubrir entonces que faltaba un estado significaría
/// cambiar el contrato con una pantalla a medias.
/// </para>
/// <para>
/// <c>NoExiste</c> es el valor cero a propósito: si algún día alguien recibe un
/// <c>default(EstadoDeMaestro)</c> por un camino que no pasó por el puerto, la respuesta que se
/// encuentra es la que no autoriza nada.
/// </para>
/// </remarks>
public enum EstadoDeMaestro
{
    /// <summary>No hay ninguna fila con ese identificador.</summary>
    NoExiste = 0,

    /// <summary>Existe y se puede usar para una operación nueva.</summary>
    SeOfreceParaLoNuevo = 1,

    /// <summary>
    /// Existe y sigue resolviendo lo que ya apunta a ella, pero no se ofrece para operaciones
    /// nuevas: una fila retirada (ADR-0023) o un tramo de impuesto que no rige en esa fecha.
    /// </summary>
    /// <remarks>
    /// «Lo viejo» es la lectura habitual, no la única: un tramo con fecha de entrada en el futuro
    /// también existe y tampoco se ofrece para una factura de hoy, y cae en este mismo valor. Las
    /// dos preguntas que contesta el puerto son «¿existe?» y «¿vale para un alta con esta fecha?»,
    /// y ese par —sí, no— tiene un solo sitio donde caber.
    /// </remarks>
    SoloResuelveLoViejo = 2,
}
