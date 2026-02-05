using System.Collections.Generic;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Domain.Interfaces
{
    public interface IRssParserService
    {
        IEnumerable<RssDraw> ParseRss(string xmlContent);
    }
}
