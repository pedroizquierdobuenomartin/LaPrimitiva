using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaPrimitiva.Domain.Interfaces;
using LaPrimitiva.Domain.Errors;
using LaPrimitiva.Domain.Models;
using Microsoft.Extensions.Logging;

namespace LaPrimitiva.Infrastructure.Services
{
    public class RssClient(HttpClient httpClient, ILogger<RssClient> logger) : IRssClient
    {
        private const string RssUrl = "https://www.loteriasyapuestas.es/es/la-primitiva/resultados/.formatoRSS";

        public async Task<string?> GetRssXmlAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Iniciando descarga del feed RSS oficial. {Operation}", "RssImport");
                using var request = new HttpRequestMessage(HttpMethod.Get, RssUrl);
                
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "es-ES,es;q=0.8,en-US;q=0.5,en;q=0.3");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Upgrade-Insecure-Requests", "1");
                request.Headers.Add("Sec-Fetch-Dest", "document");
                request.Headers.Add("Sec-Fetch-Mode", "navigate");
                request.Headers.Add("Sec-Fetch-Site", "none");
                request.Headers.Add("Sec-Fetch-User", "?1");

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength > RssFeedLimits.MaxBytes)
                {
                    throw new InvalidDataException(
                        $"El feed RSS supera el límite de {RssFeedLimits.MaxBytes} bytes.");
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var content = new MemoryStream();
                var buffer = ArrayPool<byte>.Shared.Rent(81920);

                try
                {
                    var totalBytes = 0;
                    while (true)
                    {
                        var bytesToRead = Math.Min(buffer.Length, RssFeedLimits.MaxBytes - totalBytes + 1);
                        var bytesRead = await responseStream.ReadAsync(
                            buffer.AsMemory(0, bytesToRead),
                            cancellationToken);

                        if (bytesRead == 0)
                            break;

                        totalBytes += bytesRead;
                        if (totalBytes > RssFeedLimits.MaxBytes)
                        {
                            throw new InvalidDataException(
                                $"El feed RSS supera el límite de {RssFeedLimits.MaxBytes} bytes.");
                        }

                        await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                content.Position = 0;
                using var reader = new StreamReader(
                    content,
                    ResolveEncoding(response.Content.Headers.ContentType?.CharSet),
                    detectEncodingFromByteOrderMarks: true);
                var xml = await reader.ReadToEndAsync(cancellationToken);
                logger.LogInformation(
                    "Feed RSS descargado correctamente. {Operation} {ByteCount} {StatusCode}",
                    "RssImport",
                    content.Length,
                    (int)response.StatusCode);
                return xml;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExternalServiceTimeoutException("SELAE", httpClient.Timeout, exception);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning("El feed RSS fue rechazado por los límites de seguridad. {Operation}", "RssImport");
                throw new ExternalDataFormatException("SELAE", "rss.size-limit", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ExternalServiceUnavailableException("SELAE", exception);
            }
        }

        private static Encoding ResolveEncoding(string? charset)
        {
            if (string.IsNullOrWhiteSpace(charset))
                return Encoding.UTF8;

            try
            {
                return Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8;
            }
        }
    }
}
