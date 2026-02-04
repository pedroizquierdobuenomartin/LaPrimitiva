using System;

namespace LaPrimitiva.Application.DTOs
{
    public record WinningDrawDto
    {
        public Guid Id { get; set; }
        public DateTime DrawDate { get; set; } = DateTime.Today;
        public int Number1 { get; set; }
        public int Number2 { get; set; }
        public int Number3 { get; set; }
        public int Number4 { get; set; }
        public int Number5 { get; set; }
        public int Number6 { get; set; }
        public int Complementario { get; set; }
        public int Reintegro { get; set; }
        public string? Joker { get; set; }

        public WinningDrawDto() { }

        public WinningDrawDto(Guid id, DateTime drawDate, int n1, int n2, int n3, int n4, int n5, int n6, int comp, int rein, string? joker)
        {
            Id = id;
            DrawDate = drawDate;
            Number1 = n1;
            Number2 = n2;
            Number3 = n3;
            Number4 = n4;
            Number5 = n5;
            Number6 = n6;
            Complementario = comp;
            Reintegro = rein;
            Joker = joker;
        }
    }
}
