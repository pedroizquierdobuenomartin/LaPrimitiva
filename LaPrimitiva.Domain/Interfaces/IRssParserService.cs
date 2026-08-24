using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Domain.Interfaces
{
    public interface IRssParserService
    {
        Task<IReadOnlyList<RssDraw>> ParseRssAsync(
            string xmlContent,
            CancellationToken cancellationToken = default);
    }
}
