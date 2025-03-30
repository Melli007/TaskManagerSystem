using ETechTaskManager.Controllers;
using ETechTaskManager.Filters;
using ETechTaskManager.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // Set the license context
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews(options =>
{
    // Add the AuthenticationFilter globally
    options.Filters.Add<AuthenticationFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 16 * 1024 * 1024; // Or a specific limit like 16MB
});

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".AspNetCore.Session_" + Guid.NewGuid().ToString();
});
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>(); // Add this

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins("https://localhost:7013")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register SignalR
builder.Services.AddSignalR();

// Register HttpClient and NotificationController1 dependencies
builder.Services.AddHttpClient();
builder.Services.AddScoped<NotificationController1>();

var app = builder.Build();

// Ensure all users are offline on startup
using (var scope = app.Services.CreateScope())
{
    var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient();

    // Ensure you use the full absolute URL since this is an MVC project
    var response = await client.PutAsync("https://localhost:7013/api/Users/SetAllOffline", null);

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine("Failed to set all users offline at startup");
    }
    else
    {
        Console.WriteLine("All users set to offline at startup");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable session middleware
app.UseSession();

app.UseMiddleware<SessionExpirationMiddleware>();

app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();  // Authenticate users

app.UseAuthorization();   // Authorize users

// Define routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();
