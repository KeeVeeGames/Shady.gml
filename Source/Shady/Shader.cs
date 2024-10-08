namespace Shady
{
    internal class Shader
    {
        public const string FullRegion = "__shady_export";
        public const string MacroRegion = "__shady_macro";

        public string Name { get; }
        public string Extension { get; }
        public string FileName { get; }
        public LinkedList<ShaderLine> Lines { get; }
        public string[]? VariantArguments;
        public bool WillModify { get; set; }

        private readonly Dictionary<string, LinkedList<ShaderLine>> _regions;

        public Shader(string fileName)
        {
            Name = Path.GetFileName(fileName);
            Extension = Path.GetExtension(fileName);
            FileName = fileName;
            Lines = new LinkedList<ShaderLine>();
            WillModify = false;

            _regions = new Dictionary<string, LinkedList<ShaderLine>>();
        }

        public void AddLine(int lineIndex, string line)
        {
            Lines.AddLast(new ShaderLine(Name, lineIndex, line));
        }

        public void AddToRegion(string regionName, ShaderLine shaderLine)
        {
            LinkedList<ShaderLine> region;

            if (!_regions.ContainsKey(regionName))
            {
                region = new LinkedList<ShaderLine>();
                _regions.Add(regionName, region);
            } else
            {
                region = _regions[regionName];
            }

            region.AddLast(shaderLine);
        }

        public LinkedList<ShaderLine>? GetRegion(string regionName)
        {
            if (_regions.ContainsKey(regionName))
            {
                return _regions[regionName];
            }
            else
            {
                return null;
            }
        }

        public string[] GetRegionNames() { return _regions.Keys.ToArray(); }

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
        public (string ShaderName, string RegionName) ImportRegion;

        public ShaderLine(string shaderName, int lineIndex, string line)
        {
            LineIndex = lineIndex;
            Line = line;
            ShaderName = shaderName;
        }
    }
}
