using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed record GetGigByIdQuery(Guid GigId) : IRequest<Gig?>;