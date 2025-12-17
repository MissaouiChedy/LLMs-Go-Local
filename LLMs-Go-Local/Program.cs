using OpenAI;
using System.Text;
using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

await using McpClient fileTools = await McpClient
                .CreateAsync(new StdioClientTransport( new StdioClientTransportOptions ()
                {
                    Command = "dotnet run --project ../../../../LLMs-Go-Local.MCPServer/LLMs-Go-Local.MCPServer.csproj",
                    Name = "MCP Server For File Creation Tools",
                }));

IList<McpClientTool> toolsInFileUtilMcp = await fileTools.ListToolsAsync();

string modelId = "qwen2.5-1.5b-instruct-cuda-gpu:4";

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = factory.CreateLogger("Program");

ApiKeyCredential key = new ApiKeyCredential("KEY");
OpenAIClient client = new OpenAIClient(key, new OpenAIClientOptions
{
    Endpoint = new Uri("http://127.0.0.1:58897/v1"),
});

string instructions = await File.ReadAllTextAsync("AgentInstructions.md");

var agent = client
    .GetChatClient(modelId)
    .CreateAIAgent(new ChatClientAgentOptions
    {
        Name = "Agent001",
        ChatOptions = new ChatOptions
        {
            MaxOutputTokens = 25000,
            Temperature = 0.35f,
            Instructions = instructions,
            Tools = toolsInFileUtilMcp.Cast<AITool>().ToList()
//            RawRepresentationFactory = _ => new ChatCompletionOptions
//            {
//#pragma warning disable OPENAI001
//                ReasoningEffortLevel = ChatReasoningEffortLevel.Minimal, //'minimal', 'low', 'medium' (default) or 'high'
//#pragma warning restore OPENAI001
//            },
        }
    })
    .AsBuilder()
    .Use(FunctionCallMiddleware)
    .Build();


var result = await agent.RunAsync("Provide a blog post about abstract classes in C# and save the blog post into a file named post.md");

Console.WriteLine(result.Text);

//AgentThread thread = agent.GetNewThread();
//var streamingResponse = agent.RunStreamingAsync("Provide a blog post about abstract classes in C# and save the blog post into a file named post.md", thread, new AgentRunOptions
//{
//    AllowBackgroundResponses = true,

//});


//await foreach (var responseUpdate in streamingResponse)
//{
//    Console.Write(responseUpdate.Text);
//}

Console.WriteLine();

Console.WriteLine("====================================");

async ValueTask<object?> FunctionCallMiddleware(AIAgent callingAgent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
{
    StringBuilder functionCallDetails = new();
    functionCallDetails.Append($"Tool Call: '{context.Function.Name}'");
    if (context.Arguments.Count > 0)
    {
        functionCallDetails.Append($" (Args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))}");
    }

    logger.LogInformation(">> {0}", functionCallDetails.ToString());

    return await next(context, cancellationToken);
}
