using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static GitCommands.Git.Extended.MoveCommand;
using GitExtensions.AITools.LlmProviders.OneCModels;

namespace GitExtensions.AITools.LlmProviders
{
    internal sealed class OneCProvider : ILlmProvider
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        
        private readonly string _apiKey;
        private string _workDirGit;
        private OneCApiClient _apiClient;

        public string Name => "OneС";

        public OneCProvider(string apiKey)
        {
            _apiKey = apiKey;
        }

        public (string message, bool isReady) GetStatus(string apiKey)
        {
            return string.IsNullOrWhiteSpace(apiKey)
                ? ("API key required", false)
                : ("Ready", true);
        }

        public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, string workDirGit, CancellationToken cancellationToken)
        {
            _workDirGit = workDirGit;

            if (_apiClient == null)
            {
                try
                {
                    _apiClient = new OneCApiClient(_apiKey, workDirGit);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Error connect API {e.Message}");
                }
            }

            string programmingLanguage = "";
            bool createNewSession = false;

            // Получаем или создаем сессию
            ConversationSession conversation = await _apiClient.GetOrCreateSession(
                createNew: createNewSession,
                programmingLanguage: string.IsNullOrEmpty(programmingLanguage) ? "" : programmingLanguage
            );

            string question = "";
            if ( conversation.MessagesCount > 0 )
                question = $"{userPrompt}";
            else
                question = $"{systemPrompt}\n{userPrompt}";

            // Отправляем вопрос
            string answer = await _apiClient.SendMessage(conversation.ConversationId, question);

            return answer;
        }
    
    
    }
    
    
}
