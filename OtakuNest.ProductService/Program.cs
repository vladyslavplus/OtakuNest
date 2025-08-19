using OtakuNest.Common.Extensions;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddSwaggerWithJwt()
    .AddAppDbContext(builder.Configuration)
    .AddRabbitMq()
    .AddAppServices()
    .AddCommonHelpers()
    .AddFluentValidationSetup(typeof(Program).Assembly)
    .AddJwtBearerAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

await app.ApplyMigrationsAsync();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<ProductSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseGlobalExceptionHandling();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();
