# LLMs-Go-Local
In this workshop, we will explore with a hands on approach how to deploy and use local LLMs with C#, the Microsoft Agent Framework and Ollama.

## Pre-Requisites

- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/) or Alternatively [VS Code](https://code.visualstudio.com/) with the [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) 


- [Ollama](https://ollama.com/download)  
    - Verify installation: `ollama --version`
    - `qwen3:1.7b` model in Ollama  
        - Pull the model into Ollama:
            `ollama pull qwen3:1.7b`  
        - Verify the model is available:
            `ollama list` (look for `qwen3:1.7b`)
