using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using examen_incidencias.Data;
using examen_incidencias.Models;

namespace examen_incidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OperacionesController(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        var abiertas = await _context.Incidencias
            .Where(i => i.Estado == EstadoIncidencia.Abierta)
            .OrderByDescending(i => i.FechaCreacion)
            .ToListAsync();

        ViewBag.PieSocketClusterId = _configuration["PieSocket:ClusterId"];
        ViewBag.PieSocketApiKey = _configuration["PieSocket:ApiKey"];

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

