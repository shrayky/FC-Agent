using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using CSharpFunctionalExtensions;
using Polly;
using Polly.Extensions.Http;

namespace Shared.Http
{
    public static class HttpClientExtensions
    {
        public static Result<HttpClient, string> CreateClientSafely(
            this IHttpClientFactory httpClientFactory,
            string clientName,
            ILogger logger)
        {
            try
            {
                var client = httpClientFactory.CreateClient(clientName);
                return Result.Success<HttpClient, string>(client);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Клиент '{ClientName}' не зарегистрирован в DI", clientName);
                return Result.Failure<HttpClient, string>($"Клиент '{clientName}' не зарегистрирован");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Неожиданная ошибка при создании HttpClient '{ClientName}'", clientName);
                return Result.Failure<HttpClient, string>($"Ошибка создания клиента: {ex.Message}");
            }
        }

        public static async Task<Result<HttpResponseMessage, string>> SendRequestSafelyAsync(
            this HttpClient httpClient,
            Func<HttpClient, Task<HttpResponseMessage>> requestFunc,
            ILogger logger,
            string operationName = "HTTP запрос")
        {
            try
            {
                var response = await requestFunc(httpClient);
                return Result.Success<HttpResponseMessage, string>(response);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Сетевая ошибка при выполнении {OperationName}", operationName);
                return Result.Failure<HttpResponseMessage, string>($"Сетевая ошибка: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                logger.LogError(ex, "Таймаут при выполнении {OperationName}", operationName);
                return Result.Failure<HttpResponseMessage, string>("Превышено время ожидания");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Неожиданная ошибка при выполнении {OperationName}", operationName);
                return Result.Failure<HttpResponseMessage, string>($"Ошибка запроса: {ex.Message}");
            }
        }
        
        public static async Task<Result<Stream>> DownloadFileWithResumeAsync(
            this HttpClient httpClient,
            string url,
            string destinationPath,
            ILogger logger,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        logger.LogWarning("Повтор попытки {RetryCount} загрузки файла через {Timespan}", retryCount, timespan);
                    });

            try
            {
                var fileInfo = new FileInfo(destinationPath);
                var existingLength = fileInfo.Exists ? fileInfo.Length : 0;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (existingLength > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existingLength, null);
                    logger.LogInformation("Продолжаем загрузку с позиции {Position} байт", existingLength);
                }

                var response = await retryPolicy.ExecuteAsync(async () =>
                    await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken));

                if (!response.IsSuccessStatusCode)
                {
                    var error = $"Ошибка загрузки файла: {response.StatusCode} {response.ReasonPhrase}";
                    logger.LogError(error);
                    return Result.Failure<Stream>(error);
                }

                var totalBytes = existingLength + (response.Content.Headers.ContentLength ?? 0);
                logger.LogInformation("Загружаем файл. Размер: {Size} байт, докачка: {Resume}", 
                    totalBytes, existingLength > 0);

                var memoryStream = new MemoryStream();
                
                if (existingLength > 0)
                {
                    await using var existingFile = File.OpenRead(destinationPath);
                    await existingFile.CopyToAsync(memoryStream, cancellationToken);
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[8192];
                var totalBytesRead = existingLength;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }

                memoryStream.Position = 0;
                return Result.Success<Stream>(memoryStream);
            }
            catch (Exception ex)
            {
                var error = $"Ошибка загрузки файла: {ex.Message}";
                logger.LogError(ex, error);
                return Result.Failure<Stream>(error);
            }
        }
        
    }
}