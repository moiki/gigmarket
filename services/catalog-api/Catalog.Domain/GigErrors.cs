using BuildingBlocks.Common;

namespace Catalog.Domain;

public static class GigErrors
{
    public static readonly Error InvalidId = Error.Validation("Gig.InvalidId", "El id del gig es inválido.");
    public static readonly Error TitleRequired = Error.Validation("Gig.TitleRequired", "El título es obligatorio.");
    public static readonly Error TitleTooLong = Error.Validation("Gig.TitleTooLong", "El título no puede superar los 100 caracteres.");
    public static readonly Error DescriptionTooLong = Error.Validation("Gig.DescriptionTooLong", "La descripción no puede superar los 2000 caracteres.");
    public static readonly Error InvalidPrice = Error.Validation("Gig.InvalidPrice", "El precio debe ser mayor que 0 y menor o igual que 100000.");
    public static readonly Error UnknownCategory = Error.Validation("Gig.UnknownCategory", "La categoría no existe.");
    public static readonly Error OwnerRequired = Error.Validation("Gig.OwnerRequired", "El ownerId es obligatorio.");
    public static readonly Error NotDraftStatus = Error.Validation("Gig.NotDraftStatus", "Solo un gig en estado Draft puede publicarse.");
    public static readonly Error GigNotFound = Error.NotFound("Gig.NotFound", "El gig no existe.");
}