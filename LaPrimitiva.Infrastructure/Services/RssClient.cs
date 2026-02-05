using System.Net.Http;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Interfaces;

namespace LaPrimitiva.Infrastructure.Services
{
    public class RssClient(HttpClient httpClient) : IRssClient
    {
        private const string RssUrl = "https://www.loteriasyapuestas.es/es/la-primitiva/resultados/.formatoRSS";

        public async Task<string?> GetRssXmlAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, RssUrl);
                
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "es-ES,es;q=0.8,en-US;q=0.5,en;q=0.3");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-User", "?1");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                // Rethrow the exception to be caught by DrawNotificationService
                throw new Exception($"Error de red: {ex.Message}", ex); 
            }
        }
    }
}
