namespace Backend.Api.Catalog;

internal sealed class ProductImages {
    private readonly string? m_BucketUrl;

    public ProductImages(string? bucketUrl) {
        if (string.IsNullOrWhiteSpace(bucketUrl)) {
            return;
        }

        if (!Uri.TryCreate(bucketUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) {
            throw new InvalidOperationException("PRODUCT_IMAGES_BUCKET_URL_INVALID");
        }

        m_BucketUrl = uri.AbsoluteUri.TrimEnd('/');
    }

    public string? GetUrl(string? imageKey) {
        if (m_BucketUrl is null || string.IsNullOrWhiteSpace(imageKey) || Uri.TryCreate(imageKey, UriKind.Absolute, out _)) {
            return null;
        }

        var segments = imageKey.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".." || segment.Contains('\\'))) {
            return null;
        }

        return $"{m_BucketUrl}/{string.Join('/', segments.Select(Uri.EscapeDataString))}";
    }
}
