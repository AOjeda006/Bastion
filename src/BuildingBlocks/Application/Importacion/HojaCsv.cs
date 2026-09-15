namespace Bastion.BuildingBlocks.Application.Importacion;

/// <summary>Las filas con datos de un fichero CSV que ya ha pasado los errores de fichero.</summary>
/// <remarks>
/// Solo las filas con algún dato. Las que traen todos los campos vacíos —las que exporta una hoja de
/// cálculo de una fila con formato y sin nada escrito— no están, aunque sí cuentan para el tope de
/// filas y para la numeración de las demás.
/// </remarks>
/// <param name="Filas">En el orden del fichero.</param>
public sealed record HojaCsv(IReadOnlyList<FilaCsv> Filas);

/// <summary>Una fila del fichero, partida en campos, con lo que ya se sabe que está mal en ella.</summary>
/// <param name="Linea">
/// El número de fila que enseña una hoja de cálculo al abrir el fichero: la cabecera es la 1, las
/// filas vacías cuentan, y un campo entre comillas con saltos de línea dentro no añade ninguna.
/// </param>
/// <param name="Campos">Los campos, ya sin las comillas que los envolvían y con las dobles deshechas.</param>
/// <param name="ComillasMalColocadas">
/// Las posiciones de los campos que tenían una comilla donde el dialecto no la admite. Su texto está
/// en <paramref name="Campos"/>, leído tal cual, pero no se debe interpretar.
/// </param>
/// <param name="CuadraConLaCabecera">Si trae exactamente tantos campos como la cabecera.</param>
public sealed record FilaCsv(
    int Linea,
    IReadOnlyList<string> Campos,
    IReadOnlySet<int> ComillasMalColocadas,
    bool CuadraConLaCabecera);
