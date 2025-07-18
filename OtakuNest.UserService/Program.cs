using OtakuNest.Common.Extensions;
using OtakuNest.UserService.Extensions;
using OtakuNest.UserService.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddSwaggerWithJwt()
    .AddAppIdentity(builder.Configuration)
    .AddJwtBearerAuthentication(builder.Configuration)
    .AddAppMassTransit()
    .AddApplicationServices();

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

await app.SeedDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
