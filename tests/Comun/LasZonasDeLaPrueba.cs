using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Fechas;
using Bastion.BuildingBlocks.Domain.Autorizacion;
using Bastion.BuildingBlocks.Domain.Fechas;

namespace Bastion.Pruebas.Comun;

/// <summary>
/// Las zonas de una prueba que no tiene Organización detrás: la de la empresa, y la propia de los
/// almacenes que la tengan. El almacén que no tiene la suya hereda la de la empresa, como en la base.
/// </summary>
/// <remarks>
/// Apunta a quién se le preguntó, para que un caso pueda afirmar que la zona se pidió a la empresa
/// o al almacén que tocaba, y no a otro.
/// </remarks>
/// <param name="deLaEmpresa">La zona de la empresa.</param>
/// <param name="propias">La zona propia de cada almacén que la tenga.</param>
internal sealed class ZonasFijas(ZonaHoraria deLaEmpresa, IReadOnlyDictionary<Guid, ZonaHoraria>? propias = null)
    : IZonasDeLaEmpresa
{
    /// <summary>Las empresas por las que se preguntó, en orden.</summary>
    public List<Guid> EmpresasPreguntadas { get; } = [];

    /// <summary>Los almacenes por los que se preguntó, en orden.</summary>
    public List<Guid> AlmacenesPreguntados { get; } = [];

    public Task<ZonaHoraria> DeLaEmpresaAsync(Guid empresaId, CancellationToken cancelacion)
    {
        EmpresasPreguntadas.Add(empresaId);

        return Task.FromResult(deLaEmpresa);
    }

    public Task<ZonaHoraria> DelAlmacenAsync(Guid almacenId, CancellationToken cancelacion)
    {
        AlmacenesPreguntados.Add(almacenId);

        return Task.FromResult(propias?.GetValueOrDefault(almacenId) ?? deLaEmpresa);
    }

    public Task<IReadOnlyList<ZonaHoraria>> TodasAsync(Guid empresaId, CancellationToken cancelacion)
    {
        EmpresasPreguntadas.Add(empresaId);

        return Task.FromResult<IReadOnlyList<ZonaHoraria>>(
            [.. (propias?.Values ?? []).Prepend(deLaEmpresa).Distinct()]);
    }
}

/// <summary>
/// «Hoy» en la empresa de una prueba: el <see cref="HoyEnLaEmpresa"/> de verdad, con su zona fija y
/// el reloj de la prueba.
/// </summary>
/// <remarks>
/// Cada caso dice su zona: ninguno hereda la de la máquina, ni la da por supuesta.
/// </remarks>
internal static class HoyDeLaPrueba
{
    /// <summary>La empresa por la que se pregunta, que en una prueba sin Organización da igual.</summary>
    public static readonly Guid Empresa = new("3a5b0000-0000-4000-8000-000000000035");

    /// <summary>Hoy en la empresa, con una zona y un reloj.</summary>
    /// <param name="zona">La zona de la empresa.</param>
    /// <param name="reloj">El reloj de la prueba.</param>
    public static IHoyEnLaEmpresa En(ZonaHoraria zona, TimeProvider reloj) => En(new ZonasFijas(zona), reloj);

    /// <summary>Hoy en la empresa, con sus zonas y un reloj.</summary>
    /// <param name="zonas">Las zonas de la empresa y de sus almacenes.</param>
    /// <param name="reloj">El reloj de la prueba.</param>
    public static IHoyEnLaEmpresa En(IZonasDeLaEmpresa zonas, TimeProvider reloj) =>
        new HoyEnLaEmpresa(zonas, new DeLaEmpresa(), reloj);

    /// <summary>Quien pregunta, que solo dice su empresa.</summary>
    private sealed class DeLaEmpresa : IUsuarioActual
    {
        public bool EstaAutenticado => true;

        public Guid UsuarioId => throw new NotSupportedException("El «hoy» de la empresa no pregunta quién.");

        public Guid EmpresaId => Empresa;

        public bool Tiene(Permiso permiso) =>
            throw new NotSupportedException("El «hoy» de la empresa no pregunta por permisos.");
    }
}
