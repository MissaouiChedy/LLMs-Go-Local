using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using System.Text;
using System.Text.Json;

await using McpClient fileTools = await McpClient
                .CreateAsync(new StdioClientTransport(new StdioClientTransportOptions()
                {
                    Command = "dotnet run --project ../../../../LLMs-Go-Local.MCPServer/LLMs-Go-Local.MCPServer.csproj",
                    Name = "MCP Server For File Creation Tools",
                }));

IList<McpClientTool> toolsInFileUtilMcp = await fileTools.ListToolsAsync();

string mainAgentInstructions = await File.ReadAllTextAsync("MainAgentInstructions.md");

using OllamaApiClient mainChatClient = new(new Uri("http://localhost:11434"), "qwen3:1.7b");

AIAgent mainAgent = new ChatClientAgent(
    mainChatClient,
    instructions: mainAgentInstructions,
    name: "MainAgent");

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

string codeSampleInstructions = await File.ReadAllTextAsync("CodeSampleAgentInstructions.md");

using OllamaApiClient codeSampleChatClient = new(new Uri("http://localhost:11434"), "qwen3:1.7b");

AIAgent codeSampleAgent = new ChatClientAgent(
    codeSampleChatClient,
    instructions: codeSampleInstructions,
    name: "CodeSampleAgent",
    tools: toolsInFileUtilMcp.Cast<AITool>().ToList())
    .AsBuilder()
    .Use(FunctionCallMiddleware)
    .Build();


List<ChatMessage> messages = [];

bool continueLoop = true;
while (continueLoop)
{
    Workflow workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(mainAgent)
                .WithHandoffs(mainAgent, [blogWriterAgent, codeSampleAgent])
                .WithHandoffs([blogWriterAgent, codeSampleAgent], mainAgent)
                .Build();
    Console.Write("Prompt >> ");
    string prompt = Console.ReadLine() ?? "exit";
    if (prompt == "exit" || string.IsNullOrWhiteSpace(prompt))
    {
        continueLoop = false;
        continue;
    }

    
    messages.Add(new(Microsoft.Extensions.AI.ChatRole.User, prompt));
    messages.AddRange(await RunWorkflowAsync(workflow, messages));
    
    Console.WriteLine();
}

async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
{
    string? lastExecutorId = null;

    StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    await foreach (WorkflowEvent @event in run.WatchStreamAsync())
    {
        switch (@event)
        {
            case AgentRunUpdateEvent e:
                {
                    if (e.ExecutorId != lastExecutorId)
                    {
                        lastExecutorId = e.ExecutorId;
                        Console.WriteLine();
                        Console.WriteLine($">>>>>> {e.Update.AuthorName ?? e.ExecutorId}");
                    }

                    Console.Write(e.Update.Text);
                    if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                    {
                        Console.WriteLine();
                        Console.WriteLine($">>>>>> Call '{call.Name}' with arguments: {JsonSerializer.Serialize(call.Arguments)}]");
                    }

                    break;
                }
            case WorkflowOutputEvent output:
                Console.WriteLine();
                Console.WriteLine("-------------------------");
                return output.As<List<ChatMessage>>()!;
        }
    }

    return [];
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
