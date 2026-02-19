using Microsoft.EntityFrameworkCore;
using TallinkFinance.Data;
using TallinkFinance.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Data Source=tallinkfinance.db";

    options
        .UseSqlite(connectionString)
        .EnableDetailedErrors();

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});
builder.Services.AddScoped<BalanceCalculatorService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var resetDatabase = app.Configuration.GetValue<bool>("Database:ResetOnStartup");
    if (resetDatabase)
    {
        dbContext.Database.EnsureDeleted();
    }

    dbContext.Database.EnsureCreated();
}

app.Run();
