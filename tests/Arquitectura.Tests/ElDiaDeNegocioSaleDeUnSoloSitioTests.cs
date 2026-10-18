using Bastion.Api.Arranque;
using Bastion.BuildingBlocks.Domain.Fechas;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El día de negocio sale de <see cref="DiaDeNegocio"/> y de ningún otro sitio del backend.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hubo dieciséis</b>, en Catálogo, Inventario y Terceros, cada uno con su
/// <c>DateOnly.FromDateTime</c> sobre el reloj. Decían lo mismo porque todos lo copiaban del de al
/// lado, y el día que la regla cambie —la zona del almacén, antes de la fase 5— el que se quede
/// sin cambiar contestará otro día sin que nada se ponga rojo. Lo pidió el repaso de la fase 2, y
/// lo hizo el 2.16.
/// </para>
/// <para>
/// <b>Se lee la IL y no el texto.</b> La llamada se busca en el código compilado de todos los
/// ensamblados del backend, con Mono.Cecil, en cualquier instrucción que la nombre: una llamada,
/// un delegado, una lambda o un método asíncrono, que el compilador saca a un tipo anidado. Un
/// alias, un <c>using static</c> o un comentario no la esconden ni la inventan.
/// </para>
/// <para>
/// <b>Qué se busca, y qué se escapa.</b> La aguja es <c>DateOnly.FromDateTime</c>, que es como lo
/// escribían los dieciséis y como lo escribe el sitio. Un día montado a mano con el año, el mes y
/// el día de un instante, o un <c>DateTime.Today</c>, no casan; ninguno existe hoy en el backend,
/// así que prohibirlos sería una regla sin contraejemplo, que sale verde sin mirar nada —lo que
/// pasó con <c>System.Data</c> en el 0.12—. Tampoco lo ve el SQL: el <c>current_date</c> de la
/// función que mantiene las particiones del libro lo cuenta la nota de la zona, en
/// <c>docs/PLAN.md</c>. Es el límite, y se dice.
/// </para>
/// </remarks>
public sealed class ElDiaDeNegocioSaleDeUnSoloSitioTests
{
    /// <summary>El tipo de la aguja, derivado y no tecleado.</summary>
    private static readonly string s_tipoDeLaAguja = typeof(DateOnly).FullName!;

    /// <summary>El método de la aguja: el que convierte un instante en su día.</summary>
    private const string MetodoDeLaAguja = nameof(DateOnly.FromDateTime);

    /// <summary>El único sitio donde se puede llamar.</summary>
    private static readonly string s_sitio = typeof(DiaDeNegocio).FullName!;

    private static readonly Lazy<Lectura> s_lectura = new(Leer);

    /// <summary>
    /// El barrido lee el backend entero: cada ensamblado del bloque común y de los módulos, y el
    /// raíz de composición, y ninguno más.
    /// </summary>
    /// <remarks>
    /// El universo es lo que hay en la salida, y cada fichero tiene que ser algo que el inventario
    /// descubre o el raíz de composición. Un módulo que el descubrimiento dejara fuera no se
    /// leería, y lo que calculara por su cuenta no se vería.
    /// </remarks>
    [Fact]
    public void El_barrido_lee_el_backend_entero()
    {
        Lectura lectura = s_lectura.Value;

        string[] esperados =
        [
            .. Ensamblados.Todos.Values
                .Append(typeof(MigradorDeArranque).Assembly)
                .Select(ensamblado => Path.GetFileName(ensamblado.Location))
                .Order(StringComparer.Ordinal),
        ];

        esperados.ShouldNotBeEmpty("el inventario no descubre ningún ensamblado del backend");
        lectura.Leidos.ShouldBe(
            esperados,
            ignoreOrder: false,
            customMessage: "lo que el barrido lee de la salida no es el backend que descubre " +
            "el inventario");
        lectura.Cuerpos.ShouldBeGreaterThan(0, "el barrido no ha leído ningún cuerpo de método");
    }

    /// <summary>La pareja: la aguja está en su sitio, y en ningún otro método de él.</summary>
    /// <remarks>
    /// Sin esta, una aguja con una letra cambiada, o un lector que no recorriera las
    /// instrucciones, dejarían la regla de abajo verde para siempre. Y si el sitio deja de
    /// escribirlo así, esta se pone roja y obliga a cambiar la aguja con él.
    /// </remarks>
    [Fact]
    public void El_dia_de_negocio_se_calcula_en_su_sitio()
    {
        string[] enElSitio =
        [
            .. from hallazgo in s_lectura.Value.Hallazgos
               where hallazgo.EnElSitio
               select hallazgo.Ensamblado + ": " + hallazgo.Metodo,
        ];

        string dondeEsta = Path.GetFileName(typeof(DiaDeNegocio).Assembly.Location);

        enElSitio.ShouldBe(
            [dondeEsta + ": " + s_sitio + "::" + nameof(DiaDeNegocio.De)],
            customMessage: $"{s_tipoDeLaAguja}.{MetodoDeLaAguja} ya no está donde se calcula el " +
            "día de negocio: o la aguja está mal, o el sitio dejó de escribirlo así, y en los " +
            "dos casos la prohibición está saliendo verde sin mirar nada");
    }

    /// <summary>Nadie más convierte un instante en un día.</summary>
    [Fact]
    public void Nadie_mas_calcula_el_dia_de_negocio()
    {
        string[] fuera =
        [
            .. from hallazgo in s_lectura.Value.Hallazgos
               where !hallazgo.EnElSitio
               select hallazgo.Ensamblado + ": " + hallazgo.Metodo,
        ];

        fuera.ShouldBeEmpty(
            $"el día de negocio se calcula fuera de {s_sitio}:{Environment.NewLine}" +
            Ensamblados.Enumerar(fuera) + Environment.NewLine +
            $"Llama a {nameof(DiaDeNegocio)}.{nameof(DiaDeNegocio.Hoy)}(reloj) o a " +
            $"{nameof(DiaDeNegocio)}.{nameof(DiaDeNegocio.De)}(instante)");
    }

    private static Lectura Leer()
    {
        string propio =
            Path.GetFileName(typeof(ElDiaDeNegocioSaleDeUnSoloSitioTests).Assembly.Location);
        List<string> leidos = [];
        List<Hallazgo> hallazgos = [];
        int cuerpos = 0;

        foreach (string fichero in Directory
            .EnumerateFiles(AppContext.BaseDirectory, Inventario.Raiz + ".*.dll")
            .Order(StringComparer.Ordinal))
        {
            string nombre = Path.GetFileName(fichero);

            if (string.Equals(nombre, propio, StringComparison.Ordinal))
            {
                continue;
            }

            leidos.Add(nombre);

            using var ensamblado = AssemblyDefinition.ReadAssembly(fichero);

            // `GetTypes` aplana los anidados: ahí viven las lambdas y las máquinas de estado.
            foreach (TypeDefinition tipo in ensamblado.MainModule.GetTypes())
            {
                foreach (MethodDefinition metodo in tipo.Methods.Where(metodo => metodo.HasBody))
                {
                    cuerpos++;

                    foreach (Instruction instruccion in metodo.Body.Instructions)
                    {
                        if (instruccion.Operand is MethodReference llamado && EsLaAguja(llamado))
                        {
                            bool enElSitio = string.Equals(
                                Exterior(tipo).FullName, s_sitio, StringComparison.Ordinal);

                            string donde = tipo.FullName + "::" + metodo.Name;

                            hallazgos.Add(new Hallazgo(nombre, donde, enElSitio));
                        }
                    }
                }
            }
        }

        return new Lectura(leidos, cuerpos, hallazgos);
    }

    private static bool EsLaAguja(MethodReference llamado) =>
        string.Equals(llamado.DeclaringType.FullName, s_tipoDeLaAguja, StringComparison.Ordinal)
        && string.Equals(llamado.Name, MetodoDeLaAguja, StringComparison.Ordinal);

    /// <summary>El tipo de fuera de un anidado: el de una lambda es el de su método.</summary>
    private static TypeDefinition Exterior(TypeDefinition tipo)
    {
        while (tipo.DeclaringType is { } declarante)
        {
            tipo = declarante;
        }

        return tipo;
    }

    /// <summary>Lo que el barrido lee: los ficheros, los cuerpos de método y la aguja.</summary>
    private sealed record Lectura(
        IReadOnlyList<string> Leidos, int Cuerpos, IReadOnlyList<Hallazgo> Hallazgos);

    /// <summary>Un método que llama a la aguja, y si es del sitio.</summary>
    private sealed record Hallazgo(string Ensamblado, string Metodo, bool EnElSitio);
}
