using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using VerisFlow.Mcp.Server;
using VerisFlow.Mcp.Server.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new JwtBearerEvents();

    var existingOnMessageReceived = options.Events.OnMessageReceived;

    options.Events.OnMessageReceived = async context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        // Intercept token from query string specifically for WebSocket handshakes
        // WebSockets cannot pass Authorization headers during browser handshakes, requiring token extraction from query parameters.
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/mcphub"))
        {
            context.Token = accessToken;
        }

        if (existingOnMessageReceived != null)
        {
            await existingOnMessageReceived(context);
        }
    };
});

// Configure a strict authorization policy requiring a specific scope to prevent unauthorized tenant access.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireApiScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("http://schemas.microsoft.com/identity/claims/scope", "access_as_user");
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "VerisFlow Local Agent API",
        Version = "v1",
        Description = "API for Cloud AIs to trigger local VerisFlow workflow analysis."
    });

    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var clientId = builder.Configuration["AzureAd:ClientId"];
    var scope = builder.Configuration["AzureAd:Scopes"];

    var authorizationUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize";
    var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

    // Configure Azure AD OAuth2 Authorization Code flow with PKCE for OpenAPI documentation.
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(authorizationUrl),
                TokenUrl = new Uri(tokenUrl),
                Scopes = new Dictionary<string, string>
                {
                    { $"api://{clientId}/{scope}", "Access API" }
                }
            }
        }
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new List<string> { $"api://{clientId}/{scope}" }
        }
    });
});

// INCREASE SIGNALR LIMIT: Set to 10MB to support large JSON trace data
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

builder.Services.AddSingleton<McpCoordinator>();
builder.Services.AddSingleton<McpSseConnectionManager>();

builder.Services.AddHttpClient();
builder.Services.AddControllers();

var claudeCorsPolicy = "AllowClaudeWeb";

builder.Services.AddCors(options =>
{
    options.AddPolicy(claudeCorsPolicy, policy =>
    {
        policy.WithOrigins(
            "https://example.com",
            "https://preview.example.com",
            "https://www.example.com",
            "https://api.example.com"
            )
        // Allow dynamic loopback addresses for local testing alongside explicitly trusted remote domains.
        .SetIsOriginAllowed(origin =>
        {
            var host = new Uri(origin).Host;
            return host == "localhost" || host == "127.0.0.1";
        })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Strictly required for SignalR token negotiation
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    var clientId = builder.Configuration["AzureAd:ClientId"];
    c.OAuthClientId(clientId);
    c.OAuthUsePkce();
});

// ==========================================
// CRITICAL MIDDLEWARE ORDERING FIX
// ==========================================
// 1. Explicitly enable routing first so endpoint metadata is available for CORS
app.UseRouting();

// 2. Apply CORS policy immediately after routing, before Auth
app.UseCors(claudeCorsPolicy);

// 3. Authenticate and Authorize the request
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// HUB AND CONTROLLER MAPPINGS
// ==========================================
// Enforce the strict scope policy on the SignalR Hub.
app.MapHub<McpRelayHub>("/mcphub").RequireAuthorization("RequireApiScope");

app.MapControllers();

// Clean invocation mapping minimal APIs configured in extensions
app.MapTraceToolEndpoints();
app.MapHamiltonToolEndpoints();

app.Run();