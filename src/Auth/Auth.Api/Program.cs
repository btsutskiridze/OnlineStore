using Auth.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var jwtConfig = builder.Configuration.GetSection("Jwt");

app.MapPost("/token", ([FromQuery] string role, [FromQuery] string? name, [FromQuery] int? userId) =>
{
    var id = string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
        ? 1.ToString()
        : 2.ToString();
    role = string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
        ? UserRoles.Admin : UserRoles.User;

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim> {
        new (ClaimTypes.NameIdentifier, id),
        new(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? $"{role.ToLower()}" : name!),
        new(ClaimTypes.Role, role)
    };

    var jwt = new JwtSecurityToken(
        issuer: jwtConfig["Issuer"],
        audience: jwtConfig["Audience"],
        claims: claims,
        notBefore: DateTime.UtcNow.AddMinutes(-1),
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: creds
    );

    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt), role });
})
.WithName("IssueDevToken");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
