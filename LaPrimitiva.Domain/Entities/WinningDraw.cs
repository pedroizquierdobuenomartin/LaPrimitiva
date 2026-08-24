using System;
using System.Linq;

namespace LaPrimitiva.Domain.Entities
{
    public class WinningDraw
    {
        public const int MinimumNumber = 1;
        public const int MaximumNumber = 49;
        public const int MinimumReintegro = 0;
        public const int MaximumReintegro = 9;
        public const int JokerLength = 7;

        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime DrawDate { get; set; }
        public int Number1 { get; set; }
        public int Number2 { get; set; }
        public int Number3 { get; set; }
        public int Number4 { get; set; }
        public int Number5 { get; set; }
        public int Number6 { get; set; }
        public int Complementario { get; set; }
        public int Reintegro { get; set; }
        public string? Joker { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public void Validate()
        {
            var mainNumbers = GetMainNumbers();
            if (mainNumbers.Any(number => number is < MinimumNumber or > MaximumNumber))
            {
                throw new InvalidOperationException($"Los números principales deben estar entre {MinimumNumber} y {MaximumNumber}.");
            }

            if (mainNumbers.Distinct().Count() != mainNumbers.Length)
            {
                throw new InvalidOperationException("Los números principales no se pueden repetir.");
            }

            if (Complementario is < MinimumNumber or > MaximumNumber)
            {
                throw new InvalidOperationException($"El complementario debe estar entre {MinimumNumber} y {MaximumNumber}.");
            }

            if (mainNumbers.Contains(Complementario))
            {
                throw new InvalidOperationException("El complementario no puede repetir un número principal.");
            }

            if (Reintegro is < MinimumReintegro or > MaximumReintegro)
            {
                throw new InvalidOperationException($"El reintegro debe estar entre {MinimumReintegro} y {MaximumReintegro}.");
            }

            if (Joker is not null &&
                (Joker.Length != JokerLength || Joker.Any(character => !char.IsAsciiDigit(character))))
            {
                throw new InvalidOperationException($"El Joker debe contener exactamente {JokerLength} dígitos.");
            }
        }

        public bool IsValid()
        {
            try
            {
                Validate();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public int[] GetMainNumbers() =>
        [
            Number1,
            Number2,
            Number3,
            Number4,
            Number5,
            Number6
        ];
    }
}
