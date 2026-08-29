using Bastion.BuildingBlocks.Application.Autorizacion;
using Bastion.BuildingBlocks.Application.Multiempresa;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bastion.BuildingBlocks.Infrastructure.Multiempresa;

/// <summary>
/// Lee la empresa activa del <i>claim</i> de la petición en curso, y lleva la cuenta de los ámbitos
/// sin inquilino abiertos.
/// </summary>
/// <remarks>
/// <para>
/// El ámbito va en un <see cref="AsyncLocal{T}"/> y no en un campo de instancia porque tiene que
/// sobrevivir a los <c>await</c> del camino que lo abre y <b>no</b> filtrarse a peticiones vecinas:
/// el host atiende varias a la vez en el mismo proceso, y un <c>static</c> a secas convertiría «la
/// semilla está sembrando» en «nadie filtra, para todos».
/// </para>
/// <para>
/// Es <c>scoped</c>: uno por petición, igual que los <c>DbContext</c> que lo consultan.
/// </para>
/// </remarks>
/// <param name="acceso">Acceso a la petición en curso.</param>
/// <param name="registro">Dónde se anota la apertura de cada ámbito sin inquilino.</param>
public sealed partial class InquilinoActual(
    IHttpContextAccessor acceso,
    ILogger<InquilinoActual> registro) : IInquilinoActual
{
    private static readonly AsyncLocal<Ambito?> s_ambito = new();

    /// <inheritdoc/>
    public bool HayEmpresaActiva => Leer() is not null;

    /// <inheritdoc/>
    public Guid? EmpresaDelFiltro => s_ambito.Value is not null
        ? null
        : Leer() ?? throw new FaltaLaEmpresaActivaException();

    /// <inheritdoc/>
    public MotivoSinInquilino? MotivoDelAmbito => s_ambito.Value?.Motivo;

    /// <inheritdoc/>
    public IDisposable SinInquilino(MotivoSinInquilino motivo)
    {
        // Se anota al abrirlo, no al cerrarlo: si lo de dentro revienta, el registro ya dice bajo
        // qué ámbito estaba corriendo, que es justo lo que hará falta para entenderlo.
        Anotar(registro, motivo);

        Ambito ambito = new(motivo, s_ambito.Value);
        s_ambito.Value = ambito;

        return ambito;
    }

    [LoggerMessage(
        EventId = 8100,
        Level = LogLevel.Information,
        Message = "Consulta sin filtro de empresa, a propósito. Motivo: {Motivo}.")]
    private static partial void Anotar(ILogger registro, MotivoSinInquilino motivo);

    private Guid? Leer()
    {
        string? valor = acceso.HttpContext?.User.FindFirst(ClaimsDeBastion.Empresa)?.Value;

        return Guid.TryParse(valor, out Guid empresaId) ? empresaId : null;
    }

    // Al cerrarse recupera el de fuera en vez de dejar el campo en nulo: anidar dos ámbitos es
    // normal —la semilla abre el suyo y por dentro llama a una comprobación de unicidad que abre
    // el suyo— y con un nulo a pelo el de fuera se cerraría también, en silencio y a mitad.
    private sealed class Ambito(MotivoSinInquilino motivo, Ambito? anterior) : IDisposable
    {
        private bool _cerrado;

        // El motivo se guarda, no solo se anota: la fila de auditoría escrita bajo este ámbito lo
        // lleva en su propia columna, que es lo que distingue «no tiene empresa porque la sembró
        // el arranque» de «no tiene empresa y nadie sabe por qué».
        public MotivoSinInquilino Motivo => motivo;

        public void Dispose()
        {
            if (_cerrado)
            {
                return;
            }

            _cerrado = true;
            s_ambito.Value = anterior;
        }
    }
}
