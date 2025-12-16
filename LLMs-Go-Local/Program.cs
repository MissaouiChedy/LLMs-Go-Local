using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

string modelAlias = "phi-4-mini-reasoning";

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = factory.CreateLogger("Program");

ApiKeyCredential key = new ApiKeyCredential("notneeded");
OpenAIClient client = new OpenAIClient(key, new OpenAIClientOptions
{
    Endpoint = new Uri("http://127.0.0.1:54612/v1"),
});

var agent = client
    .GetChatClient("Phi-4-mini-reasoning-cuda-gpu:3")
    .CreateAIAgent(new ChatClientAgentOptions
    {
        Name = "Agent001",
        ChatOptions = new ChatOptions
        {
            MaxOutputTokens = 25000,
            Temperature = 0.35f,
            Instructions = @"You are a helpful programming language concept explainer.",
            RawRepresentationFactory = _ => new ChatCompletionOptions
            {
#pragma warning disable OPENAI001
                ReasoningEffortLevel = "minimal", //'minimal', 'low', 'medium' (default) or 'high'
#pragma warning restore OPENAI001
            },
        }
    })
    .AsBuilder()
    .Build();

AgentThread thread = agent.GetNewThread();
var streamingResponse = agent.RunStreamingAsync("Explain briefly abstract classes in C#", thread, new AgentRunOptions
{
    AllowBackgroundResponses = true,
});


StringBuilder builder = new StringBuilder();
await foreach (var responseUpdate in streamingResponse)
{
    builder.Append(responseUpdate.Text);
    Console.Write(responseUpdate.Text);
}
Console.Clear();
string output = builder.ToString();

var parts = output.Split("</think>");

var reasoning = parts[0];
var response = parts[1];

Console.WriteLine(response);


Console.WriteLine("====================================");
