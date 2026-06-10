using Asp.Versioning;
using ClaimsIntake.API.Endpoints;
using ClaimsIntake.API.Middleware;
using ClaimsIntake.API.Swagger;
using ClaimsIntake.Application;
using ClaimsIntake.Infrastructure;
using ClaimsIntake.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));

builder.Services.RegisterApplication();
builder.Services.RegisterInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    // Group names become "v1", "v2", ... and drive the Swagger doc names.
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Swagger documents are generated per discovered API version — see ConfigureSwaggerGenOptions.
builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ClaimsDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.MapClaimEndpoints();
// Adding a v2 later requires no Swagger changes: give the (new) endpoint group
//   .HasApiVersion(new ApiVersion(2, 0))
// and Swagger will pick it up automatically.
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // One UI entry per discovered version; must run after endpoints are mapped.
        foreach (var description in app.DescribeApiVersions())
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });

    await app.Services.MigrateDatabaseAsync();
}

app.Run();

// Exposes the implicit Program class to WebApplicationFactory<Program> in the test project.
public partial class Program { }
