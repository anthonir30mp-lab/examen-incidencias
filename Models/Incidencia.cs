using System.ComponentModel.DataAnnotations;

namespace examen_incidencias.Models;

public enum Prioridad
{
    Baja,
    Media,
    Alta
}

public enum EstadoIncidencia
{
    Abierta,
    Cerrada
}

public class Incidencia
{
    public int Id { get; set; }

    [Required]
    public string Estacion { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    public Prioridad Prioridad { get; set; }

    public EstadoIncidencia Estado { get; set; }

    public DateTime FechaCreacion { get; set; }
}
