using OllamaSharp;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

await using McpClient fileTools = await McpClient
                .CreateAsync(new StdioClientTransport(new StdioClientTransportOptions()
                {
                    Command = "dotnet run --project ../../../../LLMs-Go-Local.MCPServer/LLMs-Go-Local.MCPServer.csproj",
                    Name = "MCP Server For File Creation Tools",
                }));

IList<McpClientTool> toolsInFileUtilMcp = await fileTools.ListToolsAsync();

string blogWriterInstructions = await File.ReadAllTextAsync("BlogWriterAgentInstructions.md");

using OllamaApiClient blogWriterChatClient = new(new Uri("http://localhost:11434"), "qwen3:1.7b");

AIAgent blogWriterAgent = new ChatClientAgent(
    blogWriterChatClient,
    instructions: blogWriterInstructions,
    name: "BlogWriterAgent",
    tools: toolsInFileUtilMcp.Cast<AITool>().ToList())
    .AsBuilder()
    .Use(FunctionCallMiddleware)
    .Build();

bool continueLoop = true;
while (continueLoop)
{
    AgentThread thread = blogWriterAgent.GetNewThread();
    Console.Write("Prompt >> ");
    string prompt = Console.ReadLine() ?? "exit";
    if (prompt == "exit" || string.IsNullOrWhiteSpace(prompt))
    {
        continueLoop = false;
        continue;
    }
    //"Provide a blog post about abstract classes in C# and save the blog post into a file named post.md"
    var streamingResponse = blogWriterAgent.RunStreamingAsync(prompt, thread);
    Console.WriteLine();
    await foreach (var responseUpdate in streamingResponse)
    {
        Console.Write(responseUpdate.Text);
    }
    Console.WriteLine();
}

async ValueTask<object?> FunctionCallMiddleware(AIAgent callingAgent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)
{
    StringBuilder functionCallDetails = new();
    functionCallDetails.Append($"Tool Call: '{context.Function.Name}'");
    if (context.Arguments.Count > 0)
    {
        functionCallDetails.Append($" (Args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))}");
    }

    Console.WriteLine(">>>> {0}", functionCallDetails.ToString());

    return await next(context, cancellationToken);
}
