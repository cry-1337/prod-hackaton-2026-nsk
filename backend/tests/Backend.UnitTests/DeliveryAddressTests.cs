using Backend.Application.Commands.Groups;
using Backend.Domain.Groups;
using Xunit;

namespace Backend.UnitTests;

public sealed class DeliveryAddressTests {
    [Theory]
    [InlineData("Ростов-на-Дону", "8 Марта", "12А", null, null, true)]
    [InlineData("Казань", "пр. Победы", "12/1", "Б", "18-2", true)]
    [InlineData("123", "Мира", "1", null, null, false)]
    [InlineData("Казань", " ", "1", null, null, false)]
    [InlineData("Казань", "Мира", "дом", null, null, false)]
    [InlineData("Казань", "Мира", "0", null, null, false)]
    [InlineData("Казань", "Мира", "1", null, "<42>", false)]
    public void ValidatesCheckoutAddress(string city, string street, string house, string? building, string? apartment, bool valid) {
        var address = new DeliveryAddress(city, street, house, building, apartment);
        var command = new ChangeGroupLimitCommand("group", Guid.NewGuid(), 99000, null) { Address = address };

        Assert.Equal(valid, new ChangeGroupLimitValidator().Validate(command).IsValid);
        Assert.Equal(address, DeliveryAddress.FromStorage(address.ToStorage()));
    }

    [Fact]
    public void LegacyTextIsPreservedForReadingAndWithdrawalNeedsNoAddress() {
        Assert.Null(DeliveryAddress.FromStorage("Прежний адрес"));
        Assert.Null(DeliveryAddress.FromStorage("{не адрес}"));
        var revoke = new ChangeGroupLimitCommand("group", Guid.NewGuid(), null, null);
        Assert.True(new ChangeGroupLimitValidator().Validate(revoke).IsValid);
    }
}
