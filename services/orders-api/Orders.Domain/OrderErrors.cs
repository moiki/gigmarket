using BuildingBlocks.Common;

namespace Orders.Domain;

public static class OrderErrors
{
    public static readonly Error InvalidId = Error.Validation("Order.InvalidId", "El id de la orden es inválido.");
    public static readonly Error GigIdRequired = Error.Validation("Order.GigIdRequired", "El gigId es obligatorio.");
    public static readonly Error BuyerRequired = Error.Validation("Order.BuyerRequired", "El buyerId es obligatorio.");
    public static readonly Error ProviderRequired = Error.Validation("Order.ProviderRequired", "El providerId es obligatorio.");
    public static readonly Error InvalidPrice = Error.Validation("Order.InvalidPrice", "El precio debe ser mayor que 0 y menor o igual que 100000.");
    public static readonly Error GigNotFound = Error.NotFound("Order.GigNotFound", "El gig no existe.");
    public static readonly Error GigNotActive = Error.Conflict("Order.GigNotActive", "El gig no está activo y no puede ser contratado.");
    public static readonly Error NotFound = Error.NotFound("Order.NotFound", "La orden no existe.");
    public static readonly Error InvalidStatusTransition = Error.Conflict("Order.InvalidStatusTransition", "La orden no puede cambiar al estado solicitado desde su estado actual.");
    public static readonly Error CatalogUnavailable = Error.Unavailable("Order.CatalogUnavailable", "El catálogo no está disponible, intente más tarde.");
    public static readonly Error InvalidIdempotencyKey = Error.Validation("Order.InvalidIdempotencyKey", "El header 'Idempotency-Key' debe ser un string no vacío de máximo 128 caracteres.");
    public static readonly Error IdempotencyKeyMismatch = Error.Conflict("Order.IdempotencyKeyMismatch", "La clave de idempotencia ya fue usada con un payload o buyer diferente.");
    public static readonly Error ReplayUnavailable = Error.Unavailable("Order.ReplayUnavailable", "La orden original de la repetición ya no está disponible.");
}