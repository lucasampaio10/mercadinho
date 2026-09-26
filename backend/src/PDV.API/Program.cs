using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using PDV.API.Authentication;
using PDV.API.Extensions;
using PDV.API.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ── Infraestrutura + Application ──────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Erro de validação com o mesmo formato { error } dos erros de negócio — senão o
// front recebe um ValidationProblemDetails cru e cai no "Erro desconhecido".
builder.Services.Configure<ApiBehaviorOptions>(opts =>
{
    opts.InvalidModelStateResponseFactory = context =>
    {
        var mensagem = context.ModelState
            .SelectMany(campo => campo.Value?.Errors ?? [])
            .Select(erro => erro.ErrorMessage)
            .FirstOrDefault(msg => !string.IsNullOrWhiteSpace(msg))
            ?? "Dados inválidos na requisição.";

        return new BadRequestObjectResult(new { error = mensagem });
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PDV Mercadinho API", Version = "v1" });
    c.AddSecurityDefinition(ApiKeyAuthenticationOptions.Scheme, new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationOptions.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Chave compartilhada do PDV."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = ApiKeyAuthenticationOptions.Scheme
            }
        }] = Array.Empty<string>()
    });
});

// ── Autenticação ──────────────────────────────────────────────────────────────
var apiKey = builder.Configuration["API_KEY"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "API_KEY não configurada. Defina em appsettings.Development.json (copie de " +
        "appsettings.Development.example.json) ou na variável de ambiente API_KEY.");
}

builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.Scheme, opts => opts.Key = apiKey);

// Sem endpoint anônimo por padrão: quem precisa abrir (health) marca [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ── CORS ──────────────────────────────────────────────────────────────────────
// AllowAnyOrigin deixaria qualquer página aberta no celular do balcão conversar com
// a API. As origens do Expo (web e dev server) são declaradas explicitamente.
var origensPermitidas = builder.Configuration
    .GetSection("CORS_ORIGINS").Get<string[]>()
    ?? ["http://localhost:8081", "http://localhost:19006"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origensPermitidas)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration["POSTGRES_CONNECTION_STRING"]!,
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

// ── Migrations automáticas na inicialização ───────────────────────────────────
await app.ApplyMigrationsAsync();

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
