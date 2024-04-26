using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CityInfo.API;
using CityInfo.API.DbContexts;
using CityInfo.API.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (environment == Environments.Development)
{
    /*
     * SERILOG Logging
     * For write to different destinations Sinks Package are required
     * Serilog.Sinks.File
     * Serilog.Sinks.Console
     */
    builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day));
}
else
{
    /*
     * Configure KeyVault
     * Add nuggets packages is required
     * - Azure.Extensions.AspNetCore.Configuration.Secrets
     * - Azure.Identity
     */
    var secretClient = new SecretClient(
        new Uri("https://testcakv01.vault.azure.net/"),
        new DefaultAzureCredential());

    builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());

    /*
     * SERILOG Logging
     * For write to different destinations Sinks Package are required
     * Serilog.Sinks.File
     * Serilog.Sinks.Console
     * Serilog.Sinks.ApplicationInsights (Azure functionality)
     */
    builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/cityinfo.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.ApplicationInsights(
        new TelemetryConfiguration
        {
            InstrumentationKey = builder.Configuration["ApplicationInsightsInstrumentationKey"]
        },
        TelemetryConverter.Traces
    ));
}

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
})
    .AddNewtonsoftJson()
    .AddXmlDataContractSerializerFormatters();

builder.Services.AddProblemDetails();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSingleton<FileExtensionContentTypeProvider>();

#if DEBUG
builder.Services.AddTransient<IMailService, LocalMailService>();
#else
builder.Services.AddTransient<IMailService, CloudMailService>();
#endif

builder.Services.AddSingleton<CitiesDataStore>();

builder.Services
    .AddDbContext<CityInfoContext>(
        dbContextOptions => dbContextOptions.UseSqlite(
            builder.Configuration["ConnectionStrings:CityInfoDBConnectionString"]));

builder.Services.AddScoped<ICityInfoRepository, CityInfoRepository>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

/*
 ** dotnet user-jwts: To validate tokens in development environment 
 * Syntax samples:
 * create token
 * - dotnet user-jwts create --issuer https://localhost:7236 --audience cityinfoapi --claim "city=Antwerp"
 * get signing key for respective audience
 * - dotnet user-jwts key --issuer https://localhost:7236
 * get list of created tokens
 * - dotnet user-jwts list
 * print an exisiting token
 * - dotnet user-jwts print  50aac056
 */

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
#pragma warning disable CS8604 // Possible null reference argument.
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Authentication:Issuer"],
            ValidAudience = builder.Configuration["Authentication:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(builder.Configuration["Authentication:SecretForKey"]))

        };
#pragma warning restore CS8604 // Possible null reference argument.
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MustBeFromAntwerp", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("city", "Antwerp");

    });
});

builder.Services
    .AddApiVersioning(setupAction =>
    {
        setupAction.ReportApiVersions = true;
        setupAction.AssumeDefaultVersionWhenUnspecified = true;
        setupAction.DefaultApiVersion = new ApiVersion(1, 0);
    })
    .AddMvc()
    .AddApiExplorer(setupAction =>
    {
        setupAction.SubstituteApiVersionInUrl = true;
    });

var apiVersionDescriptionProvider = builder.Services.BuildServiceProvider()
    .GetRequiredService<IApiVersionDescriptionProvider>();

builder.Services.AddSwaggerGen(setupAction =>
{
    foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
    {
        setupAction.SwaggerDoc(
            $"{description.GroupName}",
            new()
            {
                Title = "City Info API",
                Version = description.ApiVersion.ToString(),
                Description = "Through this API you can access cities and their points of interest"
            });
    }

    // XML comments documents is configured in Project/Properties/Build/Output
    //                      => DocumentationFile
    //                      => XML documentation file path
    var xmlCommentsFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlCommentsFullPath = Path.Combine(AppContext.BaseDirectory, xmlCommentsFile);

    setupAction.IncludeXmlComments(xmlCommentsFullPath);

    setupAction.AddSecurityDefinition("CityInfoApiBearerAuth", new()
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        Description = "input a valid token to access this API"
    });

    setupAction.AddSecurityRequirement(new()
    {
        {
            new ()
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "CityInfoApiBearerAuth"
                }
            },
            new List<string> ()
        }
    });
});


/*
 * Forwarded Middleware
 * Some times when a proxy or load balancer is configured, some information from the original request is changed like IP
 * This information should be added in the ForwardedHeaders of the request
 * For this a default middleware exists, we could configure it
 */
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// Forwaded Headers
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI(setupAction =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var desc in descriptions)
        {
            setupAction.SwaggerEndpoint(
                $"/swagger/{desc.GroupName}/swagger.json",
                desc.GroupName.ToUpperInvariant());
        }
    });
//}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllers();
});

app.Run();

/*
 * Notest about test
 * For end-to-end test we can use many options:
 * ------- Postman: UI were we can develop diferents request to the API
 * ------- End Postman
 * 
 * -------- Microsoft http-repl: Command Line Tool to test API from command line
 * cmd syntaxes
 * - install
 * dotnet tool install -g microsoft.dotnet-httprepl
 * 
 * - list of dotnet tools installed
 * dotnet tool list -g
 * 
 * - connect to host of API
 * httprepl https://localhost:7236
 * 
 * - connect to host of API with openapi documentation
 * httprepl https://localhost:7236 --openapi https://localhost:7236/swagger/2.0/swagger.json
 * 
 * - navigate to differents endpoints (after connect)
 * https://localhost:7236/> cd api/v2/cities
 * https://localhost:7236/api/v2/cities> cd 1
 * https://localhost:7236/api/v2/cities/1> ls
 * https://localhost:7236/api/v2/cities/1> cd pointsofinterest
 * 
 * - establish default editor for post 
 * https://localhost:7236/api/v2/cities/1/pointsofinterest>  pref set editor.command.default c:/Windows/system32/notepad.exe
 * 
 * - send post request
 * https://localhost:7236/api/v2/cities/1/pointsofinterest> post -h Content-Type:application/json -h Accept:application/json
 * 
 * -- enable authentication toke JWT
 * https://localhost:7236/api/cities> set header Authorization "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwiZ2l2ZW5fbmFtZSI6IkNhcmxvcyIsImZhbWlseV9uYW1lIjoiQWd1aWxhciIsImNpdHkiOiJBbnR3ZXJwIiwibmJmIjoxNzE0MDkyNzA2LCJleHAiOjE3MTQwOTYzMDYsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjcyMzYiLCJhdWQiOiJjaXR5aW5mb2FwaSJ9.BI6WLGWAgdIVDGs5z_LSN83zrlQnpuKQcJk5GMxXKbI"
 * 
 * -------- End http-repl
 
 */
