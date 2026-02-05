using System;

namespace LaPrimitiva.Domain.Models
{
    public record RssDraw(
        DateTime Date,
        int[] Numbers,
        int Complementary,
        int Reintegro,
        int? Joker = null
    );
}
