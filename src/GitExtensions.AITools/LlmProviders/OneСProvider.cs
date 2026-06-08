using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static GitCommands.Git.Extended.MoveCommand;

namespace GitExtensions.AITools.LlmProviders
{
    internal sealed class OneСProvider : ILlmProvider
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        
        private readonly string _apiKey;
        private OneCApiClient _apiClient;

        public string Name => "OneС";

        public OneСProvider(string apiKey)
        {
            _apiKey = apiKey;
        }

        public (string message, bool isReady) GetStatus(string apiKey)
        {
            return string.IsNullOrWhiteSpace(apiKey)
                ? ("API key required", false)
                : ("Ready", true);
        }

        public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {

            if (_apiClient == null)
            {
                try
                {
                    _apiClient = new OneCApiClient(_apiKey);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Error connect API {e.Message}");
                }
            }

            string programmingLanguage = "";
            bool createNewSession = false;

            // Получаем или создаем сессию
            string conversationId = await _apiClient.GetOrCreateSession(
                createNew: createNewSession,
                programmingLanguage: string.IsNullOrEmpty(programmingLanguage) ? "" : programmingLanguage
            );

            string question = $"{systemPrompt}\n{userPrompt}";
            
            // Отправляем вопрос
            string answer = await _apiClient.SendMessage(conversationId, question);

            return answer;
        }
    
    
    }
    
    
}
