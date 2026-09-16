using BuildingBlocks.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Api;
using Orders.Application;
using Orders.Infrastructure;

namespace Orders.IntegrationTests;

internal sealed class OrdersApiFactory(string connectionString, FakeGigCatalog catalog) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));

            services.RemoveAll<IGigCatalog>();
            services.AddSingleton<IGigCatalog>(catalog);
        });
    }
}