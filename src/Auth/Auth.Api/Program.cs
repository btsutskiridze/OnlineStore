using Auth.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtTokenConfigurationOptions>(
    builder.Configuration.GetSection("JwtTokenConfiguration"));
builder.Services.Configure<InterServiceAuthenticationOptions>(
    builder.Configuration.GetSection("InterServiceAuthentication"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
