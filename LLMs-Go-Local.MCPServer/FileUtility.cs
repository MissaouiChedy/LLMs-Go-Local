using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace LLMs_Go_Local.MCPServer
{
    [McpServerToolType]
    [Description("Utility to create files and Directories in the file system")]
    public class FileUtility
    {
        [McpServerTool]
        [Description("Create a file with a name and content in the file system")]
        public async Task<string> CreateTextFile(string filename, string content)
        {
            string timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmssffff");
            await File.WriteAllTextAsync(@$"C:\W\FileUtils\{timestamp}-{filename.Replace("/", "_")}", content);
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
}
