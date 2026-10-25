using Bookstore.Api.Data;
using Bookstore.Api.Security;
using Bookstore.Api.Services;
using Bookstore.Api.Startup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is required.");
var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException("Authentication:Audience is required.");

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IBookstoreManagementClient, BookstoreManagementClient>();
builder.Services.AddDbContextFactory<BookstoreDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("Bookstore")
        ?? throw new InvalidOperationException("ConnectionStrings:Bookstore is required.");
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        if (builder.Configuration["Authentication:MetadataAddress"] is { } metadataAddress)
        {
            options.MetadataAddress = metadataAddress;
        }
        options.Audience = audience;
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.SearchBooks, policy =>
        policy.RequireAuthenticatedUser().RequireScope("books.search"));
    options.AddPolicy(Policies.ManageBooks, policy =>
        policy.RequireAuthenticatedUser().RequireScope("books.manage")
            .RequireClaim("azp", "bookstore-service"));
    options.AddPolicy(Policies.UseManagementUi, policy =>
        policy.RequireAuthenticatedUser().RequireScope("books.search")
            .RequireRealmRole("bookstore-editor"));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sql-server");
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookstore API",
        Version = "v1",
        Description = "OAuth-protected CRUD and search API for books."
    });

    var tokenUrl = new Uri($"{authority.TrimEnd('/')}/protocol/openid-connect/token");
    var authorizationUrl = new Uri($"{authority.TrimEnd('/')}/protocol/openid-connect/auth");
    options.AddSecurityDefinition("interactive", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "Authorization Code with PKCE for book search.",
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = authorizationUrl,
                TokenUrl = tokenUrl,
                Scopes = new Dictionary<string, string> { ["books.search"] = "Search books" }
            }
        }
    });
    options.AddSecurityDefinition("service", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "Client Credentials for book management.",
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = tokenUrl,
                Scopes = new Dictionary<string, string> { ["books.manage"] = "Manage books" }
            }
        }
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("interactive", document)] = ["books.search"]
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("service", document)] = ["books.manage"]
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Bookstore API v1");
        options.OAuthClientId("bookstore-ui");
        options.OAuthUsePkce();
        options.DocumentTitle = "Bookstore API";
    });
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

await app.InitialiseDatabaseAsync();
await app.RunAsync();

public partial class Program;
