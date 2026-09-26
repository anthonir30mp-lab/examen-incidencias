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

    public OperacionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        var abiertas = await _context.Incidencias
            .Where(i => i.Estado == EstadoIncidencia.Abierta)
            .OrderByDescending(i => i.FechaCreacion)
            .ToListAsync();

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

        return RedirectToAction(nameof(Incidencias));
    }
}
