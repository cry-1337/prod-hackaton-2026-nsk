using System.Net.Mail;
using FluentValidation;

namespace Backend.Application.Common.Validation;

internal static class EmailRules {
    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("EMAIL_REQUIRED")
            .MaximumLength(254).WithErrorCode("EMAIL_TOO_LONG")
            .Must(IsMailbox).WithErrorCode("EMAIL_INVALID");

    private static bool IsMailbox(string value) {
        var email = value.Trim();

        return MailAddress.TryCreate(email, out var address) && address.Address == email;
    }
}
