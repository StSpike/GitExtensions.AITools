using GitExtensions.AITools.LlmProviders.OneCModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace GitExtensions.AITools.LlmProviders
{
    /// <summary>
    /// HTTP клиент для работы с API 1С.ai
    /// </summary>
    public class OneCApiClient 
    {
        private const string BaseUrl = "https://code.1c.ai";
        private const int MaxActiveSessions = 10;
        private const int SessionTtl = 3600;
        private const string FileConfigName = "OneC.sessions";

        private readonly Dictionary<string, ConversationSession> _sessions;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string _workDirGit;
        
        public OneCApiClient(string oneCAiToken, string workDirGit)
        {
            _workDirGit = workDirGit;

            string confDir = Path.Combine(_workDirGit, FileConfigName);
            if (File.Exists(confDir))
            {
                try
                {
                    using (FileStream fs = new FileStream(confDir, FileMode.OpenOrCreate))
                    {
                        _sessions = JsonSerializer.Deserialize<Dictionary<string, ConversationSession>>(fs);
                    }
                }
                catch
                {
                    _sessions = new Dictionary<string, ConversationSession>();
                }
            }
            else
            {
                _sessions = new Dictionary<string, ConversationSession>();
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            };

            // Создаем HTTP клиент
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Настраиваем заголовки
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            _httpClient.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            /*_httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            _httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));*/
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("ru-ru"));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-us", 0.8));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
            _httpClient.DefaultRequestHeaders.Add("Authorization", oneCAiToken);
            _httpClient.DefaultRequestHeaders.Add("Origin", BaseUrl);
            _httpClient.DefaultRequestHeaders.Add("Referer", $"{BaseUrl}/chat/");
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows NT 10.0; Win64; x64)"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppleWebKit", "620.1"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(KHTML, like Gecko)"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JavaFX", "22"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Safari", "620.1"));
        }

        /// <summary>
        /// Создать новую дискуссию
        /// </summary>
        public async Task<ConversationSession> CreateConversation(string programmingLanguage = "")
        {
            try
            {
                var request = new ConversationRequest
                {
                    ProgrammingLanguage = programmingLanguage
                };

                var json = JsonSerializer.Serialize(request);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string url = $"{BaseUrl}/chat_api/v1/conversations/";
                
                using var HttpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                HttpRequest.Content = content;
                HttpRequest.Headers.TryAddWithoutValidation("Session-Id", "");
                
                var response = await _httpClient.SendAsync(HttpRequest);
                var responseJson = await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new ApiError($"Message send error: {(int)response.StatusCode}, {responseJson}", (int)response.StatusCode);
                }
                
                var conversationResponse = JsonSerializer.Deserialize<ConversationResponse>(responseJson);

                if (conversationResponse?.Uuid == null)
                {
                    throw new ApiError("Не удалось получить ID дискуссии");
                }

                var session = new ConversationSession
                {
                    ConversationId = conversationResponse.Uuid,
                    CreatedAt = DateTime.Now,
                    LastUsed = DateTime.Now
                };

                _sessions[conversationResponse.Uuid] = session;

                return session;
            }
            catch (HttpRequestException ex)
            {
                throw new ApiError($"Ошибка HTTP при создании дискуссии: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new ApiError($"Ошибка разбора JSON при создании дискуссии: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new ApiError($"Неожиданная ошибка при создании дискуссии: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Отправить сообщение
        /// </summary>
        public async Task<string> SendMessage(string conversationId, string message, string parentUuid = null)
        {
            try
            {
                // Ensure session exists
                if (!_sessions.ContainsKey(conversationId))
                {
                    _sessions[conversationId] = new ConversationSession
                    {
                        ConversationId = conversationId
                    };
                }

                var session = _sessions[conversationId];
                
                // Fallback parent_uuid to last known assistant uuid from session
                if (string.IsNullOrEmpty(parentUuid) && !string.IsNullOrEmpty(session.LastMessageUuid))
                {
                    parentUuid = session.LastMessageUuid;
                }

                var requestData = MessageRequest.FromInstruction(message, parentUuid);
                var payload = JsonSerializer.SerializeToDocument(requestData, _jsonOptions);
                var payloadObj = JsonSerializer.Deserialize<Dictionary<string, object>>(payload.RootElement.GetRawText());

                var json = JsonSerializer.Serialize(payloadObj, _jsonOptions);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat_api/v1/conversations/{conversationId}/messages");
                requestMessage.Content = new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json");

                // Добавляем заголовок для SSE
                requestMessage.Headers.TryAddWithoutValidation("Accept", "text/event-stream");

                var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    throw new ApiError($"Message send error: {(int)response.StatusCode}, {responseJson}", (int)response.StatusCode);
                }
                
                var stream = await response.Content.ReadAsStreamAsync();
                var fullText = await ParseSSEResponse(stream, session);

                // Обновляем время последнего использования+
                session.UpdateUsage();

                await SaveSessions();
                
                return fullText;
            }
            catch (HttpRequestException ex)
            {
                throw new ApiError($"Ошибка HTTP при отправке сообщения: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new ApiError($"Неожиданная ошибка при отправке сообщения: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Разобрать SSE ответ
        /// </summary>
        private async Task<string> ParseSSEResponse(System.IO.Stream stream, ConversationSession session)
        {
            var fullText = new StringBuilder();
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);
                       
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    try
                    {
                        var chunk = JsonSerializer.Deserialize<MessageChunk>(data, _jsonOptions);

                        if (chunk == null) continue;

                        // --- Пропускаем user echo ---
                        if (chunk.Role == "user" && chunk.Finished)
                        {
                            continue;
                        }

                        if (chunk?.Content != null && chunk.Content.TryGetValue("content", out var content))
                        {
                            fullText.Append(content?.ToString());
                            session.LastMessageUuid = chunk.Uuid;
                        }

                        // Если сообщение завершено, выходим
                        if (chunk?.Finished == true)
                            break;
                    }
                    catch (JsonException ex)
                    {
                        throw new ApiError($"Ошибка разбора SSE чанка: {data}", ex);
                    }
                }
            }

            return fullText.ToString().Trim();
        }

        /// <summary>
        /// Получить существующую сессию или создать новую
        /// </summary>
        public async Task<ConversationSession> GetOrCreateSession(bool createNew = false, string programmingLanguage = null)
        {
            // Очищаем устаревшие сессии
            await CleanupOldSessions();

            // Если требуется новая сессия или нет активных сессий
            if (createNew || _sessions.Count == 0)
            {
                return await CreateConversation(programmingLanguage);
            }

            // Проверяем лимит активных сессий
            if (_sessions.Count >= MaxActiveSessions)
            {
                // Удаляем самую старую сессию
                var oldestSession = _sessions.OrderBy(kvp => kvp.Value.LastUsed).First();
                _sessions.Remove(oldestSession.Key);
            }

            // Возвращаем самую свежую сессию
            var recentSession = _sessions.OrderByDescending(kvp => kvp.Value.LastUsed).First();

            await SaveSessions();

            return _sessions[recentSession.Key];
        }

        /// <summary>
        /// Сохранить сессии в файл
        /// </summary>
        public async Task SaveSessions()
        {
            string confDir = Path.Combine(_workDirGit, FileConfigName);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            FileMode fileMode = File.Exists(confDir) ? FileMode.Truncate : FileMode.OpenOrCreate;

            using (FileStream fs = new FileStream(confDir, fileMode))
            {
                JsonSerializer.Serialize<Dictionary<string, ConversationSession>>(fs, _sessions, options);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Очистка устаревших сессий
        /// </summary>
        private async Task CleanupOldSessions()
        {
            var currentTime = DateTime.Now;
            var ttl = TimeSpan.FromSeconds(SessionTtl);

            var expiredSessions = _sessions
                .Where(kvp => currentTime - kvp.Value.LastUsed > ttl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.Remove(sessionId);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Получить информацию о сессии
        /// </summary>
        public ConversationSession GetSession(string conversationId)
        {
            _sessions.TryGetValue(conversationId, out var session);
            return session;
        }

        /// <summary>
        /// Получить все активные сессии
        /// </summary>
        public IReadOnlyList<ConversationSession> GetAllSessions()
        {
            return _sessions.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Закрыть HTTP клиент
        /// </summary>
        public async Task Close()
        {
            _httpClient?.Dispose();
            await Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await Close();
            GC.SuppressFinalize(this);
        }
    
    }
       
    /// <summary>
    /// Content delta structure in streaming response.
    /// </summary>
    public class ContentDelta
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("reasoning_content")]
        public string ReasoningContent { get; set; }

        [JsonPropertyName("tool_calls")]
        public object ToolCalls { get; set; }
    }
            

}