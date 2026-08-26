using Bastion.BuildingBlocks.Domain.Resultados;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bastion.BuildingBlocks.Infrastructure.Errores;

/// <summary>
/// El 400 automático de <c>[ApiController]</c>, reescrito para que salga por la misma política
/// que todo lo demás y sin nada de dentro.
/// </summary>
/// <remarks>
/// <para>
/// Sin esto, la respuesta de un cuerpo que no encaja la compone MVC por su cuenta: sale sin pasar
/// por <c>CustomizeProblemDetails</c> —o sea, <b>sin identificador de traza</b>, que el §9 exige
/// en toda respuesta de error— y con el mensaje que haya generado el deserializador.
/// </para>
/// <para>
/// Y ese mensaje trae el interior por la puerta. Mandar <c>[]</c> a una acción que espera un
/// objeto contesta hoy <i>«The JSON value could not be converted to
/// Bastion.Organizacion.Contracts.Empresas.CrearEmpresaDto»</i>: el espacio de nombres, el módulo
/// y el nombre del tipo de C#. No es una traza de pila, así que no la para ningún manejador de
/// excepciones; es el camino <b>previsto</b> para un cuerpo mal formado. Lo descubrió el test que
/// pide justamente eso, <c>Un_cuerpo_que_no_es_el_que_toca_es_400_y_no_dice_por_donde_ha_roto</c>.
/// </para>
/// <para>
/// Lo que NO se toca: los mensajes de las anotaciones del contrato («la letra de control no
/// corresponde», «los tipos admitidos son…»). Esos están escritos PARA fuera y dicen qué
/// corregir; borrarlos convertiría cada 400 en un «no es válido» que obliga a ir al OpenAPI.
/// </para>
/// </remarks>
public static class EntradaNoValida
{
    /// <summary>El mismo código que usan los casos de uso para un fallo por campo.</summary>
    /// <remarks>
    /// Repetido a propósito y no compartido con las capas de aplicación: <c>Application</c> no
    /// referencia <c>Infrastructure</c> (§4). Lo que importa es que el cliente vea el MISMO
    /// <c>type</c> lo detecte quien lo detecte, y eso se comprueba en un test de contrato.
    /// </remarks>
    public const string Codigo = "datos-no-validos";

    private const string Mensaje = "Los datos enviados no son válidos. Revisa los campos indicados.";

    private const string SinForma = "El valor recibido no tiene la forma que espera esta operación.";

    /// <summary>Construye la respuesta del 400 automático.</summary>
    /// <param name="contexto">Contexto de la acción, con el estado del modelo ya validado.</param>
    public static IActionResult Respuesta(ActionContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return Traducir(contexto.ModelState).AResultadoDeAccion();
    }

    private static ErrorDeOperacion Traducir(ModelStateDictionary estado)
    {
        Dictionary<string, IReadOnlyList<string>> campos = new(StringComparer.Ordinal);

        foreach ((string clave, ModelStateEntry entrada) in estado)
        {
            if (entrada.Errors.Count == 0)
            {
                continue;
            }

            campos[Campo(clave)] = [.. entrada.Errors.Select(error => Motivo(clave, error))];
        }

        return ErrorDeOperacion.Validacion(Codigo, Mensaje, campos);
    }

    // La ruta JSON de la raíz, para cuando el documento revienta tan pronto que ni eso se sabe.
    private static string Campo(string clave) => string.IsNullOrEmpty(clave) ? "$" : clave;

    private static string Motivo(string clave, ModelError error) =>
        EsDeForma(clave, error) || string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? SinForma
            : error.ErrorMessage;

    // Qué distingue un fallo de FORMA de uno de contrato, que es lo único que hay que distinguir
    // aquí. No sirve mirar `Exception`: MVC publica a propósito el texto de las
    // `InputFormatterException` y las deja con la excepción a nulo, así que por ahí las dos clases
    // son idénticas. Lo que sí las separa es la CLAVE: el formateador de JSON registra el error
    // bajo la ruta del documento (`$`, `$.nif`), que es lo que el cliente ha escrito; el enlace de
    // modelo y las anotaciones usan el nombre del campo. Se cubre además el caso con excepción,
    // que es el de un conversor que ha estallado.
    private static bool EsDeForma(string clave, ModelError error) =>
        error.Exception is not null || clave.Length == 0 || clave[0] == '$';
}
