using System.Threading.Tasks;

namespace LaPrimitiva.Domain.Interfaces
{
    public interface IRssClient
    {
        Task<string?> GetRssXmlAsync();
    }
}
