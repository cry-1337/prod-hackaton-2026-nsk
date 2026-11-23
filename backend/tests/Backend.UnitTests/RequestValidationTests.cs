using Backend.Application.Commands.Auth;
using Backend.Application.Queries.Catalog;
using Backend.Application.Queries.Groups;
using Xunit;

namespace Backend.UnitTests;

public sealed class RequestValidationTests {
    [Theory]
    [InlineData(" buyer@example.test ", "Покупатель", "valid-password", null)]
    [InlineData("Buyer <buyer@example.test>", "Покупатель", "valid-password", "EMAIL_INVALID")]
    [InlineData("buyer@example.test", " ", "valid-password", "NICKNAME_REQUIRED")]
    [InlineData("buyer@example.test", "Покупатель", "short", "PASSWORD_LENGTH_INVALID")]
    public void RegistrationValidatesCredentials(string email, string name, string password, string? expectedError) {
        var result = new RegisterCommandValidator().Validate(new RegisterCommand(email, name, password));

        Assert.Equal(expectedError, result.Errors.SingleOrDefault()?.ErrorCode);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void CatalogValidatesSearchLength(int length, bool valid) {
        var query = new GetProductsQuery(new string('я', length));
        Assert.Equal(valid, new GetProductsQueryValidator().Validate(query).IsValid);
    }

    [Theory]
    [InlineData(null, null, 50, null)]
    [InlineData(5L, null, 100, null)]
    [InlineData(null, 5L, 1, null)]
    [InlineData(5L, 7L, 30, "CHAT_CURSORS_CONFLICT")]
    [InlineData(0L, null, 30, "CHAT_CURSOR_INVALID")]
    [InlineData(null, null, 101, "CHAT_PAGE_SIZE_INVALID")]
    public void ChatAcceptsOnePagingDirection(long? before, long? after, int limit, string? expectedError) {
        var query = new GetGroupChatMessagesQuery("group", before, after, limit);
        var result = new GetGroupChatMessagesValidator().Validate(query);

        Assert.Equal(expectedError, result.Errors.SingleOrDefault()?.ErrorCode);
    }
}
