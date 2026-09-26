using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using examen_incidencias.Data;
using examen_incidencias.Models;
using examen_incidencias.Services;

namespace examen_incidencias.Controllers;

[Authorize]
public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AlgoliaService _algolia;

    public OperacionesController(ApplicationDbContext context, AlgoliaService algolia)
    {
        _context = context;
        _algolia = algolia;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias(string? query)
    {
        List<Incidencia> abiertas;

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
            abiertas = await _context.Incidencias
                .Where(i => i.Estado == EstadoIncidencia.Abierta)
                .OrderByDescending(i => i.FechaCreacion)
                .ToListAsync();
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

        // Sincronizar el cambio de estado en Algolia
        await _algolia.SyncIncidenciaAsync(incidencia);

        return RedirectToAction(nameof(Incidencias));
    }
}
