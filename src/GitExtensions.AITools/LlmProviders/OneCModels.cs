using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GitExtensions.AITools.LlmProviders.OneCModels
{
    /// <summary>
    /// Запрос на создание новой дискуссии.
    /// </summary>
    public class ConversationRequest
    {
        [JsonPropertyName("skill_name")]
        public string SkillName { get; set; } = "custom";

        [JsonPropertyName("is_chat")]
        public bool IsChat { get; set; } = true;

        [JsonPropertyName("ui_language")]
        public string UiLanguage { get; set; } = "russian";

        [JsonPropertyName("programming_language")]
        public string ProgrammingLanguage { get; set; } = "";

        [JsonPropertyName("script_language")]
        public string ScriptLanguage { get; set; } = "";
    }

    /// <summary>
    /// Ответ при создании дискуссии.
    /// </summary>
    public class ConversationResponse
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
    }

    /// <summary>
    /// Inner content structure for message.
    /// </summary>
    public class MessageContentInner
    {
        [JsonPropertyName("instruction")]
        public string Instruction { get; set; }
    }

    /// <summary>
    /// Outer content structure for message.
    /// </summary>
    public class MessageContentOuter
    {
        [JsonPropertyName("content")]
        public MessageContentInner Content { get; set; }

        [JsonPropertyName("tools")]
        public List<object> Tools { get; set; } = new List<object>();
    }

    /// <summary>
    /// Request to send a message.
    /// </summary>
    public class MessageRequest
    {
        [JsonPropertyName("content")]
        public MessageContentOuter Content { get; set; }

        [JsonPropertyName("parent_uuid")]
        public required string ParentUuid { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        /// <summary>
        /// Create MessageRequest from instruction string.
        /// </summary>
        public static MessageRequest FromInstruction(string instruction, string parentUuid = null)
        {
            return new MessageRequest
            {
                Content = new MessageContentOuter
                {
                    Content = new MessageContentInner
                    {
                        Instruction = instruction
                    },
                    Tools = new List<object>()
                },
                ParentUuid = parentUuid,
                Role = "user"
            };
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

    /// <summary>
    /// Chunk of message from SSE stream.
    /// </summary>
    public class MessageChunk
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public Dictionary<string, object> Content { get; set; }

        [JsonPropertyName("content_delta")]
        public ContentDelta ContentDelta { get; set; }

        [JsonPropertyName("parent_uuid")]
        public string ParentUuid { get; set; }

        [JsonPropertyName("finished")]
        public bool Finished { get; set; } = false;

        [JsonPropertyName("render_info")]
        public object RenderInfo { get; set; }
    }

    /// <summary>
    /// Сессия дискуссии.
    /// </summary>
    public class ConversationSession
    {
        private int _messagesCount = 0;

        [JsonPropertyName("conversation_id")]
        public string ConversationId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("last_used")]
        public DateTime LastUsed { get; set; } = DateTime.Now;

        [JsonPropertyName("last_message_uuid")]
        public string LastMessageUuid { get; set; }

        [JsonPropertyName("messages_count")]
        public int MessagesCount
        {
            get => _messagesCount;
            set => _messagesCount = value;
        }

        /// <summary>
        /// Обновить время последнего использования.
        /// </summary>
        public void UpdateUsage()
        {
            LastUsed = DateTime.Now;
            _messagesCount++;
        }
    }

    /// <summary>
    /// Ошибка API 1С.ai.
    /// </summary>
    public class ApiError : Exception
    {
        public int? StatusCode { get; set; }

        public ApiError(string message) : base(message)
        {
        }

        public ApiError(string message, int? statusCode = null) : base(message)
        {
            StatusCode = statusCode;
        }

        public ApiError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

 }
