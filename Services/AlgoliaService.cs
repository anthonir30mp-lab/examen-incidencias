using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using examen_incidencias.Models;

namespace examen_incidencias.Services;

public class AlgoliaService
{
    private readonly SearchClient _client;
    private const string IndexName = "incidencias";

    public AlgoliaService(IConfiguration configuration)
    {
        var appId = configuration["Algolia:AppId"]
            ?? throw new InvalidOperationException("Algolia:AppId not configured.");
        var adminApiKey = configuration["Algolia:AdminApiKey"]
            ?? throw new InvalidOperationException("Algolia:AdminApiKey not configured.");

        _client = new SearchClient(appId, adminApiKey);
    }

    /// <summary>
    /// Sincroniza una incidencia en el índice de Algolia (crear/actualizar/cerrar).
    /// </summary>
    public async Task SyncIncidenciaAsync(Incidencia incidencia)
    {
        var record = new Dictionary<string, object>
        {
            ["objectID"] = incidencia.Id.ToString(),
            ["Estacion"] = incidencia.Estacion,
            ["Descripcion"] = incidencia.Descripcion,
            ["Estado"] = incidencia.Estado.ToString()
        };

        await _client.SaveObjectsAsync(IndexName, new List<object> { record });
    }

    /// <summary>
    /// Sincroniza múltiples incidencias en el índice de Algolia.
    /// </summary>
    public async Task SyncIncidenciasAsync(IEnumerable<Incidencia> incidencias)
    {
        var records = incidencias.Select(i => new Dictionary<string, object>
        {
            ["objectID"] = i.Id.ToString(),
            ["Estacion"] = i.Estacion,
            ["Descripcion"] = i.Descripcion,
            ["Estado"] = i.Estado.ToString()
        }).Cast<object>().ToList();

        if (records.Count > 0)
        {
            await _client.SaveObjectsAsync(IndexName, records);
        }
    }

    /// <summary>
    /// Busca en el índice de Algolia y devuelve los ObjectID que coincidan.
    /// </summary>
    public async Task<List<int>> SearchAsync(string query)
    {
        var searchParams = new SearchParams(new SearchParamsObject
        {
            Query = query
        });

        var result = await _client.SearchSingleIndexAsync<Dictionary<string, object>>(
            IndexName, searchParams);

        var ids = new List<int>();
        foreach (var hit in result.Hits)
        {
            if (hit.TryGetValue("objectID", out var objectId) &&
                int.TryParse(objectId?.ToString(), out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Verifica si el índice está vacío.
    /// </summary>
    public async Task<bool> IsIndexEmptyAsync()
    {
        try
        {
            var indices = await _client.ListIndicesAsync();
            var index = indices.Items.FirstOrDefault(i => i.Name == IndexName);
            return index == null || index.Entries == 0;
        }
        catch
        {
            // Si el índice no existe, se considera vacío
            return true;
        }
    }
}
