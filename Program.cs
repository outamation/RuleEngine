using DemoRuleEngine.Services;
using DemoRuleEngine.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSerilog((services, configuration) => 
    configuration.ReadFrom.Configuration(builder.Configuration));

// --- Database Configuration ---
builder.Services.AddPooledDbContextFactory<RuleDbContext>(options =>
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register a scoped DbContext resolved from the factory (for endpoints/scoped services)
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<RuleDbContext>>().CreateDbContext());

builder.Services.AddFastEndpoints();
builder.Services.AddHttpClient();

// Register Rule Engine services as singletons for caching and thread safety
builder.Services.AddSingleton<IRuleAuditService, RuleAuditService>();
builder.Services.AddSingleton<IRuleManagerService, RuleManagerService>();
builder.Services.AddSingleton<ISchemaService, SchemaService>();
builder.Services.AddScoped<IEligibilityService, EligibilityService>();

// Add CORS for embedded UI
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Force initialization of RuleManagerService on startup to seed DB and compile RulesEngine cache
using (var scope = app.Services.CreateScope())
{
    var ruleManager = scope.ServiceProvider.GetRequiredService<IRuleManagerService>();
    // Access async getter to fire initial load
    _ = ruleManager.GetWorkflowsAsync();
}

// Configure the HTTP request pipeline.
app.UseCors();
app.UseHttpsRedirection();

// Serve static files (embedded web UI from wwwroot/)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseFastEndpoints();

app.Run();
