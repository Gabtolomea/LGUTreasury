using System.ComponentModel.DataAnnotations;
using LGUTreasury.Models;

namespace LGUTreasury.Tests;

public class ContactNumberTests
{
    [Theory]
    [InlineData("09171234567")]
    [InlineData("0917 123 4567")]
    [InlineData("0917-123-4567")]
    [InlineData("(0917) 123-4567")]
    [InlineData("+639171234567")]
    public void NumericContactNumbers_AreAccepted(string value)
    {
        Assert.True(ContactNumberRules.IsValid(value));
        Assert.Empty(Validate(value));
    }

    [Theory]
    [InlineData("0917ABC4567")]
    [InlineData("0917-abc-4567")]
    [InlineData("CALL ME")]
    public void ContactNumbersWithLetters_AreRejected(string value)
    {
        Assert.False(ContactNumberRules.IsValid(value));
        Assert.True(ContactNumberRules.HasLetters(value));
        Assert.Contains(Validate(value), e => e.ErrorMessage == ContactNumberRules.LettersErrorMessage);
    }

    [Theory]
    [InlineData("0917@4567")]
    [InlineData("0917#4567")]
    public void ContactNumbersWithOtherCharacters_AreRejected(string value)
    {
        Assert.False(ContactNumberRules.IsValid(value));
        Assert.False(ContactNumberRules.HasLetters(value));
        Assert.Contains(Validate(value), e => e.ErrorMessage == ContactNumberRules.LettersErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyContactNumber_IsAllowed_BecauseTheFieldIsOptional(string? value)
    {
        Assert.True(ContactNumberRules.IsValid(value));
        Assert.Empty(Validate(value));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890123456")]
    public void ContactNumbersWithWrongDigitCount_AreRejected(string value)
    {
        Assert.False(ContactNumberRules.IsValid(value));
        Assert.Contains(Validate(value), e => e.ErrorMessage == ContactNumberRules.LengthErrorMessage);
    }

    [Theory]
    [InlineData("09171234567", "09171234567")]
    [InlineData("0917 123 4567", "09171234567")]
    [InlineData("0917-123-4567", "09171234567")]
    [InlineData("+63 917 123 4567", "639171234567")]
    [InlineData("0917ABC4567", "09174567")]
    public void Normalize_KeepsDigitsOnly_AndDropsLetters(string input, string expected)
    {
        Assert.Equal(expected, ContactNumberRules.Normalize(input));
    }

    [Fact]
    public void Normalize_ReturnsNull_WhenThereAreNoDigits()
    {
        Assert.Null(ContactNumberRules.Normalize("none"));
        Assert.Null(ContactNumberRules.Normalize(null));
        Assert.Null(ContactNumberRules.Normalize("   "));
    }

    [Theory]
    [InlineData("09171234567", "0917-123-4567")]
    [InlineData("0917123456", "0917123456")]
    [InlineData("0917ABC4567", "09174567")]
    [InlineData(null, null)]
    public void Format_RendersLocalMobileNumbers(string? input, string? expected)
    {
        Assert.Equal(expected, ContactNumberRules.Format(input));
    }

    [Fact]
    public void StoredContactNumber_NeverKeepsLetters()
    {
        var stored = ContactNumberRules.Normalize("0917ABC4567");
        Assert.NotNull(stored);
        Assert.DoesNotContain(stored!, c => char.IsLetter(c));
    }

    private static List<ValidationResult> Validate(string? value)
    {
        var results = new List<ValidationResult>();
        var attribute = new ContactNumberAttribute();
        var context = new ValidationContext(new ContactHolder { ContactNumber = value });
        var result = attribute.GetValidationResult(value, context);
        if (result != null) results.Add(result);
        return results;
    }

    private sealed class ContactHolder
    {
        [ContactNumber]
        public string? ContactNumber { get; set; }
    }
}
