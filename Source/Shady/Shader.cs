using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Shady
{
    internal class Shader
    {
        public string Name { get; }
        public LinkedList<ShaderLine> Lines { get; }
        private readonly Dictionary<string, LinkedListNode<string>> _exports;
        private readonly Dictionary<string, LinkedListNode<string>> _macros;
        public Shader(string name)
        {
            Name = name;
            Lines = new LinkedList<ShaderLine>();
        }

        public void AddLine(int lineIndex, string line)
        {
            Lines.AddLast(new ShaderLine(Name, lineIndex, line));
        }

        public void DebugConsole()
        {
            foreach (var line in Lines)
            {
                Console.WriteLine(line);
            }
        }
    }

    internal class ShaderLine
    {
        public int LineIndex { get; }
        public string Line { get; }
        public string ShaderName { get; }
        public ShaderLine(string shaderName, int lineIndex, string line)
        {
            LineIndex = lineIndex;
            Line = line;
            ShaderName = shaderName;
        }
    }
}
