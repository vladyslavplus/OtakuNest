using MassTransit;
using OtakuNest.SearchService.Consumers;
using OtakuNest.SearchService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductCreatedConsumer>();
    x.AddConsumer<ProductUpdatedConsumer>();
    x.AddConsumer<ProductDeletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("search-service-queue", e =>
        {
            e.ConfigureConsumer<ProductCreatedConsumer>(context);
            e.ConfigureConsumer<ProductUpdatedConsumer>(context);
            e.ConfigureConsumer<ProductDeletedConsumer>(context);
        });
    });
});

var elasticSearchUrl = builder.Configuration.GetConnectionString("Elasticsearch")
                      ?? builder.Configuration["Elasticsearch:Url"]
                      ?? throw new InvalidOperationException("Elasticsearch URL not configured");

builder.Services.AddElasticSearch(elasticSearchUrl);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();