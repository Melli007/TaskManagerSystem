using EtechTaskManagerBackend.Data;
using EtechTaskManagerBackend.EtechHubs;
using EtechTaskManagerBackend.Interfaces;
using EtechTaskManagerBackend.Migrations;
using EtechTaskManagerBackend.Repository;
using EtechTaskManagerBackend.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

// This should be placed inside Program.cs or Startup.cs
QuestPDF.Settings.License = LicenseType.Community;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Register LoggingService

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Etech Task Manager API",
        Version = "v1"
    });


    c.OperationFilter<FileUploadOperation>(); // For TasksController
});




builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue; // Or a specific limit like 50MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 16 * 1024 * 1024; // 16 MB
});


builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

//Logging Services
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddHttpClient();


builder.Services.AddTransient<Seed>();
builder.Services.AddScoped<IUsersRepository , UsersRepository>();
builder.Services.AddScoped<ITasksRepository , TasksRepository>();
builder.Services.AddScoped<INotificationsRepository , NotificationsRepository>();
builder.Services.AddScoped<IMessagesRepository , MessagesRepository>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            policy.WithOrigins("https://localhost:7097") 
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});


builder.Services.AddSignalR(options =>
{
    // optional config
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
});

// This is important. If you do not do this, `Clients.User(...)` won't match your user IDs properly.
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();


var app = builder.Build();


if (args.Length == 1 && args[0].ToLower() == "seeddata")
{
    SeedData(app);
}

void SeedData(IHost app)
{
    // Get IServiceScopeFactory to create a scope for the seed operation
    var scopedFactory = app.Services.GetService<IServiceScopeFactory>();

    if (scopedFactory != null)
    {
        using (var scope = scopedFactory.CreateScope())
        {
            // Resolve the Seed service and call SeedDataContext method
            var service = scope.ServiceProvider.GetRequiredService<Seed>();
            service.SeedDataContext();
        }
    }
    else
    {
        Console.WriteLine("IServiceScopeFactory not available for seeding.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Show detailed errors during development
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // If you see a cross?origin block for images, you can add:
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "https://localhost:7097");
    }
});


app.UseCors("AllowSpecificOrigins");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<TaskHub>("/taskHub");
app.MapHub<MessageHub>("/messageHub");


app.MapControllers();

app.Run();
