using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using examen_incidencias.Data;
using examen_incidencias.Models;
using examen_incidencias.Services;

namespace examen_incidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OperacionesController> _logger;
    private readonly AlgoliaService _algolia;

    private const string CacheKeyIncidenciasAbiertas = "incidencias:abiertas";

    public OperacionesController(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IDistributedCache cache,
        ILogger<OperacionesController> logger,
        AlgoliaService algolia)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _algolia = algolia;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias(string? query)
    {
        List<Incidencia> abiertas;

        ViewBag.PieSocketClusterId = _configuration["PieSocket:ClusterId"];
        ViewBag.PieSocketApiKey = _configuration["PieSocket:ApiKey"];

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Buscar en Algolia y filtrar solo las abiertas en la BD
            var objectIds = await _algolia.SearchAsync(query);

            abiertas = await _context.Incidencias
                .Where(i => objectIds.Contains(i.Id) && i.Estado == EstadoIncidencia.Abierta)
                .OrderByDescending(i => i.FechaCreacion)
                .ToListAsync();
        }
        else
        {
            // Sin query: usar caché Redis
            try
            {
                var cachedData = await _cache.GetStringAsync(CacheKeyIncidenciasAbiertas);

                if (cachedData != null)
                {
                    _logger.LogInformation("Cache HIT para '{CacheKey}'", CacheKeyIncidenciasAbiertas);
                    abiertas = JsonSerializer.Deserialize<List<Incidencia>>(cachedData) ?? new List<Incidencia>();
                    ViewBag.Query = query;
                    return View(abiertas);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al acceder a Redis para la key '{CacheKey}'. Se consultará la base de datos.", CacheKeyIncidenciasAbiertas);
            }

            _logger.LogInformation("Cache MISS para '{CacheKey}'. Consultando base de datos.", CacheKeyIncidenciasAbiertas);

            abiertas = await _context.Incidencias
                .Where(i => i.Estado == EstadoIncidencia.Abierta)
                .OrderByDescending(i => i.FechaCreacion)
                .ToListAsync();

            try
            {
                var serialized = JsonSerializer.Serialize(abiertas);
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                };
                await _cache.SetStringAsync(CacheKeyIncidenciasAbiertas, serialized, cacheOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al guardar en Redis la key '{CacheKey}'. La app continúa sin caché.", CacheKeyIncidenciasAbiertas);
            }
        }

        ViewBag.Query = query;
        return View(abiertas);
    }

    // POST: /Operaciones/CerrarIncidencia
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarIncidencia(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia == null)
        {
            return NotFound();
        }

        incidencia.Estado = EstadoIncidencia.Cerrada;
        await _context.SaveChangesAsync();

        // Invalidar caché de incidencias abiertas
        try
        {
            await _cache.RemoveAsync(CacheKeyIncidenciasAbiertas);
            _logger.LogInformation("Cache invalidada: key '{CacheKey}' removida.", CacheKeyIncidenciasAbiertas);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la key '{CacheKey}' en Redis.", CacheKeyIncidenciasAbiertas);
        }

        // Sincronizar el cambio de estado en Algolia
        await _algolia.SyncIncidenciaAsync(incidencia);

        // Publicar evento a PieSocket vía REST API
        var clusterId = _configuration["PieSocket:ClusterId"];
        var apiKey = _configuration["PieSocket:ApiKey"];

        var client = _httpClientFactory.CreateClient();
        var publishUrl = $"https://{clusterId}.piesocket.com/api/publish";

        var payload = new
        {
            key = apiKey,
            secret = apiKey,
            roomId = "incidencias",
            message = JsonSerializer.Serialize(new
            {
                tipo = "IncidenciaActualizada",
                id = incidencia.Id,
                estado = "Cerrada"
            })
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            await client.PostAsync(publishUrl, jsonContent);
        }
        catch (Exception ex)
        {
            // Log pero no bloquear la operación principal
            Console.WriteLine($"Error al publicar evento PieSocket: {ex.Message}");
        }

        return RedirectToAction(nameof(Incidencias));
    }
}
