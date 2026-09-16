using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Common;
using Orders.Application;
using Orders.Domain;

namespace Orders.Infrastructure;

public sealed class HttpGigCatalog(HttpClient http) : IGigCatalog
{
    public async Task<Result<GigInfo>> GetByIdAsync(Guid gigId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync($"/api/gigs/{gigId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return OrderErrors.GigNotFound;

            if (!response.IsSuccessStatusCode)
                return OrderErrors.CatalogUnavailable;

            var gig = await response.Content.ReadFromJsonAsync<GigCatalogResponse>(cancellationToken);

            return new GigInfo(gig!.Id, gig.Price, gig.OwnerId, gig.Status == "Active");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderErrors.CatalogUnavailable;
        }
        catch (HttpRequestException)
        {
            return OrderErrors.CatalogUnavailable;
        }
        catch (JsonException)
        {
            return OrderErrors.CatalogUnavailable;
        }
    }
}

internal sealed record GigCatalogResponse(Guid Id, decimal Price, Guid OwnerId, string Status);