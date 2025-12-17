using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace LLMs_Go_Local.MCPServer
{
    [McpServerToolType]
    [Description("Utility to create files in the file system")]
    public class FileUtility
    {
        [McpServerTool]
        [Description("Create a file with a name and content in the file system")]
        public async Task<string> CreateTextFile(string filename, string content)
        {
            await File.WriteAllTextAsync(@$"C:\W\FileUtils\{filename}.md", content);
            return "OK Done";
        }
    }
}
