using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using examen_incidencias.Data;
using examen_incidencias.Models;
using examen_incidencias.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AlgoliaService>();

var app = builder.Build();

// Seed data
await SeedDataAsync(app.Services);

// Sincronizar índice de Algolia con datos del seed (solo si está vacío)
await SyncAlgoliaIndexAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// --- Seed ---
static async Task SeedDataAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // Crear rol Supervisor
    if (!await roleManager.RoleExistsAsync("Supervisor"))
    {
        await roleManager.CreateAsync(new IdentityRole("Supervisor"));
    }

    // Crear usuario supervisor
    const string supervisorEmail = "supervisor@ejemplo.com";
    const string supervisorPassword = "Supervisor123!";

    if (await userManager.FindByEmailAsync(supervisorEmail) == null)
    {
        var supervisor = new IdentityUser
        {
            UserName = supervisorEmail,
            Email = supervisorEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(supervisor, supervisorPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(supervisor, "Supervisor");
        }
    }

    // Crear incidencias de ejemplo (solo si no existen)
    if (!context.Incidencias.Any())
    {
        context.Incidencias.AddRange(
            new Incidencia
            {
                Estacion = "Estacion Centro",
                Descripcion = "Bicicleta con cadena rota en anclaje 3",
                Prioridad = Prioridad.Alta,
                Estado = EstadoIncidencia.Abierta,
                FechaCreacion = DateTime.Now.AddDays(-3)
            },
            new Incidencia
            {
                Estacion = "Estacion Norte",
                Descripcion = "Panel de información dañado por vandalismo",
                Prioridad = Prioridad.Media,
                Estado = EstadoIncidencia.Abierta,
                FechaCreacion = DateTime.Now.AddDays(-2)
            },
            new Incidencia
            {
                Estacion = "Estacion Sur",
                Descripcion = "Sistema de anclaje no libera bicicletas",
                Prioridad = Prioridad.Alta,
                Estado = EstadoIncidencia.Abierta,
                FechaCreacion = DateTime.Now.AddDays(-1)
            },
            new Incidencia
            {
                Estacion = "Estacion Este",
                Descripcion = "Falta señalización de la estación",
                Prioridad = Prioridad.Baja,
                Estado = EstadoIncidencia.Abierta,
                FechaCreacion = DateTime.Now.AddHours(-5)
            },
            new Incidencia
            {
                Estacion = "Estacion Centro",
                Descripcion = "Neumático desinflado en bicicleta #12",
                Prioridad = Prioridad.Media,
                Estado = EstadoIncidencia.Cerrada,
                FechaCreacion = DateTime.Now.AddDays(-7)
            }
        );

        await context.SaveChangesAsync();
    }
}

// --- Algolia Sync ---
static async Task SyncAlgoliaIndexAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var services = scope.ServiceProvider;

    var algolia = services.GetRequiredService<AlgoliaService>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    if (await algolia.IsIndexEmptyAsync())
    {
        var incidencias = await context.Incidencias.ToListAsync();
        await algolia.SyncIncidenciasAsync(incidencias);
    }
}
