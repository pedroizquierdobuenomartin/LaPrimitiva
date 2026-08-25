using System;
using System.Linq;
using System.Threading;
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
        IWinningDrawService winningDrawService,
        GlobalState globalState) : IDrawNotificationService
    {
        private const string LastCheckedDateKey = "LastCheckedDrawDate";
        private static readonly SemaphoreSlim UpdateLock = new(1, 1);

        public async Task CheckForNewDrawsAsync(CancellationToken cancellationToken = default)
        {
            if (!await UpdateLock.WaitAsync(0, cancellationToken))
                return;

            globalState.IsLoading = true;
            globalState.LastError = null;
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(RssFeedLimits.Timeout);

            try
            {
                var xml = await rssClient.GetRssXmlAsync(timeoutSource.Token);
                if (string.IsNullOrEmpty(xml))
                {
                    globalState.LastError = "Error: El servidor de Loterías y Apuestas (SELAE) bloqueó la solicitud (403 Forbidden).";
                    return;
                }

                var parsedDraws = await rssParser.ParseRssAsync(xml, timeoutSource.Token);
                var rssDraws = parsedDraws.OrderByDescending(d => d.Date).ToList();
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
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                globalState.LastError =
                    $"Error: La sincronización RSS superó el límite de {RssFeedLimits.Timeout.TotalSeconds:0} segundos.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                globalState.LastError = $"Error al sincronizar sorteos: {ex.Message}";
            }
            finally
            {
                globalState.IsLoading = false;
                UpdateLock.Release();
            }
        }
    }
}
