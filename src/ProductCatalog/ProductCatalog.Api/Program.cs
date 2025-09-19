using ProductCatalog.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddCustomAuthorization(builder.Configuration)
    .AddCustomSwagger();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseSwaggerDocumentation();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
