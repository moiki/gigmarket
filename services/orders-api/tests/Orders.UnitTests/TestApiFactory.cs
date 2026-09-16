using BuildingBlocks.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Api;
using Orders.Application;
using Orders.Infrastructure;

namespace Orders.UnitTests;

internal sealed class TestApiFactory(FakeGigCatalog catalog) : WebApplicationFactory<Program>
{
    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryIdempotencyStore _idempotency = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOrderRepository>();
            services.AddSingleton<IOrderRepository>(_orders);

            services.RemoveAll<IGigCatalog>();
            services.AddSingleton<IGigCatalog>(catalog);

            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton<IIdempotencyStore>(_idempotency);

            services.RemoveAll<ITransaction>();
            services.AddSingleton<ITransaction>(new NoopTransaction());
        });
    }

    public InMemoryOrderRepository Orders => _orders;
}