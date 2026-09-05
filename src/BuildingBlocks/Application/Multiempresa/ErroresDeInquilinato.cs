using Bastion.BuildingBlocks.Domain.Resultados;

namespace Bastion.BuildingBlocks.Application.Multiempresa;

/// <summary>
/// Los desenlaces fallidos que dependen de la empresa del <i>claim</i> y no del módulo que los
/// encuentra.
/// </summary>
/// <remarks>
/// <para>
/// <b>Está en el bloque común desde el ítem 1.5, y el motivo es el código.</b> «La empresa con la
/// que estás operando ya no opera» es el mismo hecho lo descubra Organización dando de alta un
/// almacén o Terceros dando de alta un cliente: la sesión apunta a una empresa que se bloqueó, y
/// lo que hay que hacer es volver a entrar. Con una constante por módulo habría un
/// <c>type</c> por módulo para una sola situación, el frontal necesitaría un texto para cada uno,
/// y el día que se añadiera el sexto módulo nadie se acordaría de escribir el sexto texto.
/// </para>
/// <para>
/// Vive en <c>Application</c> y no en <c>Domain</c> porque no es una invariante de ninguna
/// entidad: es el desenlace de un caso de uso que ha mirado el <i>claim</i>.
/// </para>
/// </remarks>
public static class ErroresDeInquilinato
{
    /// <summary>La empresa del <i>claim</i> no existe o está bloqueada.</summary>
    /// <remarks>
    /// Sin identificador en el mensaje, y no por descuido: quien recibe esto no ha escrito ninguna
    /// empresa —le vino en el token—, así que repetírsela no le ayuda a corregir nada. Lo que
    /// necesita saber es que su sesión apunta a una empresa que ya no opera y que tiene que volver
    /// a entrar.
    /// </remarks>
    public static ErrorDeOperacion EmpresaActivaNoOperativa() => ErrorDeOperacion.Conflicto(
        "empresa-activa-no-operativa",
        "La empresa con la que está operando no existe o está bloqueada. Vuelva a iniciar sesión.");
}
