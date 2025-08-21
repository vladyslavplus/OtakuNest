using OtakuNest.CommentService.Extensions;
using OtakuNest.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddSwaggerWithJwt()
    .AddAppDbContext(builder.Configuration)
    .AddRabbitMq()
    .AddRedisCache(builder.Configuration)
    .AddAppServices()
    .AddCommonHelpers()
    .AddFluentValidationSetup(typeof(Program).Assembly)
    .AddJwtBearerAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

var app = builder.Build();

await app.ApplyMigrationsAsync();

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
