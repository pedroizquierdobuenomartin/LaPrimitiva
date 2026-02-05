using System;
using System.Linq;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Models;

namespace LaPrimitiva.Application.Services
{
    public class DrawNotificationService(
        IRssClient rssClient,
        IRssParserService rssParser,
        ILocalStorageService localStorage,
        IWinningDrawService winningDrawService,
        GlobalState globalState) : IDrawNotificationService
    {
        private const string LastCheckedDateKey = "LastCheckedDrawDate";

        public async Task CheckForNewDrawsAsync()
        {
            globalState.IsLoading = true;
            globalState.LastError = null;

            try
            {
                var xml = await rssClient.GetRssXmlAsync();
                if (string.IsNullOrEmpty(xml))
                {
                    globalState.LastError = "Error: El servidor de Loterías y Apuestas (SELAE) bloqueó la solicitud (403 Forbidden).";
                    return;
                }

                var rssDraws = rssParser.ParseRss(xml).OrderByDescending(d => d.Date).ToList();
                if (!rssDraws.Any())
                {
                    globalState.LastError = "Error: No se pudieron encontrar sorteos en el feed de SELAE.";
                    return;
                }

                // Filter draws by the latest historical date in database
                var latestHistoricalDate = await winningDrawService.GetLatestDrawDateAsync();
                if (latestHistoricalDate.HasValue)
                {
                    // Use .Date for comparison to avoid time issues if present
                    rssDraws = rssDraws.Where(d => d.Date.Date > latestHistoricalDate.Value.Date).ToList();
                }

                // Update global state
                globalState.RecentDraws = rssDraws.Take(10).ToList();
                globalState.NewDrawsCount = globalState.RecentDraws.Count;
            }
            catch (Exception ex)
            {
                globalState.LastError = $"Error al sincronizar sorteos: {ex.Message}";
            }
            finally
            {
                globalState.IsLoading = false;
            }
        }
    }
}
