using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed record GetGigsQuery(int Page, int PageSize, GigStatus? Status) : IRequest<PagedResult<Gig>>;