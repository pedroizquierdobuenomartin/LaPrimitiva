using System;

namespace LaPrimitiva.Domain.Entities
{
    public class WinningDraw
    {
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
    }
}
