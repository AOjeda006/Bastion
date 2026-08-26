using Bastion.BuildingBlocks.Application.Multiempresa;
using Bastion.BuildingBlocks.Domain.Resultados;
using Bastion.Identidad.Application.Arranque;
using Bastion.Organizacion.Application.Empresas;
using Bastion.Organizacion.Contracts.Comun;
using Bastion.Organizacion.Contracts.Empresas;

namespace Bastion.Api.Arranque;

/// <summary>
/// La puerta de arranque: la primera empresa y la primera cuenta, y solo si no hay ninguna.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vive en el <i>composition root</i> porque cruza dos módulos</b> —la empresa es de
/// Organización, la cuenta es de Identidad— y ningún módulo puede orquestar al otro sin romper la
/// frontera del §4. Aquí sí: este proyecto es el único que ve a los dos.
/// </para>
/// <para>
/// <b>Todo sale de variables de entorno y no hay valores por omisión.</b> Ni un correo
/// <c>admin@…</c>, ni una contraseña «solo para desarrollo»: unas credenciales por omisión son
/// unas credenciales conocidas, y la instalación que nadie configuró es exactamente la que las
/// conservaría. Si falta cualquiera de las variables, la semilla <b>no hace nada</b> y lo dice en
/// el registro; el sistema arranca cerrado, que es el estado seguro.
/// </para>
/// </remarks>
public static partial class SemillaDeArranque
{
    /// <summary>Correo de la primera cuenta.</summary>
    public const string VariableDeCorreo = "BASTION_SEMILLA_ADMIN_CORREO";

    /// <summary>Contraseña de la primera cuenta.</summary>
    public const string VariableDeContrasena = "BASTION_SEMILLA_ADMIN_CONTRASENA";

    /// <summary>NIF de la primera empresa.</summary>
    public const string VariableDeNif = "BASTION_SEMILLA_EMPRESA_NIF";

    /// <summary>Razón social de la primera empresa.</summary>
    public const string VariableDeRazonSocial = "BASTION_SEMILLA_EMPRESA_RAZON_SOCIAL";

    /// <summary>Calle del domicilio fiscal.</summary>
    public const string VariableDeCalle = "BASTION_SEMILLA_EMPRESA_CALLE";

    /// <summary>Código postal del domicilio fiscal.</summary>
    public const string VariableDeCodigoPostal = "BASTION_SEMILLA_EMPRESA_CODIGO_POSTAL";

    /// <summary>Población del domicilio fiscal.</summary>
    public const string VariableDePoblacion = "BASTION_SEMILLA_EMPRESA_POBLACION";

    /// <summary>País del domicilio fiscal, en ISO 3166-1 alfa-2. Por omisión, <c>ES</c>.</summary>
    public const string VariableDePais = "BASTION_SEMILLA_EMPRESA_PAIS";

    private static readonly string[] s_obligatorias =
    [
        VariableDeCorreo,
        VariableDeContrasena,
        VariableDeNif,
        VariableDeRazonSocial,
        VariableDeCalle,
        VariableDeCodigoPostal,
        VariableDePoblacion,
    ];

    /// <summary>Aplica la semilla si procede.</summary>
    /// <param name="app">La aplicación ya construida.</param>
    public static async Task SembrarAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ILogger registro = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SemillaDeArranque));
        string[] faltan = [.. s_obligatorias.Where(variable => string.IsNullOrWhiteSpace(app.Configuration[variable]))];

        if (faltan.Length > 0)
        {
            // Nivel de aviso y no de error: en una instalación ya en marcha esto es lo NORMAL —la
            // semilla ya se aplicó y las variables se retiraron—. Lo que no puede pasar es que se
            // quede en silencio, porque entonces «no puedo entrar» no tendría explicación en el
            // registro.
            SemillaSinVariables(registro, string.Join(", ", faltan));

            return;
        }

        await using AsyncServiceScope alcance = app.Services.CreateAsyncScope();

        // La semilla corre ANTES de que exista nadie: no hay petición, no hay token y por tanto no
        // hay empresa activa. El ámbito lo dice a la cara y queda anotado en el registro, en vez de
        // que el filtro se encuentre con un hueco y lo rellene solo. Cubre las dos llamadas porque
        // las dos consultan: la empresa que quizá ya esté, y los usuarios que quizá ya haya.
        using IDisposable ambito = alcance.ServiceProvider
            .GetRequiredService<IInquilinoActual>()
            .SinInquilino(MotivoSinInquilino.SemillaDeArranque);

        Guid empresaId = await AsegurarEmpresaAsync(alcance.ServiceProvider, app.Configuration).ConfigureAwait(false);

        bool creada = await alcance.ServiceProvider.GetRequiredService<ISembrarAdministrador>()
            .EjecutarAsync(
                new SemillaDeAdministrador(
                    empresaId,
                    app.Configuration[VariableDeCorreo]!,
                    app.Configuration[VariableDeContrasena]!),
                CancellationToken.None)
            .ConfigureAwait(false);

        // El correo SÍ va al registro y la contraseña NO. Saber qué cuenta existe es lo que hace
        // falta para entrar la primera vez; la contraseña ya la tiene quien puso la variable.
        if (creada)
        {
            SemillaAplicada(registro, empresaId, app.Configuration[VariableDeCorreo]!);
        }
        else
        {
            SemillaOmitida(registro);
        }
    }

    // Métodos de registro generados por el compilador (`[LoggerMessage]`). No es ceremonia: la
    // llamada corriente monta un `object[]` y evalúa sus argumentos aunque el nivel esté apagado.
    // Aquí da igual —esto se ejecuta una vez al arrancar—, pero el analizador exige la misma forma
    // en todo el proyecto, y una excepción abierta «porque este sitio no es caliente» es la que
    // acaba copiándose al sitio que sí lo es.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "La semilla de arranque no se aplica: faltan las variables {Variables}.")]
    private static partial void SemillaSinVariables(ILogger logger, string variables);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Semilla aplicada: empresa {Empresa} y cuenta de administración {Correo}.")]
    private static partial void SemillaAplicada(ILogger logger, Guid empresa, string correo);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "La semilla no se aplica: ya hay usuarios dados de alta.")]
    private static partial void SemillaOmitida(ILogger logger);

    private static async Task<Guid> AsegurarEmpresaAsync(IServiceProvider servicios, IConfiguration configuracion)
    {
        Guid? existente = await servicios.GetRequiredService<IConsultaDeEmpresas>()
            .PrimeraActivaAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            return existente.Value;
        }

        Resultado<EmpresaDto> creacion = await servicios.GetRequiredService<ICrearEmpresa>()
            .EjecutarAsync(
                new CrearEmpresaDto
                {
                    Nif = configuracion[VariableDeNif]!,
                    RazonSocial = configuracion[VariableDeRazonSocial]!,
                    DomicilioFiscal = new DireccionDto
                    {
                        Calle = configuracion[VariableDeCalle]!,
                        CodigoPostal = configuracion[VariableDeCodigoPostal]!,
                        Poblacion = configuracion[VariableDePoblacion]!,
                        Pais = (configuracion[VariableDePais] ?? "ES").Trim(),
                    },
                    // La divisa y el régimen NO son variables: una pyme española tributa en euros
                    // y el régimen general es el de la inmensa mayoría. Los dos se cambian después
                    // desde la propia aplicación, que es donde se ve lo que se está eligiendo.
                    DivisaBase = "EUR",
                    RegimenDeIva = "General",
                },
                CancellationToken.None)
            .ConfigureAwait(false);

        if (!creacion.EsCorrecto)
        {
            // Revienta el arranque a propósito. Seguir dejaría el sistema en pie, sin empresa y
            // sin cuenta, y con un aviso perdido en el registro como única pista.
            throw new InvalidOperationException(
                "La semilla de arranque no ha podido crear la primera empresa: " +
                $"{creacion.Error!.Codigo}. Revise las variables {VariableDeNif} y " +
                $"{VariableDeCalle}, {VariableDeCodigoPostal}, {VariableDePoblacion}.");
        }

        return creacion.Valor.Id;
    }
}
