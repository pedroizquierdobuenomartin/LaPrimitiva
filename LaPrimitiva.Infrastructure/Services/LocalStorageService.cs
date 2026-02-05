using System.Text.Json;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Interfaces;
using Microsoft.JSInterop;

namespace LaPrimitiva.Infrastructure.Services
{
    public class LocalStorageService(IJSRuntime jsRuntime) : ILocalStorageService
    {
        public async Task SetItemAsync<T>(string key, T value)
        {
            var json = JsonSerializer.Serialize(value);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }

        public async Task<T?> GetItemAsync<T>(string key)
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
