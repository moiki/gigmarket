using BuildingBlocks.Common;
using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed record PublishGigCommand(Guid GigId) : IRequest<Result<Gig>>;