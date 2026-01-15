# LLMs-Go-Local

## Description

What if you could run Large Language Models directly on your local machine, with no cloud, no network latency, and no subscriptions?

In this workshop, we will explore with a hands-on approach how to deploy and use local LLMs with C# and the Microsoft Agent Framework.

Discover the benefits of running GenAI at the Edge:
- 🍫 **Bring intelligence closer to your data** - Process sensitive information without sending it to external servers
- 🍰 **Build, test, and iterate offline** - Develop AI-powered applications without internet dependency
- 🍪 **Keep your models private and in control** - Full ownership of your AI infrastructure

## Pre-Requisites

- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) or Alternatively [VS Code](https://code.visualstudio.com/) with the [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) 

- [Ollama](https://ollama.com/download)  
    - Verify installation: `ollama --version`
    - `qwen3:1.7b` model in Ollama  
        - Pull the model into Ollama:
            `ollama pull qwen3:1.7b`  
        - Verify the model is available:
            `ollama list` (look for `qwen3:1.7b`)

## Setup Overview

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-username/LLMs-Go-Local.git
   cd LLMs-Go-Local
   ```

2. **Ensure Ollama is running**
   ```bash
   ollama serve
   ```

3. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

4. **Build the solution**
   ```bash
   dotnet build
   ```

## Console App Overview

The main console application (`LLMs-Go-Local/`) demonstrates how to create AI agents using the Microsoft Agent Framework with local LLMs powered by Ollama.

**Key Components:**
- **Program.cs** - Entry point and agent orchestration
- **MainAgentInstructions.md** - Instructions for the main coordinating agent
- **BlogWriterAgentInstructions.md** - Instructions for the blog writing agent
- **CodeSampleAgentInstructions.md** - Instructions for the code sample generation agent

The application showcases a multi-agent architecture where specialized agents collaborate to accomplish tasks.

## MCP Server Overview

The MCP (Model Context Protocol) Server (`LLMs-Go-Local.MCPServer/`) provides file system utilities that agents can use to interact with local files.

**Key Components:**
- **Program.cs** - MCP Server entry point and configuration
- **FileUtility.cs** - File operations exposed to agents (read, write, list files)

The MCP Server enables agents to perform file-based operations, extending their capabilities beyond conversation.

## How to Use?

1. **Start the MCP Server** (in a separate terminal)
   ```bash
   cd LLMs-Go-Local.MCPServer
   dotnet run
   ```

2. **Run the Console Application**
   ```bash
   cd LLMs-Go-Local
   dotnet run
   ```

3. **Interact with the agents** through the console interface. The main agent will coordinate with specialized agents to help you with:
   - Writing blog posts
   - Generating code samples
   - File operations via MCP

## Notes

- **Model Performance**: The `qwen3:1.7b` model is optimized for local execution with moderate hardware. For better results, consider larger models if your hardware supports them.
- **Memory Usage**: Local LLMs require significant RAM. Ensure you have at least 8GB available.
- **First Run**: Initial model loading may take longer as Ollama caches the model.
- **Offline Capable**: Once the model is downloaded, no internet connection is required.

## Contributing

Your contributions are welcome ! Please checkout [the contribution guidelines](CONTRIBUTING.md)

## References

Microsoft Agent Framework code samples available in the [rwjdk/MicrosoftAgentFrameworkSamples](https://github.com/rwjdk/MicrosoftAgentFrameworkSamples) were very useful in preparing this sample. Thank you [@rwjdk](https://github.com/rwjdk)!