using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Application.Services;
using LaPrimitiva.Application.Interfaces;
using LaPrimitiva.Domain.Models;
using LaPrimitiva.Domain.Errors;

namespace LaPrimitiva.Application.Services
{
    public class DrawNotificationService(
        IRssClient rssClient,
        IRssParserService rssParser,
        IWinningDrawService winningDrawService,
        GlobalState globalState,
        IApplicationErrorReporter errorReporter) : IDrawNotificationService
    {
        private const string LastCheckedDateKey = "LastCheckedDrawDate";
        private static readonly SemaphoreSlim UpdateLock = new(1, 1);

        public async Task CheckForNewDrawsAsync(CancellationToken cancellationToken = default)
        {
            if (!await UpdateLock.WaitAsync(0, cancellationToken))
                return;

            globalState.IsLoading = true;
            globalState.LastError = null;
            globalState.LastErrorReference = null;
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(RssFeedLimits.Timeout);

            try
            {
                var xml = await rssClient.GetRssXmlAsync(timeoutSource.Token);
                if (string.IsNullOrEmpty(xml))
                {
                    throw new ExternalDataFormatException("SELAE", "rss.empty");
                }

                var parsedDraws = await rssParser.ParseRssAsync(xml, timeoutSource.Token);
                var rssDraws = parsedDraws.OrderByDescending(d => d.Date).ToList();
                if (!rssDraws.Any())
                {
                    throw new ExternalDataFormatException("SELAE", "rss.no-valid-items");
                }

                var rssDates = rssDraws
                    .Select(draw => draw.Date.Date)
                    .Distinct()
                    .ToArray();
                var existingDates = await winningDrawService.GetExistingDrawDatesAsync(rssDates);
                var existingDateSet = existingDates
                    .Select(date => date.Date)
                    .ToHashSet();
                rssDraws = rssDraws
                    .Where(draw => !existingDateSet.Contains(draw.Date.Date))
                    .GroupBy(draw => draw.Date.Date)
                    .Select(group => group.First())
                    .ToList();

                // Update global state
                globalState.RecentDraws = rssDraws.Take(10).ToList();
                globalState.NewDrawsCount = rssDraws.Count;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                var timeout = new ExternalServiceTimeoutException("SELAE", RssFeedLimits.Timeout, exception);
                errorReporter.Report(timeout, "RssImport");
                globalState.LastError = ApplicationError.FromException(timeout);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IErrorException)
            {
                errorReporter.Report(ex, "RssImport");
                globalState.LastError = ApplicationError.FromException(ex);
            }
            catch (Exception ex)
            {
                var reference = errorReporter.Report(ex, "RssImport");
                globalState.LastError = ApplicationError.FromException(ex);
                globalState.LastErrorReference = reference;
            }
            finally
            {
                globalState.IsLoading = false;
                UpdateLock.Release();
            }
        }
    }
}
