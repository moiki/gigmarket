using BuildingBlocks.Common;
using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed record CreateGigCommand(
    string Title,
    string? Description,
    decimal Price,
    string Category,
    Guid OwnerId) : IRequest<Result<Gig>>;