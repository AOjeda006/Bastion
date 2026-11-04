using System.Reflection;
using Bastion.Api.Arranque;
using Bastion.BuildingBlocks.Domain.Fechas;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

namespace Bastion.Arquitectura.Tests;

/// <summary>
/// El día de negocio sale de <see cref="DiaDeNegocio"/> y de ningún otro sitio del backend, y sale
/// siempre con su zona.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hubo dieciséis</b>, en Catálogo, Inventario y Terceros, cada uno con su
/// <c>DateOnly.FromDateTime</c> sobre el reloj. Decían lo mismo porque todos lo copiaban del de al
/// lado, y el día que la regla cambiara el que se quedara sin cambiar contestaría otro día sin que
/// nada se pusiera rojo. Lo pidió el repaso de la fase 2, y lo hizo el 2.16. La regla cambió en el
/// 3.5, del día UTC al de la zona (ADR-0079), y cambió en un solo sitio.
/// </para>
/// <para>
/// <b>Se lee la IL y no el texto.</b> Las llamadas se buscan en el código compilado de todos los
/// ensamblados del backend, con Mono.Cecil, en cualquier instrucción que las nombre: una llamada,
/// un delegado, una lambda o un método asíncrono, que el compilador saca a un tipo anidado. Un
/// alias, un <c>using static</c> o un comentario no la esconden ni la inventan.
/// </para>
/// <para>
/// <b>Dos agujas desde el 3.5</b>: <c>DateOnly.FromDateTime</c>, que es como lo escribían los
/// dieciséis, y las conversiones de <c>TimeZoneInfo</c> —todas las que empiezan por
/// <c>ConvertTime</c>—, que es como se llega a la hora de una zona. La pareja afirma que las dos
/// están en el sitio, y en qué métodos.
/// </para>
/// <para>
/// <b>Qué se escapa.</b> Un día montado a mano con el año, el mes y el día de un instante, o un
/// <c>DateTime.Today</c>, no casan; ninguno existe hoy en el backend, así que prohibirlos sería una
/// regla sin contraejemplo, que sale verde sin mirar nada —lo que pasó con <c>System.Data</c> en el
/// 0.12—. Tampoco lo ve el SQL: el <c>current_date</c> de la función que mantiene las particiones
/// del libro lo cuenta la nota de la zona, en <c>docs/PLAN.md</c>. Es el límite, y se dice.
/// </para>
/// </remarks>
public sealed class ElDiaDeNegocioSaleDeUnSoloSitioTests
{
    /// <summary>Las agujas, derivadas y no tecleadas: el tipo y el principio del nombre.</summary>
    private static readonly Aguja[] s_agujas =
    [
        new(typeof(DateOnly).FullName!, nameof(DateOnly.FromDateTime)),
        new(typeof(TimeZoneInfo).FullName!, nameof(TimeZoneInfo.ConvertTime)),
    ];

    /// <summary>El único sitio donde se pueden llamar.</summary>
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

    /// <summary>La pareja: las agujas están en su sitio, en los métodos que las usan.</summary>
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
               select hallazgo.Ensamblado + ": " + hallazgo.Llamada + " en " + hallazgo.Metodo,
        ];

        string dondeEsta = Path.GetFileName(typeof(DiaDeNegocio).Assembly.Location) + ": ";
        string de = " en " + s_sitio + "::" + nameof(DiaDeNegocio.De);
        string inicio = " en " + s_sitio + "::" + nameof(DiaDeNegocio.Inicio);

        enElSitio.ShouldBe(
            [
                dondeEsta + typeof(DateOnly).FullName + "::" + nameof(DateOnly.FromDateTime) + de,
                dondeEsta + typeof(TimeZoneInfo).FullName + "::" + nameof(TimeZoneInfo.ConvertTime) + de,
                dondeEsta + typeof(TimeZoneInfo).FullName + "::" + nameof(TimeZoneInfo.ConvertTimeToUtc) + inicio,
            ],
            ignoreOrder: true,
            customMessage: "las agujas ya no están donde se calcula el día de negocio: o una aguja " +
            "está mal, o el sitio dejó de escribirlo así, y en los dos casos la prohibición está " +
            "saliendo verde sin mirar nada");
    }

    /// <summary>Nadie más convierte un instante en un día, ni en la hora de una zona.</summary>
    [Fact]
    public void Nadie_mas_calcula_el_dia_de_negocio()
    {
        string[] fuera =
        [
            .. from hallazgo in s_lectura.Value.Hallazgos
               where !hallazgo.EnElSitio
               select hallazgo.Ensamblado + ": " + hallazgo.Llamada + " en " + hallazgo.Metodo,
        ];

        fuera.ShouldBeEmpty(
            $"el día de negocio se calcula fuera de {s_sitio}:{Environment.NewLine}" +
            Ensamblados.Enumerar(fuera) + Environment.NewLine +
            "Pide la zona a IZonasDeLaEmpresa y llama a " +
            $"{nameof(DiaDeNegocio)}.{nameof(DiaDeNegocio.De)}(instante, zona) o a " +
            $"{nameof(DiaDeNegocio)}.{nameof(DiaDeNegocio.Hoy)}(reloj, zona)");
    }

    /// <summary>
    /// Todo lo que publica el sitio recibe la zona: no hay un día sin zona que alguien pueda pedir
    /// por comodidad, y que sería el UTC de antes con otro nombre.
    /// </summary>
    /// <remarks>
    /// La pareja va dentro: entre lo que se mira están el día de un instante y el de hoy, y el de
    /// hoy se reconoce por su tipo. Una zona es una, o cualquier colección de ellas.
    /// </remarks>
    [Fact]
    public void Todo_lo_que_da_un_dia_recibe_su_zona()
    {
        MethodInfo[] publicos = typeof(DiaDeNegocio).GetMethods(BindingFlags.Public | BindingFlags.Static);

        publicos.Select(metodo => metodo.Name)
            .ShouldContain(nameof(DiaDeNegocio.De), "el barrido de los métodos no ve el sitio");
        publicos.Where(metodo => metodo.ReturnType == typeof(DateOnly)).Select(metodo => metodo.Name)
            .ShouldContain(nameof(DiaDeNegocio.Hoy), "el barrido no reconoce un día por su tipo");

        string[] sinZona =
        [
            .. from metodo in publicos
               where !metodo.GetParameters().Any(parametro => EsZona(parametro.ParameterType))
               select metodo.Name,
        ];

        sinZona.ShouldBeEmpty($"{s_sitio} publica métodos que no reciben la zona");
    }

    private static bool EsZona(Type tipo) =>
        tipo == typeof(ZonaHoraria) || typeof(IEnumerable<ZonaHoraria>).IsAssignableFrom(tipo);

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
                        if (instruccion.Operand is MethodReference llamado
                            && s_agujas.Any(aguja => aguja.Casa(llamado)))
                        {
                            bool enElSitio = string.Equals(
                                Exterior(tipo).FullName, s_sitio, StringComparison.Ordinal);

                            hallazgos.Add(new Hallazgo(
                                nombre,
                                llamado.DeclaringType.FullName + "::" + llamado.Name,
                                tipo.FullName + "::" + metodo.Name,
                                enElSitio));
                        }
                    }
                }
            }
        }

        return new Lectura(leidos, cuerpos, hallazgos);
    }

    /// <summary>El tipo de fuera de un anidado: el de una lambda es el de su método.</summary>
    private static TypeDefinition Exterior(TypeDefinition tipo)
    {
        while (tipo.DeclaringType is { } declarante)
        {
            tipo = declarante;
        }

        return tipo;
    }

    /// <summary>Una aguja: el tipo, exacto, y el principio del nombre del método.</summary>
    private sealed record Aguja(string Tipo, string Metodo)
    {
        public bool Casa(MethodReference llamado) =>
            string.Equals(llamado.DeclaringType.FullName, Tipo, StringComparison.Ordinal)
            && llamado.Name.StartsWith(Metodo, StringComparison.Ordinal);
    }

    /// <summary>Lo que el barrido lee: los ficheros, los cuerpos de método y las agujas.</summary>
    private sealed record Lectura(
        IReadOnlyList<string> Leidos, int Cuerpos, IReadOnlyList<Hallazgo> Hallazgos);

    /// <summary>Un método que llama a una aguja, a cuál, y si es del sitio.</summary>
    private sealed record Hallazgo(string Ensamblado, string Llamada, string Metodo, bool EnElSitio);
}
