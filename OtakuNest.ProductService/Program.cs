using OtakuNest.Common.Extensions;
using OtakuNest.ProductService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddSwaggerWithJwt()
    .AddAppDbContext(builder.Configuration)
    .AddRabbitMq()
    .AddAppServices()
    .AddCommonHelpers()
    .AddJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

await app.ApplyMigrationsAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();
