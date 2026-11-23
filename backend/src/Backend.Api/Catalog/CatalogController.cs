using Backend.Api.Contracts.Responses;
using Backend.Api.Groups;
using Backend.Application.Queries.Catalog;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Catalog;

[ApiController]
[Route("api/catalog/products")]
[AllowAnonymous]
public sealed class CatalogController(IMediator mediator) : ControllerBase {
    [HttpGet]
    [EndpointSummary("Найти товары в каталоге")]
    [EndpointDescription("Поиск по части названия без учёта регистра. sortByOrganizerLikes=true сортирует по лайкам самого популярного организатора среди открытых сборов товара.")]
    [ProducesResponseType<ProductCardResponse[]>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — search длиннее 256 символов.")]
    public async Task<ActionResult<ProductCardResponse[]>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] bool sortByOrganizerLikes = false,
        CancellationToken cancellationToken = default
    ) {
        var products = await mediator.Send(new GetProductsQuery(search) { SortByOrganizerLikes = sortByOrganizerLikes }, cancellationToken);
        var images = HttpContext.RequestServices.GetRequiredService<ProductImages>();
        return Ok(products.Select(product => product.Adapt<ProductCardResponse>() with {
            ImageUrl = images.GetUrl(product.ImageKey)
        }).ToArray());
    }

    [HttpGet("{productId}")]
    [EndpointSummary("Посмотреть товар, цены и открытые сборы")]
    [EndpointDescription("productId — UUID товара из каталога. В openGroups сначала идут сборы организаторов с большим числом лайков. При равенстве выше сборы с ближайшим дедлайном.")]
    [ProducesResponseType<ProductDetailsResponse>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400, "application/problem+json", Description = "title: VALIDATION_FAILED — productId должен быть UUID.")]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json", Description = "title: PRODUCT_NOT_FOUND — товар с указанным UUID не найден.")]
    public async Task<ActionResult<ProductDetailsResponse>> GetProduct(Guid productId, CancellationToken cancellationToken) {
        var product = await mediator.Send(new GetProductQuery(productId), cancellationToken);
        if (product is null) {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "PRODUCT_NOT_FOUND");
        }

        var images = HttpContext.RequestServices.GetRequiredService<ProductImages>();
        return Ok(product.Adapt<ProductDetailsResponse>() with {
            ImageUrl = images.GetUrl(product.ImageKey),
            OpenGroups = product.OpenGroups.Select(group => group.ToResponse(Url)).ToArray()
        });
    }
}
