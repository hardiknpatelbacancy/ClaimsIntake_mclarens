using System.Reflection;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClaimsIntake.API.Swagger;

/// <summary>
/// Generates one Swagger document per API version discovered by the versioning
/// API explorer. Versions are never hardcoded here — adding a version to an
/// endpoint group is all that is needed for a new document to appear.
/// </summary>
public sealed class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    /// <summary>Creates the configurator with the provider that exposes all discovered API versions.</summary>
    public ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Registers one Swagger document per discovered API version and includes this assembly's
    /// XML documentation file when it has been generated.
    /// </summary>
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "Claims Intake API",
                Version = description.ApiVersion.ToString(),
                Description = "Claims intake service: submit claims, query them, and move them through their lifecycle."
                              + (description.IsDeprecated ? " This API version has been deprecated." : string.Empty)
            });
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    }
}
