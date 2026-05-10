# Workshop: LLMs Go Local

A step-by-step guide to building a local multi-agent AI system with C# and Ollama.

---

## Step 1: Install Pre-requisites

### 1.1 IDE

Install one of the following:

- [Visual Studio 2022/2026](https://visualstudio.microsoft.com/downloads/) with the **.NET desktop development** workload  
- [VS Code](https://code.visualstudio.com/) with the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension

### 1.2 .NET SDK

Ensure **.NET 10** is installed:

```bash
dotnet --version   # should print 10.x.x
```

Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download) if needed.

### 1.3 Ollama

1. Download and install [Ollama](https://ollama.com/download).
2. Verify the installation:
   ```bash
   ollama --version
   ```
3. Pull the model used in this workshop:
   ```bash
   ollama pull qwen3:1.7b
   ```
4. Confirm the model is available:
   ```bash
   ollama list   # qwen3:1.7b should appear in the list
   ```
5. Start the Ollama server (it may already be running as a background service):
   ```bash
   ollama serve
   ```

### 1.4 Clone and restore the repository

```bash
git clone https://github.com/your-username/LLMs-Go-Local.git
cd LLMs-Go-Local
dotnet restore
```

---

## Step 2: Build a Basic Main Agent with a Chat Loop

In this step you will create a single AI agent powered by a local Ollama model and interact with it through a simple console chat loop.

### 2.1 Create the console project

The `LLMs-Go-Local` project already exists in the solution. Open `LLMs-Go-Local/LLMs-Go-Local.csproj` and confirm the following NuGet packages are referenced:

```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.5.0" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.5.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.7" />
<PackageReference Include="OllamaSharp" Version="5.4.25" />
```

Run `dotnet restore` after any changes.

### 2.2 Create the agent instructions file

Create `LLMs-Go-Local/MainAgentInstructions.md`. This file contains the system prompt for the main agent:

```markdown
- You are the central coordinator of a multi-agent system.
- You do not solve tasks directly unless explicitly required.
- Your responsibility is to analyze the user request, plan the workflow and fully delegate tasks to specialist agents.
- When presented with composite tasks, distinguish the tasks and assign each task to a specialized agent.
- Each task should be addressed by the most appropriate specialist agent.
```

Add the file to the `.csproj` so it is copied to the output directory:

```xml
<ItemGroup>
  <Content Include="MainAgentInstructions.md">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### 2.3 Write the initial Program.cs

Replace the contents of `LLMs-Go-Local/Program.cs` with the following:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;

// Load agent instructions from the markdown file
string mainAgentInstructions = await File.ReadAllTextAsync("MainAgentInstructions.md");

// Create an Ollama chat client pointing at the local Ollama server
using OllamaApiClient mainChatClient = new(new Uri("http://localhost:11434"), "qwen3:1.7b");

// Create the main AI agent
AIAgent mainAgent = new ChatClientAgent(
    mainChatClient,
    instructions: mainAgentInstructions,
    name: "MainAgent");

// Keep a running history of the conversation
List<ChatMessage> messages = [];

bool continueLoop = true;
while (continueLoop)
{
    // Build a single-agent workflow on every turn
    #pragma warning disable MAAIW001
    Workflow workflow = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(mainAgent)
        .Build();
    #pragma warning restore MAAIW001

    Console.Write("Prompt >> ");
    string prompt = Console.ReadLine() ?? "exit";
    if (prompt == "exit" || string.IsNullOrWhiteSpace(prompt))
    {
        continueLoop = false;
        continue;
    }

    messages.Add(new(ChatRole.User, prompt));
    messages.AddRange(await RunWorkflowAsync(workflow, messages));
    Console.WriteLine();
}

// Run the workflow and stream output to the console
async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
{
    StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    await foreach (WorkflowEvent @event in run.WatchStreamAsync())
    {
        switch (@event)
        {
            case AgentResponseUpdateEvent e:
                Console.Write(e.Update.Text);
                break;
            case WorkflowOutputEvent output:
                Console.WriteLine();
                return output.As<List<ChatMessage>>()!;
        }
    }
    return [];
}
```

### 2.4 Run and test

```bash
dotnet run --project LLMs-Go-Local/LLMs-Go-Local.csproj
```

At the `Prompt >>` prompt, type any question. Type `exit` to quit. Confirm responses stream back from the local model.

---

## Step 3: Add an MCP Server

Model Context Protocol (MCP) lets agents call tools exposed by an external server process. In this step you will build a file-utility MCP server and connect it to the main app.

### 3.1 Create the MCP server project

The `LLMs-Go-Local.MCPServer` project already exists in the solution. Open `LLMs-Go-Local.MCPServer/LLMs-Go-Local.MCPServer.csproj` and confirm these packages are present:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.7" />
<PackageReference Include="ModelContextProtocol" Version="1.3.0" />
```

Also add the `ModelContextProtocol` package to the main `LLMs-Go-Local.csproj`:

```xml
<PackageReference Include="ModelContextProtocol" Version="1.3.0" />
```

### 3.2 Wire up the MCP host in the server's Program.cs

`LLMs-Go-Local.MCPServer/Program.cs` bootstraps the MCP server over stdio:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();   // discovers all [McpServerToolType] classes

var app = builder.Build();
await app.RunAsync();
```

### 3.3 Create a tool class

Create `LLMs-Go-Local.MCPServer/FileUtility.cs`. The `[McpServerToolType]` attribute marks the class as a tool container; `[McpServerTool]` marks individual methods as callable tools:

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace LLMs_Go_Local.MCPServer;

[McpServerToolType]
[Description("Utility to create files and directories in the file system")]
public class FileUtility
{
    [McpServerTool]
    [Description("Create a file with a name and content in the file system")]
    public async Task<string> CreateTextFile(string filename, string content)
    {
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmssffff");
        // Write to a safe output directory: adjust the path as needed
        await File.WriteAllTextAsync(
            @$"C:\W\FileUtils\{timestamp}-{filename.Replace("/", "_")}",
            content);
        return "OK Done";
    }

    [McpServerTool]
    [Description("Create a directory with a name")]
    public string CreateDirectory(string path)
    {
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmssffff");
        Directory.CreateDirectory($"C:\\W\\FileUtils\\{timestamp}-{path}");
        return "OK Done";
    }
}
```

Create the output directory so the tool can write files:

```powershell
New-Item -ItemType Directory -Force -Path C:\W\FileUtils
```

### 3.4 Connect the MCP server to the main app

At the top of `LLMs-Go-Local/Program.cs`, launch the MCP server as a child process and enumerate its tools:

```csharp
using ModelContextProtocol.Client;

await using McpClient fileTools = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Command = "dotnet run --project ../../../../LLMs-Go-Local.MCPServer/LLMs-Go-Local.MCPServer.csproj",
        Name = "MCP Server For File Creation Tools",
    }));

IList<McpClientTool> toolsInFileUtilMcp = await fileTools.ListToolsAsync();
```

The `toolsInFileUtilMcp` collection will be passed to specialist agents that are allowed to call those tools (see Step 4).

### 3.5 Add function-call logging middleware

Add this helper at the bottom of `Program.cs` to log tool calls to the console before they execute:

```csharp
using System.Text;

async ValueTask<object?> FunctionCallMiddleware(
    AIAgent callingAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    StringBuilder details = new();
    details.Append($"Tool Call: '{context.Function.Name}'");
    if (context.Arguments.Count > 0)
        details.Append($" (Args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))})");

    Console.WriteLine(">>>> {0}", details.ToString());
    return await next(context, cancellationToken);
}
```

---

## Step 4: Add an Agent Workflow

In this step you will add two specialist agents, a blog writer and a code sample generator, and connect them to the main agent through a handoff workflow so that tasks are automatically routed to the right agent.

### 4.1 Create specialist agent instruction files

**`LLMs-Go-Local/BlogWriterAgentInstructions.md`**

```markdown
You are a blog post writer.

You only write technical blog posts on computer programming topics.

Blog posts should have an introduction, 3 sections and a conclusion.

Blog posts should be generated in markdown format.

Blog post files generated should be in the format: blog-post-title.md

Blog post files generated should be in relative path containing only the name of the file without directories.

Blog post file path should avoid containing slashes or back-slashes.
```

**`LLMs-Go-Local/CodeSampleAgentInstructions.md`**

```markdown
You are a code sample generator.

You should only generate full project runnable code samples given a programming subject.

When using library dependencies always use the language's default package manager (like NuGet, npm...).

Sample project should be saved under a directory named after the subject.

Sample code files should be generated in a specific programming language.

You should generate a README file in the coding project sample with the following structure:
- Description
- Pre-Requisites
- How to Run the Sample
- Contribution Guide

Blog post file path should avoid containing slashes or back-slashes.
```

Add both files to the `.csproj` so they copy to the output directory:

```xml
<Content Include="BlogWriterAgentInstructions.md">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
<Content Include="CodeSampleAgentInstructions.md">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
```

### 4.2 Create the specialist agents in Program.cs

After the `mainAgent` declaration, add the two specialist agents. Each one receives the MCP tools and the function-call middleware:

```csharp
using System.Text.Json;

// --- Blog Writer Agent ---
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

// --- Code Sample Agent ---
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
```

### 4.3 Build the handoff workflow

Replace the single-agent `Workflow` construction inside the chat loop with a multi-agent handoff workflow:

```csharp
#pragma warning disable MAAIW001
Workflow workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(mainAgent)
    .WithHandoffs(mainAgent, [blogWriterAgent, codeSampleAgent])
    .WithHandoffs([blogWriterAgent, codeSampleAgent], mainAgent)
    .Build();
#pragma warning restore MAAIW001
```

This configures the routing so that:
- `mainAgent` can hand off work to either specialist agent.
- Both specialist agents can hand control back to `mainAgent` when done.

### 4.4 Enhance the streaming output to show agent names and tool calls

Update `RunWorkflowAsync` to identify which agent is speaking and surface function call events:

```csharp
async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
{
    string? lastExecutorId = null;

    StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
    await foreach (WorkflowEvent @event in run.WatchStreamAsync())
    {
        switch (@event)
        {
            case AgentResponseUpdateEvent e:
            {
                if (e.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = e.ExecutorId;
                    Console.WriteLine();
                    Console.WriteLine($">>>>>> {e.Update.AuthorName ?? e.ExecutorId}");
                }

                Console.Write(e.Update.Text);

                if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault()
                        is FunctionCallContent call)
                {
                    Console.WriteLine();
                    Console.WriteLine($">>>>>> Call '{call.Name}' with arguments: {JsonSerializer.Serialize(call.Arguments)}]");
                }
                break;
            }
            case WorkflowOutputEvent output:
                Console.WriteLine();
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~");
                return output.As<List<ChatMessage>>()!;
        }
    }
    return [];
}
```

### 4.5 Run the full multi-agent system

```bash
dotnet run --project LLMs-Go-Local/LLMs-Go-Local.csproj
```

Try prompts that exercise both specialist agents, for example:

- `Write a blog post about async/await in C#`: routes to **BlogWriterAgent**, which creates a `.md` file via the MCP tool.
- `Generate a C# code sample for a minimal Web API`: routes to **CodeSampleAgent**, which creates the project files via the MCP tool.
- A combined prompt: `mainAgent` will split the work and delegate to both agents in sequence.

Watch the console for `>>>>>>` lines that show which agent is active and `>>>>` lines that show MCP tool calls being made.
