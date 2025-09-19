using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductCatalog.Api.Options;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Configure strongly-typed options
builder.Services.Configure<InboundServiceAuthorizationOptions>(
    builder.Configuration.GetSection("InboundServiceAuthorization"));
builder.Services.Configure<JwtTokenValidationOptions>(
    builder.Configuration.GetSection("JwtTokenValidation"));

var jwtOptions = builder.Configuration.GetSection("JwtTokenValidation").Get<JwtTokenValidationOptions>()
    ?? new JwtTokenValidationOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudiences = jwtOptions.AcceptedAudiences,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        };
    });


builder.Services.AddAuthorization((opts) =>
{
    var authOptions = builder.Configuration.GetSection("InboundServiceAuthorization").Get<InboundServiceAuthorizationOptions>()
        ?? new InboundServiceAuthorizationOptions();

    var interServiceConfig = authOptions.InterServiceAccess;

    opts.AddPolicy("InterServiceAccessOnly", p => p
        .RequireAssertion(ctx =>
            ctx.User.Claims.Any(c => c.Type == "aud" && c.Value == interServiceConfig.RequiredServiceAudience) &&
            ctx.User.Claims.Any(c => c.Type == "client_id" && c.Value == interServiceConfig.AuthorizedClientId) &&
            interServiceConfig.RequiredPermissions.Any(permission =>
                ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission))
        ));

    opts.AddPolicy("PublicUserAccess", p => p
        .RequireAssertion(ctx =>
            ctx.User.Claims.Any(c => c.Type == "aud" && c.Value == "OnlineStore") &&
            ctx.User.Claims.Any(c => c.Type == ClaimTypes.Role)
        ));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme{ Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer"} }, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
