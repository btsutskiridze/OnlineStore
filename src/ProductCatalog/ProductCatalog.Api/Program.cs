using ProductCatalog.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices(builder.Configuration)
    .AddDatabaseServices(builder.Configuration)
    .AddJwtAuthentication(builder.Configuration)
    .AddCustomAuthorization(builder.Configuration)
    .AddCustomSwagger();


var app = builder.Build();

app.InitializeDatabase();

app.UseGlobalExceptionHandling();

app.UseSwaggerDocumentation();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
