using LaPrimitiva.Domain.Entities;
using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Tests;

public class WinningDrawTests
{
    [Fact]
    public void Validate_WhenDrawIsValid_DoesNotThrow()
    {
        var draw = CreateValidDraw();

        draw.Validate();

        Assert.True(draw.IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void Validate_WhenMainNumberIsOutsideRange_Throws(int invalidNumber)
    {
        var draw = CreateValidDraw();
        draw.Number1 = invalidNumber;

        var exception = Assert.Throws<BusinessRuleException>(draw.Validate);

        Assert.Contains("entre 1 y 49", exception.Message);
    }

    [Fact]
    public void Validate_WhenMainNumberIsDuplicated_Throws()
    {
        var draw = CreateValidDraw();
        draw.Number6 = draw.Number5;

        Assert.Throws<BusinessRuleException>(draw.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void Validate_WhenComplementarioIsOutsideRange_Throws(int invalidNumber)
    {
        var draw = CreateValidDraw();
        draw.Complementario = invalidNumber;

        Assert.Throws<BusinessRuleException>(draw.Validate);
    }

    [Fact]
    public void Validate_WhenComplementarioRepeatsMainNumber_Throws()
    {
        var draw = CreateValidDraw();
        draw.Complementario = draw.Number3;

        Assert.Throws<BusinessRuleException>(draw.Validate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Validate_WhenReintegroIsOutsideRange_Throws(int invalidReintegro)
    {
        var draw = CreateValidDraw();
        draw.Reintegro = invalidReintegro;

        Assert.Throws<BusinessRuleException>(draw.Validate);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678")]
    [InlineData("12345A7")]
    public void Validate_WhenJokerIsNotSevenDigits_Throws(string invalidJoker)
    {
        var draw = CreateValidDraw();
        draw.Joker = invalidJoker;

        Assert.Throws<BusinessRuleException>(draw.Validate);
    }

    private static WinningDraw CreateValidDraw() => new()
    {
        DrawDate = new DateTime(2026, 8, 24),
        Number1 = 1,
        Number2 = 8,
        Number3 = 15,
        Number4 = 22,
        Number5 = 35,
        Number6 = 49,
        Complementario = 7,
        Reintegro = 0,
        Joker = "0123456"
    };
}
