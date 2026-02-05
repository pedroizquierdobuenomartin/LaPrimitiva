using System.Collections.Generic;

namespace LaPrimitiva.Application.DTOs
{
    public record CombinationResult
    {
        public List<int> Numbers { get; init; } = new();
        public int Reintegro { get; init; }
        public Dictionary<string, object> DebugInfo { get; init; } = new();
    }
}
