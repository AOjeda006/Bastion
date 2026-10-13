using System.Buffers.Binary;

namespace Bastion.Inventario.Infrastructure.Persistencia.Existencias;

/// <summary>
/// Las dos claves <c>int4</c> del cerrojo consultivo de las instantáneas de una empresa
/// (ADR-0063 §5): el compartido de quien anota y el exclusivo del recálculo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos <c>int4</c> y no un <c>int8</c></b>, porque PostgreSQL separa los dos espacios: la
/// bandeja usa una sola clave de 64 bits, y estas dos no pueden chocar con ella. Lo dice su
/// documentación, en <i>Advisory Lock Functions</i>.
/// </para>
/// <para>
/// <b>La empresa se pliega aquí y no en el motor.</b> <c>hashtext</c> es interna de PostgreSQL y no
/// promete el mismo valor entre versiones, y dos instancias con dos plegados distintos tomarían dos
/// cerrojos distintos para la misma empresa: no se esperarían. Este se fija en el carril rápido con
/// valores escritos a mano, así que cambiarlo pone un caso rojo.
/// </para>
/// <para>
/// <b>Dos empresas con el mismo plegado se esperan entre sí</b>, y eso es perder paralelismo, no
/// corrección: el exclusivo de una frena a los compartidos de la otra, y nada más.
/// </para>
/// <para>
/// No lleva SQL. Las sentencias que toman el cerrojo viven en <see cref="LaProyeccionDelLibro"/> y
/// en <see cref="LasInstantaneasMensuales"/>, que son los dos sitios que ya tienen el SQL crudo
/// declarado.
/// </para>
/// </remarks>
internal static class ClaveDelCerrojoDeLasInstantaneas
{
    /// <summary>La primera clave: «Inv1» en ASCII, que nombra las instantáneas de Inventario.</summary>
    internal const int Espacio = 0x496E7631;

    /// <summary>La segunda clave: la empresa, plegada a 32 bits.</summary>
    /// <remarks>
    /// Un <c>XOR</c> de las cuatro palabras de 32 bits del <see cref="Guid"/>, leídas en
    /// <i>little-endian</i> de <see cref="Guid.ToByteArray()"/>, que no depende de la máquina.
    /// </remarks>
    /// <param name="empresaId">La empresa.</param>
    /// <returns>La segunda clave del cerrojo de esa empresa.</returns>
    internal static int DeLaEmpresa(Guid empresaId)
    {
        ReadOnlySpan<byte> bytes = empresaId.ToByteArray();

        return BinaryPrimitives.ReadInt32LittleEndian(bytes[..4])
            ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[4..8])
            ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12])
            ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
    }
}
