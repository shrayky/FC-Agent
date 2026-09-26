using System.Net;
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
        
        /// <summary>
        /// Скачивает файл в <paramref name="destinationPath"/>, записывая данные на диск по мере поступления
        /// и докачивая уже загруженную часть через HTTP Range. Возвращает путь к скачанному файлу.
        /// </summary>
        public static async Task<Result<string>> DownloadFileWithResumeAsync(
            this HttpClient httpClient,
            string url,
            string destinationPath,
            ILogger logger,
            IProgress<long>? progress = null,
            CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Будет скачен файл в {destinationPath}", destinationPath);
            logger.LogDebug("Из источника {address}", url);

            // Не retryable: 4xx кроме 408/429 — повторять их бессмысленно, а 5xx/сетевые сбои/таймаут
            // клиента повторяем: загрузка продолжится с уже скачанной позиции.
            // HttpIOException — обрыв соединения при чтении тела ответа (.NET кидает его, например,
            // с HttpRequestError.ResponseEnded, когда сервер не дослал файл).
            var retryPolicy = Policy
                .Handle<HttpRequestException>(IsTransient)
                .Or<HttpIOException>()
                .Or<OperationCanceledException>(ex => !cancellationToken.IsCancellationRequested)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (Exception failure, TimeSpan timespan, int retryCount, Context context) =>
                    {
                        logger.LogWarning(
                            "Попытка загрузки файла не удалась ({Failure}: {Reason}), повтор {RetryCount} через {Timespan}. Докачка продолжится с текущей позиции",
                            failure.GetType().Name, failure.Message, retryCount, timespan);
                    });

            try
            {
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var totalBytesRead = await retryPolicy.ExecuteAsync(async () =>
                {
                    // Позицию докачки пересчитываем на каждой попытке: предыдущая могла успеть записать часть данных.
                    var existingLength = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0;

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    if (existingLength > 0)
                    {
                        request.Headers.Range = new RangeHeaderValue(existingLength, null);
                        logger.LogInformation("Продолжаем загрузку с позиции {Position} байт", existingLength);
                    }

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existingLength > 0)
                    {
                        logger.LogWarning("Сервер отклонил докачку с позиции {Position} байт, начинаю загрузку заново", existingLength);
                        TryDeleteFile(destinationPath);
                        throw new HttpRequestException($"Сервер отклонил докачку файла: {response.StatusCode}");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            $"Сервер вернул {response.StatusCode} {response.ReasonPhrase}",
                            null,
                            response.StatusCode);
                    }

                    var contentRange = response.Content.Headers.ContentRange;
                    var isResume = response.StatusCode == HttpStatusCode.PartialContent
                                   && contentRange?.From == existingLength
                                   && existingLength > 0;

                    if (existingLength > 0 && !isResume)
                    {
                        // Сервер не поддержал Range и отдал файл целиком — пишем с нуля, иначе данные задублируются.
                        logger.LogWarning(
                            "Сервер не поддержал докачку (код {StatusCode}), файл будет загружен заново",
                            response.StatusCode);
                        TryDeleteFile(destinationPath);
                        existingLength = 0;
                    }

                    var expectedTotal = contentRange?.Length ?? (existingLength + response.Content.Headers.ContentLength);

                    await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(
                        destinationPath,
                        existingLength > 0 ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true);

                    var buffer = new byte[81920];
                    var written = 0L;
                    int bytesRead;

                    while (true)
                    {
                        try
                        {
                            bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
                        }
                        catch (IOException ioException) when (ioException is not HttpIOException)
                        {
                            // Сокетные ошибки чтения приходят как обычный IOException: приводим их
                            // к HttpIOException, чтобы политика повторов докачала файл, а не уронила загрузку.
                            throw new HttpIOException(
                                HttpRequestError.ResponseEnded,
                                "Соединение оборвалось во время загрузки файла",
                                ioException);
                        }

                        if (bytesRead == 0)
                            break;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        written += bytesRead;
                        progress?.Report(existingLength + written);
                    }

                    await fileStream.FlushAsync(cancellationToken);

                    var total = existingLength + written;
                    if (expectedTotal.HasValue && total != expectedTotal.Value)
                    {
                        // Обрыв соединения: следующая попытка докачает недостающее с позиции total.
                        throw new HttpRequestException($"Файл скачан не полностью: {total} из {expectedTotal.Value} байт");
                    }

                    return total;
                });

                logger.LogInformation("Файл загружен: {Size} байт", totalBytesRead);
                return Result.Success(destinationPath);
            }
            catch (Exception ex)
            {
                var error = $"Ошибка загрузки файла: {ex.Message}";
                logger.LogError(ex, error);
                return Result.Failure<string>(error);
            }
        }

        private static bool IsTransient(HttpRequestException exception) =>
            exception.StatusCode is null
            || exception.StatusCode == HttpStatusCode.RequestTimeout
            || exception.StatusCode == HttpStatusCode.TooManyRequests
            || (int)exception.StatusCode >= 500;

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Файл занят другим процессом — при следующей попытке он будет перезаписан.
            }
        }
        
    }
}