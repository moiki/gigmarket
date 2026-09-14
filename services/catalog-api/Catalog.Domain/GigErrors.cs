using BuildingBlocks.Common;

namespace Catalog.Domain;

public static class GigErrors
{
    public static readonly Error InvalidId = new("Gig.InvalidId", "El id del gig es inválido.");
    public static readonly Error TitleRequired = new("Gig.TitleRequired", "El título es obligatorio.");
    public static readonly Error TitleTooLong = new("Gig.TitleTooLong", "El título no puede superar los 100 caracteres.");
    public static readonly Error DescriptionTooLong = new("Gig.DescriptionTooLong", "La descripción no puede superar los 2000 caracteres.");
    public static readonly Error InvalidPrice = new("Gig.InvalidPrice", "El precio debe ser mayor que 0 y menor o igual que 100000.");
    public static readonly Error UnknownCategory = new("Gig.UnknownCategory", "La categoría no existe.");
    public static readonly Error OwnerRequired = new("Gig.OwnerRequired", "El ownerId es obligatorio.");
    public static readonly Error NotDraftStatus = new("Gig.NotDraftStatus", "Solo un gig en estado Draft puede publicarse.");
}