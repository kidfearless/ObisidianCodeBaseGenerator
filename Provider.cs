using Anthropic;

using Microsoft.Extensions.AI;

using System.Runtime.CompilerServices;
using System.Text;

namespace ObisidianCodeBaseGenerator;

public abstract record Provider
{
    public static readonly List<string> Keys = [
        "OpenAI",
        "Anthropic",
        "Gemini"
    ];

    protected static readonly Dictionary<string, string> Urls = new() {
        { "OpenAI", "https://api.openai.com/v1/chat/" },
        { "Anthropic", "https://api.anthropic.com/v1/messages" },
        { "Gemini", "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-001:generateContent" }
    };

    protected abstract string Key { get; }
    protected virtual string Url => Urls[Key];

    protected string? ApiKey;
    protected bool IsInitialized;

    public ChatModel? ChatModel { get; set; }
    public List<Message> Messages { get; set; } = [];

    public abstract Task<List<ChatModel>> GetModelsAsync();
#pragma warning disable CS8424
    public abstract IAsyncEnumerable<Response> StreamResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default);
#pragma warning restore CS8424

    public virtual void Initialize(string apiKey)
    {
        ApiKey = apiKey;
        if (ApiKey is null or "")
        {
            throw new Exception("API key not set");
        }

        IsInitialized = true;
    }

    public static Provider GetProvider(string key, string apiKey)
    {
        Provider provider = key switch
        {
            "OpenAI" => new OpenAiProvider(),
            "Anthropic" => new AnthropicProvider(),
            "Gemini" => new GeminiProvider(),
            _ => throw new Exception("Invalid provider")
        };

        provider.Initialize(apiKey);
        return provider;
    }

    public record OpenAiProvider : Provider
    {
        protected override string Key => "OpenAI";
        protected virtual OpenAI.OpenAIClientOptions Options => new() { Endpoint = new(this.Url)};
        public override async Task<List<ChatModel>> GetModelsAsync()
        {
            if (!IsInitialized)
            {
                throw new Exception("Provider not initialized");
            }

            OpenAI.Models.OpenAIModelClient client = new OpenAI.Models.OpenAIModelClient(new(ApiKey!), Options);
            var models = await client.GetModelsAsync();
            return models.Value.Select(m => new ChatModel(m.Id)).ToList();
        }

        public override async IAsyncEnumerable<Response> StreamResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                throw new Exception("Provider not initialized");
            }

            if (ChatModel is null)
            {
                throw new Exception("Chat model not set");
            }

            if (Messages is null || !Messages.Any())
            {
                throw new Exception("No messages to send");
            }

            OpenAI.Chat.ChatClient client = new(ChatModel.Model, new(ApiKey), Options);
            var messages = Messages.Select<Message, OpenAI.Chat.ChatMessage>(m => m switch
            {
                UserMessage => new OpenAI.Chat.UserChatMessage(m.Content),
                AssistantMessage => new OpenAI.Chat.AssistantChatMessage(m.Content),
                SystemPromptMessage => new OpenAI.Chat.SystemChatMessage(m.Content),
                _ => throw new Exception($"Invalid message type {m.GetType().Name} {m.Content}")
            }).ToList();

            var response = client.CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken);
            var builder = new StringBuilder(2048);
            await foreach (var res in response)
            {
                var text = string.Join("", res.ContentUpdate.Select(t => t.Text));
                builder.Append(text);
                yield return new Response(text);
            }

            Messages.Add(new AssistantMessage(builder.ToString()));
        }
    }

    // uses the openai compatibility layer of gemini
    public record GeminiProvider : OpenAiProvider
    {
        protected override string Key => "Gemini";
        protected override string Url => "https://generativelanguage.googleapis.com/v1beta/openai/";
    }

    public record class AnthropicProvider : Provider
    {
        protected override string Key => "Anthropic";
        protected override string Url => "";

        public override async Task<List<ChatModel>> GetModelsAsync()
        {
            if (!IsInitialized || ApiKey is null)
            {
                throw new Exception("Provider not initialized");
            }

            AnthropicClient client = new(ApiKey);
            var models = await client.ModelsListAsync();
            return models.Data.Select(m => new ChatModel(m.Id)).ToList();
        }

        public override async IAsyncEnumerable<Response> StreamResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsInitialized || ApiKey is null)
            {
                throw new Exception("Provider not initialized");
            }

            if (ChatModel is null)
            {
                throw new Exception("Chat model not set");
            }

            if (Messages is null || !Messages.Any())
            {
                throw new Exception("No messages to send");
            }

            AnthropicClient client = new(ApiKey);
            var res = client.CompleteStreamingAsync(ChatModel.Model, cancellationToken: cancellationToken);
            var builder = new StringBuilder(2048);
            await foreach (var r in res)
            {
                var text = r?.Text;
                if (text is not null)
                {
                    builder.Append(text);
                    yield return new Response(text);
                }
            }

            Messages.Add(new AssistantMessage(builder.ToString()));
        }
    }
}
