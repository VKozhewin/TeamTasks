using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;


var builder = WebApplication.CreateBuilder(args);

var runtimeFolder = AppContext.BaseDirectory; 
var secureDbPath = Path.Combine(runtimeFolder, "tasks.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={secureDbPath}"));

// builder.Services.AddDbContext<AppDbContext>(options =>
//options.UseSqlite(@"Data Source=d:\Project\tasks.db"));



builder.Services.AddSignalR();  // SignalR

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(origin => true) // любые подключения
              .AllowCredentials(); // для SignalR
    });
});

var app = builder.Build();


using (var scope = app.Services.CreateScope())  // create db
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

var tasks = new List<ProjectTask>
{
    new ProjectTask { Id = 1, Title = "Test 1", Description = "Create test 1", Status = "Done" },
    new ProjectTask { Id = 2, Title = "Test 2", Description = "Run test 2", Status = "InProgress" }
};

app.MapGet("/api/tasks", async (AppDbContext db) =>
    Results.Ok(await db.Tasks.ToListAsync()));

app.MapPost("/api/tasks", async (ProjectTask newTask, AppDbContext db, IHubContext<TaskHub> hubContext) =>
{
    db.Tasks.Add(newTask);
    await db.SaveChangesAsync();

    await hubContext.Clients.All.SendAsync("TaskCreated", newTask);
    return Results.Created($"/api/tasks/{newTask.Id}", newTask);
});

app.MapPut("/api/tasks/{id}/status", async (int id, string newStatus, AppDbContext db, IHubContext<TaskHub> hubContext) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task == null) return Results.NotFound();

    task.Status = newStatus;
    await db.SaveChangesAsync();

    await hubContext.Clients.All.SendAsync("TaskUpdated", task);
    return Results.Ok(task);
});

app.MapDelete("/api/tasks/{id}", async (int id, AppDbContext db, IHubContext<TaskHub> hubContext) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task == null) return Results.NotFound();

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    await hubContext.Clients.All.SendAsync("TaskDeleted", id);
    return Results.NoContent();
});


app.UseDefaultFiles(); // index.html по умолчанию
app.UseStaticFiles(); 

app.MapHub<TaskHub>("/taskHub");

app.Run();


[Table("Tasks")]
public class ProjectTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "New";
}


public class TaskHub : Hub
{
    // 
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<ProjectTask> Tasks { get; set; } = null!;
}
