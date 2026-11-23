using System.Text.RegularExpressions;
using Backend.Domain.Groups;
using FluentValidation;

namespace Backend.Application.Common.Validation;

internal sealed class DeliveryAddressValidator : AbstractValidator<DeliveryAddress> {
    private const string NamePattern = """\A(?=[^\r\n]*\p{L})[\p{L}\p{M}\p{Nd} .,'’"()№/\-‐‑–—]+\z""";
    private const string HousePattern = """\A(?=[^\r\n]*[1-9])[0-9]+\p{L}?(?:[-/][0-9]+\p{L}?)*\z""";
    private const string UnitPattern = """\A[\p{L}\p{Nd}]+(?:[ /-][\p{L}\p{Nd}]+)*\z""";

    public DeliveryAddressValidator() {
        RuleFor(address => address.City).Must(value => Matches(value, 80, NamePattern)).WithErrorCode("ADDRESS_CITY_INVALID");
        RuleFor(address => address.Street).Must(value => Matches(value, 120, NamePattern)).WithErrorCode("ADDRESS_STREET_INVALID");
        RuleFor(address => address.House).Must(value => Matches(value, 24, HousePattern)).WithErrorCode("ADDRESS_HOUSE_INVALID");

        RuleFor(address => address.Building).Must(value => value is null || Matches(value, 20, UnitPattern))
            .WithErrorCode("ADDRESS_BUILDING_INVALID");
        RuleFor(address => address.Apartment).Must(value => value is null || Matches(value, 20, UnitPattern))
            .WithErrorCode("ADDRESS_APARTMENT_INVALID");
    }

    private static bool Matches(string? value, int maxLength, string pattern) =>
        value is { Length: > 0 } && value.Length <= maxLength && Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant);
}
