using Backend.Api.Catalog;
using Backend.Api.Contracts.Responses;
using Backend.Application.Dto;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Groups;

internal static class InviteLinks {
    public const string RouteName = "GetGroupPurchase";

    public static GroupPurchaseResponse ToResponse(this GroupPurchaseDto group, IUrlHelper url) {
        var images = url.ActionContext.HttpContext.RequestServices.GetRequiredService<ProductImages>();
        return group.Adapt<GroupPurchaseResponse>() with {
            ImageUrl = images.GetUrl(group.ImageKey),
            InviteUrl = GetConfiguredUrl(group.Id, url.ActionContext.HttpContext)
                ?? url.RouteUrl(RouteName, new { groupId = group.Id }, url.ActionContext.HttpContext.Request.Scheme)
                ?? throw new InvalidOperationException("INVITATION_URL_NOT_AVAILABLE")
        };
    }

    public static string? GetFrontendOrigin(string? template) {
        if (string.IsNullOrWhiteSpace(template)) {
            return null;
        }

        if (!Uri.TryCreate(template, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !uri.LocalPath.Contains("{code}", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.UserInfo)) {
            throw new InvalidOperationException("INVITATION_URL_TEMPLATE_INVALID");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string? GetConfiguredUrl(string code, HttpContext context) {
        var template = context.RequestServices.GetRequiredService<IConfiguration>()["Invitations:UrlTemplate"];
        return string.IsNullOrWhiteSpace(template) ? null : template.Replace("{code}", code, StringComparison.Ordinal);
    }
}
