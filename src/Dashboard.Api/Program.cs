using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Dashboard.Api.Options;
using Dashboard.Api.Services;
using Dashboard.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddAuthorization();

builder.Services
    .AddOptions<FoundryOptions>()
    .Bind(builder.Configuration.GetSection(FoundryOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.Provider is FoundryOptions.SampleProvider or FoundryOptions.HttpProvider,
        $"Foundry:Provider must be '{FoundryOptions.SampleProvider}' or '{FoundryOptions.HttpProvider}'.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
    .Validate(options => !options.Required || !string.IsNullOrWhiteSpace(options.Audience),
        "Authentication:Audience must be configured when Authentication:Required=true.")
    .Validate(options => !options.Required || !string.IsNullOrWhiteSpace(options.Authority) || !string.IsNullOrWhiteSpace(options.TenantId),
        "Authentication:Authority or Authentication:TenantId must be configured when Authentication:Required=true.")
    .ValidateOnStart();

var clientOrigin = builder.Configuration["ClientOrigin"];
if (string.IsNullOrWhiteSpace(clientOrigin))
{
    throw new InvalidOperationException("ClientOrigin configuration is required.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ClientPolicy",
        corsBuilder =>
        {
            corsBuilder
                .WithOrigins(clientOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddSingleton<SampleFoundryBillingService>();
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient<HttpFoundryBillingService>();
builder.Services.AddScoped<FoundryRequestScopeResolver>();
builder.Services.AddScoped<IFoundryBillingService>(serviceProvider =>
{
    var foundryOptions = serviceProvider.GetRequiredService<IOptions<FoundryOptions>>().Value;
    return foundryOptions.Provider switch
    {
        FoundryOptions.SampleProvider => serviceProvider.GetRequiredService<SampleFoundryBillingService>(),
        FoundryOptions.HttpProvider => serviceProvider.GetRequiredService<HttpFoundryBillingService>(),
        _ => throw new InvalidOperationException(
            $"Unsupported Foundry provider '{foundryOptions.Provider}'. Supported values are '{FoundryOptions.SampleProvider}' and '{FoundryOptions.HttpProvider}'.")
    };
});

var authenticationOptions = builder.Configuration
    .GetSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

if (!authenticationOptions.Required && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Authentication:Required=false is only supported in Development.");
}

if (authenticationOptions.Required)
{
    var authority = !string.IsNullOrWhiteSpace(authenticationOptions.Authority)
        ? authenticationOptions.Authority
        : $"https://login.microsoftonline.com/{authenticationOptions.TenantId}/v2.0";

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = authenticationOptions.Audience;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters.ValidateIssuer = true;
        });
}

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("ClientPolicy");

if (authenticationOptions.Required)
{
    app.UseAuthentication();
}

app.UseAuthorization();

var apiGroup = app.MapGroup("/api");
if (authenticationOptions.Required)
{
    apiGroup.RequireAuthorization();
}

apiGroup.MapGet("/projects", async (
    string? tenantId,
    string? subscriptionId,
    HttpRequest request,
    FoundryRequestScopeResolver requestScopeResolver,
    IFoundryBillingService billingService,
    IMemoryCache cache,
    IOptions<FoundryOptions> foundryOptionsAccessor,
    CancellationToken cancellationToken) =>
{
    try
    {
        var foundryOptions = foundryOptionsAccessor.Value;
        var resolvedScope = requestScopeResolver.Resolve(request, tenantId, subscriptionId);
        var cacheKey = $"{foundryOptions.Provider}:projects:{resolvedScope.ResolvedTenantId ?? "_"}:{resolvedScope.ResolvedSubscriptionId ?? "_"}";
        var projects = await cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(foundryOptions.CacheSeconds);
                return await billingService.GetProjectsAsync(resolvedScope.RequestScope, cancellationToken);
            });

        return Results.Ok(projects ?? Array.Empty<ProjectSummary>());
    }
    catch (FoundryProviderException ex)
    {
        return ToFoundryProblem(ex);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Foundry provider configuration error.",
            detail: ex.Message);
    }
});

apiGroup.MapGet("/projects/{projectId}/billing", async (
    string projectId,
    string? period,
    string? tenantId,
    string? subscriptionId,
    HttpRequest request,
    FoundryRequestScopeResolver requestScopeResolver,
    IFoundryBillingService billingService,
    IMemoryCache cache,
    IOptions<FoundryOptions> foundryOptionsAccessor,
    CancellationToken cancellationToken) =>
{
    if (!BillingPeriodParser.TryParse(period, out var billingPeriod))
    {
        return Results.BadRequest(new ApiError("invalid_period", "period must be 'currentMonth' or 'previousMonth'."));
    }

    try
    {
        var foundryOptions = foundryOptionsAccessor.Value;
        var resolvedScope = requestScopeResolver.Resolve(request, tenantId, subscriptionId);
        var cacheKey = $"{foundryOptions.Provider}:billing:{projectId}:{BillingPeriodParser.ToQueryValue(billingPeriod)}:{resolvedScope.ResolvedTenantId ?? "_"}:{resolvedScope.ResolvedSubscriptionId ?? "_"}";
        var summary = await cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(foundryOptions.CacheSeconds);
                return await billingService.GetBillingSummaryAsync(projectId, billingPeriod, resolvedScope.RequestScope, cancellationToken);
            });

        return Results.Ok(summary);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ApiError("project_not_found", ex.Message));
    }
    catch (FoundryProviderException ex)
    {
        return ToFoundryProblem(ex);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Foundry provider configuration error.",
            detail: ex.Message);
    }
});

app.Run();

static IResult ToFoundryProblem(FoundryProviderException ex)
{
    var extensions = new Dictionary<string, object?>
    {
        ["errorCode"] = ex.ErrorCode
    };

    return Results.Problem(
        statusCode: ex.StatusCode,
        title: ex.IsConfigurationError
            ? "Foundry provider configuration error."
            : "Foundry provider request failed.",
        detail: ex.Message,
        extensions: extensions);
}

public partial class Program;
