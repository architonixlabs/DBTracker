using System.ComponentModel.DataAnnotations;

namespace DBTracker.Core.Validation;

/// <summary>Validates email format only when a value is supplied — contact email is not mandatory.</summary>
public class OptionalEmailAddressAttribute : ValidationAttribute
{
    private readonly EmailAddressAttribute _inner = new();

    public OptionalEmailAddressAttribute()
    {
        ErrorMessage = "Enter a valid email address.";
    }

    public override bool IsValid(object? value)
    {
        if (value is string s && string.IsNullOrWhiteSpace(s))
        {
            return true;
        }
        return _inner.IsValid(value);
    }
}
