using System.Threading.Tasks;

namespace LaPrimitiva.Domain.Interfaces
{
    public interface ILocalStorageService
    {
        Task SetItemAsync<T>(string key, T value);
        Task<T?> GetItemAsync<T>(string key);
    }
}
