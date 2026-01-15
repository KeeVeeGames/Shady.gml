using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static Shady.Parser;

namespace Shady
{
    internal class Program
    {
        static Parser parser = new Parser();
        static Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("[Shady] Wrong number of arguments when calling Shady. Try using \"Shady ProjectDir --pre\" and \"Shady ProjectDir --post\"");
                return;
            }

            string archivePath = "";
            string? archiveName = Environment.GetEnvironmentVariable("YYMACROS_project_cache_directory_name");
            string? archiveDirectory = Environment.GetEnvironmentVariable("YYMACROS_ide_cache_directory");

            if (archiveName != null && archiveDirectory != null)
            {
                archivePath = Path.GetFullPath(archiveName + "\\Shady", archiveDirectory);
            }

            string projectPath = args[0];
            string shadersPath = Path.Join(projectPath, "shaders");

            string[] shaderFiles = Directory
                .EnumerateFiles(shadersPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.ToLower().EndsWith("fsh") || file.ToLower().EndsWith("vsh"))
                .ToArray();

            switch (args[1])
            {
                case "--pre":
                    try
                    {
                        Restore(shaderFiles);

                        foreach (string shaderFile in shaderFiles)
                        {
                            string shaderName = Path.GetFileName(shaderFile);
                            Shader shader = ParseShader(shaderFile);
                            shaders.Add(shaderName, shader);
                        }

                        const bool forceNonParallel = false;
                        var options = new ParallelOptions { MaxDegreeOfParallelism = forceNonParallel ? 1 : 4 };

                        Console.WriteLine($"[Shady] Version: {Assembly.GetExecutingAssembly().GetName().Version}");
                        Console.WriteLine("[Shady] Parse shaders");

                        var shadersPartitioner = Partitioner.Create(shaders);

                        Stopwatch sw = Stopwatch.StartNew();
                        Parallel.ForEach(shadersPartitioner, options, ParseTokens);
                        sw.Stop();

                        Console.WriteLine($"[Shady] Parsing took {sw.ElapsedMilliseconds} ms");

                        Console.WriteLine("[Shady] Backup original shaders");

                        foreach (KeyValuePair<string, Shader> shaderKeyValue in shaders)
                        {
                            Shader shader = shaderKeyValue.Value;

                            if (shader.WillModify)
                            {
                                string currentPath = shader.FileName;
                                string bakPath = $"{Path.GetFullPath(currentPath)}_bak";
                                string modPath = $"{currentPath}_mod";
                                bool integrityCheck = true;

                                if (File.Exists(modPath))
                                {
                                    FileInfo currentInfo = new FileInfo(currentPath);
                                    FileInfo modInfo = new FileInfo(modPath);

                                    if (currentInfo.Length == modInfo.Length)
                                    {
                                        if (shader.Lines.Select(ls => ls.Line).SequenceEqual(File.ReadLines(modPath)))
                                        {
                                            integrityCheck = false;

                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"[Shady] Integrity check for {shader.Name} failed, it seems to be corrupted!");
                                            Console.ForegroundColor = ConsoleColor.White;
                                            Console.WriteLine($"        Try to find backup files in \"{bakPath}\"");
                                            if (!string.IsNullOrEmpty(archivePath))
                                            {
                                                Console.WriteLine($"        Or in \"{archivePath}\"");
                                            }
                                        }
                                    }
                                }

                                if (integrityCheck)
                                {
                                    File.Copy(currentPath, bakPath, true);
                                }
                            }
                        }

                        Console.WriteLine("[Shady] Write modified shaders");

                        WriteShaders(shaders);

                        Console.WriteLine("[Shady] Pre-Build Complete!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Shady] {ex.ToString()}");

                        Console.WriteLine("[Shady] Fatal Error. Trying to restore backed-up shaders");

                        Restore(shaderFiles);

                        Console.WriteLine("[Shady] Restoring complete!");
                    }

                    break;

                case "--post":
                    Console.WriteLine("[Shady] Bring back original shaders");

                    Restore(shaderFiles, archivePath);

                    Console.WriteLine("[Shady] Post-Texture Complete!");

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Shady] DEBUG: In case of debugging shader compiler errors that were found on added lines,");
                    Console.WriteLine($"        you can find modified shader sources in their respective folders inside '/shaders' directory with '_mod' postfix.");
                    Console.ForegroundColor = ConsoleColor.White;

                    break;

                case "--clean":
                    Console.WriteLine("[Shady] Bring back original shaders");

                    Restore(shaderFiles);

                    Console.WriteLine("[Shady] Clean shader cache");

                    Clean(shadersPath);

                    Console.WriteLine("[Shady] Clean Complete!");

                    break;
            }
        }

        private static void Restore(string[] shaderFiles, string archivePath = "")
        {
            foreach (string shaderFile in shaderFiles)
            {
                string backupFile = $"{shaderFile}_bak";

                if (File.Exists(backupFile))
                {
                    if (!string.IsNullOrEmpty(archivePath))
                    {
                        if (!Directory.Exists(archivePath))
                        {
                            Directory.CreateDirectory(archivePath);
                        }

                        string filename = Path.GetFileName(shaderFile);

                        bool needArchive = true;
                        var lastArchive = Directory.EnumerateFiles(archivePath, $"{filename}_*")
                            .OrderByDescending(f => f)
                            .FirstOrDefault();

                        if (lastArchive != null)
                        {
                            FileInfo lastInfo = new FileInfo(lastArchive);
                            FileInfo backupInfo = new FileInfo(backupFile);

                            if (lastInfo.Length == backupInfo.Length && lastInfo.LastWriteTime == backupInfo.LastWriteTime)
                            {
                                needArchive = false;
                            }
                        }

                        if (needArchive)
                        {
                            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                            string archiveFile = Path.GetFullPath($"{archivePath}\\{filename}_{timestamp}");

                            File.Copy(backupFile, archiveFile, false);

                            var archives = Directory.EnumerateFiles(archivePath, $"{filename}_*")
                                .OrderByDescending(f => f)
                                .Skip(5);

                            foreach (var old in archives)
                                File.Delete(old);
                        }
                    }

                    File.Copy(backupFile, shaderFile, true);
                    File.Delete(backupFile);
                }
            }
        }

        private static void Clean(string shadersPath)
        {
            string[] cacheFiles = Directory
                .EnumerateFiles(shadersPath, "*.*", SearchOption.AllDirectories)
                .Where(file => file.ToLower().EndsWith("fsh_mod") || file.ToLower().EndsWith("vsh_mod"))
                .ToArray();

            foreach (string cacheFile in cacheFiles)
            {
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
            }
        }

        private static Shader ParseShader(string path)
        {
            Shader shader = new Shader(path);

            // cache checking
            if (File.Exists($"{path}_mod"))
            {
                IEnumerable<string> modLines = File.ReadLines($"{path}_mod");

                string modLineFirst = modLines.First();
                if (modLineFirst.StartsWith("// Date: "))
                {
                    string modDateString = modLineFirst.Replace("// Date: ", "");
                    DateTime modDate = DateTime.ParseExact(modDateString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                    if (modDate.CompareTo(File.GetLastWriteTime(path)) == 0)
                    {
                        shader.IsCached = true;
                    }
                }
            }

            IEnumerable<string> lines = File.ReadLines(path);
            int i = -1;

            foreach (string line in lines)
            {
                i++;
                shader.AddLine(i, line);
            }

            return shader;
        }

        private static void ParseTokens(KeyValuePair<string, Shader> shaderKeyValue)
        {
            Shader shader = shaderKeyValue.Value;
            int level = 0;
            bool isCommented = false;
            bool isLineDefinitionIgnored = false;
            bool isLineUneededPragma = false;
            bool isLineComment = false;
            bool inMain = false;
            string regionNameFunction = string.Empty;
            LinkedList<string> regionNameMacros = new LinkedList<string>();

            LinkedListNode<ShaderLine>? currentNode = shader.Lines.First;
            while (currentNode != null)
            {
                ShaderLine shaderLine = currentNode.Value;
                string line = Regex.Replace(shaderLine.Line, @"^\s+", "");  /// remove leading whitespaces
                string remainingLine;

                isLineDefinitionIgnored = false;
                isLineUneededPragma = false;
                isLineComment = false;

                // Parse Shady tokens
                Token? pragma = parser.Match(line, TokenType.Shady);
                if (pragma != null)
                {
                    remainingLine = Regex.Replace(pragma.RemainingInput, @"^\s", ""); /// remove leading whitespaces

                    TokenType previousToken = TokenType.Shady;
                    List<TokenType> expectedTokens = new List<TokenType>() {
                        TokenType.Import,
                        TokenType.Inline,
                        TokenType.Variant,
                        TokenType.MacroBegin,
                        TokenType.MacroEnd,
                        TokenType.SkipCompilation
                    };
                    List<Token> lineTokens = new List<Token>();

                    while (expectedTokens.Count != 0)
                    {
                        try
                        {
                            Token token = parser.Expect(remainingLine, expectedTokens, previousToken);

                            remainingLine = Regex.Replace(token.RemainingInput, @"^\s", ""); /// remove leading whitespaces;
                            expectedTokens.Clear();

                            switch (token.TokenType)
                            {
                                case TokenType.Import:
                                case TokenType.Inline:
                                case TokenType.Variant:
                                    expectedTokens.Add(TokenType.OpenParen);
                                    break;

                                case TokenType.MacroBegin:
                                    expectedTokens.Add(TokenType.Name);
                                    break;

                                case TokenType.OpenParen:
                                    if (previousToken != TokenType.Variant)
                                    {
                                        expectedTokens.Add(TokenType.Identifier);
                                    }
                                    else
                                    {
                                        expectedTokens.Add(TokenType.Argument);
                                    }
                                    break;

                                case TokenType.Identifier:
                                    expectedTokens.Add(TokenType.Dot);
                                    expectedTokens.Add(TokenType.CloseParen);
                                    break;

                                case TokenType.Dot:
                                    expectedTokens.Add(TokenType.Identifier);
                                    break;

                                case TokenType.Argument:
                                    expectedTokens.Add(TokenType.Comma);
                                    expectedTokens.Add(TokenType.CloseParen);
                                    break;

                                case TokenType.Comma:
                                    expectedTokens.Add(TokenType.Argument);
                                    break;

                                case TokenType.CloseParen:
                                case TokenType.Name:
                                case TokenType.MacroEnd:
                                case TokenType.SkipCompilation:
                                    expectedTokens.Add(TokenType.Empty);
                                    break;

                                default:

                                    break;
                            }

                            previousToken = token.TokenType;
                            lineTokens.Add(token);
                        }
                        catch (UnexpectedExpression e)
                        {
                            expectedTokens.Clear();
                            lineTokens.Clear();
                            Console.WriteLine($"[Shady] Syntax Error {shaderLine.ShaderName}, line {shaderLine.LineIndex + 1}: {e.Message}");
                        }
                    }

                    //Console.Write($"{index} ");
                    Debug.Write("---- ");
                    lineTokens.ForEach(token => Debug.Write($"[{token.Value}]"));
                    Debug.WriteLine("");

                    if (lineTokens.Count > 0)
                    {
                        switch (lineTokens[0].TokenType)
                        {
                            case TokenType.Import:
                                string shaderExtension = shader.Extension;

                                if (lineTokens[3].TokenType == TokenType.Dot)   // partial import
                                {
                                    if (lineTokens[5].TokenType == TokenType.Dot)   // has shader extension
                                    {
                                        shaderExtension = "." + lineTokens[4].Value;
                                        shaderLine.ImportRegion.RegionName = lineTokens[6].Value;
                                    }
                                    else
                                    {
                                        shaderLine.ImportRegion.RegionName = lineTokens[4].Value;
                                    }
                                }
                                else
                                {
                                    shaderLine.ImportRegion.RegionName = Shader.FullRegion;
                                }

                                shaderLine.ImportRegion.ShaderName = lineTokens[2].Value + shaderExtension;
                                shader.WillModify = true;

                                break;

                            case TokenType.Inline:
                                shaderLine.ImportRegion.ShaderName = lineTokens[2].Value + shader.Extension;

                                if (lineTokens[3].TokenType == TokenType.Dot)
                                {
                                    shaderLine.ImportRegion.RegionName = $"{Shader.MacroRegion}_{lineTokens[4].Value}";
                                }

                                shader.WillModify = true;

                                break;

                            case TokenType.Variant:
                                shader.VariantArguments = lineTokens.Where(token => token.TokenType is TokenType.Identifier or TokenType.Argument)
                                    .Select(token => token.Value)
                                    .ToArray();

                                shader.VariantArguments[0] += shader.Extension;
                                isLineUneededPragma = true;
                                shader.WillModify = true;

                                break;

                            case TokenType.MacroBegin:
                                regionNameMacros.AddLast(lineTokens[1].Value);
                                isLineUneededPragma = true;
                                break;

                            case TokenType.MacroEnd:
                                regionNameMacros.RemoveLast();
                                isLineUneededPragma = true;
                                break;

                            case TokenType.SkipCompilation:
                                shader.IsSkipped = true;
                                isLineUneededPragma = true;
                                shader.WillModify = true;
                                break;
                        }
                    }
                }
                else
                {
                    new Action(() =>
                    {
                        if (!isCommented)
                        {
                            if (string.IsNullOrEmpty(line))
                            {
                                isLineComment = true;
                                return;
                            }

                            // Parse line comment
                            Token? lineComment = parser.Match(line, TokenType.LineComment);
                            if (lineComment != null)
                            {
                                isLineComment = true;
                                Debug.WriteLine(">>>> Line Comment!");
                                return;
                            }

                            // Parse multi-line comment
                            Token? openComment = parser.Match(line, TokenType.OpenComment);
                            if (openComment != null)
                            {
                                Debug.WriteLine(">>>> Open Comment!");

                                Token? closeComment = parser.Match(line, TokenType.CloseComment);
                                if (closeComment != null)
                                {
                                    Debug.WriteLine(">>>> Close Comment!");
                                    isLineComment = true;
                                }
                                else
                                {
                                    isCommented = true;
                                }
                            }

                            if (level != 0) return;

                            // Parse varying
                            Token? varying = parser.Match(line, TokenType.Varying);
                            if (varying != null)
                            {
                                isLineDefinitionIgnored = true;
                                Debug.WriteLine(">>>> Varying!");
                                return;
                            }

                            // Parse uniform
                            Token? uniform = parser.Match(line, TokenType.Uniform);
                            if (uniform != null)
                            {
                                isLineDefinitionIgnored = true;
                                Debug.WriteLine(">>>> Uniform!");
                                return;
                            }

                            // Parse precision
                            Token? precision = parser.Match(line, TokenType.Precision);
                            if (precision != null)
                            {
                                isLineDefinitionIgnored = true;
                                Debug.WriteLine(">>>> Precision!");
                                return;
                            }

                            // Parse main() region
                            Token? main = parser.Match(line, TokenType.Main);
                            if (main != null)
                            {
                                inMain = true;
                                Debug.WriteLine(">>>> Main!");
                                return;
                            }

                            // Parse #define region
                            Token? define = parser.Match(line, TokenType.Define);
                            if (define != null)
                            {
                                Debug.WriteLine(">>>> Define!");

                                remainingLine = define.RemainingInput.TrimStart();

                                try
                                {
                                    Token token = parser.Expect(remainingLine, TokenType.Identifier, TokenType.Define);
                                    shader.AddToRegion(token.Value, shaderLine);
                                }
                                catch (UnexpectedExpression e)
                                {
                                    Console.WriteLine($"[Shady] Syntax Error in {shaderLine.ShaderName}, line {shaderLine.LineIndex + 1}: {e.Message}");
                                }

                                return;
                            }

                            // Parse assignment region
                            Token? assignment = parser.Match(line, TokenType.Assignment);
                            if (assignment != null)
                            {
                                Debug.WriteLine(">>>> Assignment!");

                                remainingLine = assignment.Value;

                                Token? assignmentName = parser.Match(remainingLine, TokenType.Identifier);

                                if (assignmentName != null)
                                {
                                    shader.AddToRegion(assignmentName.Value, shaderLine);
                                }

                                return;
                            }

                            // Parse function region
                            Token? function = parser.Match(line, TokenType.Function);
                            if (function != null)
                            {
                                Debug.WriteLine(">>>> Function!");

                                remainingLine = function.Value;

                                Token? functionName = parser.Match(remainingLine, TokenType.Identifier);

                                if (functionName != null)
                                {
                                    regionNameFunction = functionName.Value;
                                }

                                return;
                            }
                        }
                        else
                        {
                            Token? closeComment = parser.Match(line, TokenType.CloseComment);
                            if (closeComment != null)
                            {
                                Debug.WriteLine(">>>> Close Comment!");
                                isCommented = false;
                                isLineComment = true;
                            }
                        }
                    })();
                }


                if (!isLineComment && !isCommented && !isLineUneededPragma)
                {
                    if (!inMain && !isLineDefinitionIgnored)
                    {
                        shader.AddToRegion(Shader.FullRegion, shaderLine);

                        if (!string.IsNullOrEmpty(regionNameFunction))
                        {
                            shader.AddToRegion(regionNameFunction, shaderLine);
                        }
                    }

                    foreach (string regionNameMacro in regionNameMacros)
                    {
                        shader.AddToRegion($"{Shader.MacroRegion}_{regionNameMacro}", shaderLine);
                    }

                    // Parse close brace
                    Token? closeBrace = parser.Match(line, TokenType.CloseBrace);
                    if (closeBrace != null)
                    {
                        level--;
                        Debug.WriteLine(">>>> Level Down!");

                        if (level == 0)
                        {
                            inMain = false;
                            regionNameFunction = string.Empty;
                        }
                    }

                    // Parse open brace
                    Token? openBrace = parser.Match(line, TokenType.OpenBrace);
                    if (openBrace != null)
                    {
                        level++;
                        Debug.WriteLine(">>>> Level Up!");
                    }
                }

                Debug.WriteLine($"{shaderLine.ShaderName}.{level}: {shaderLine.Line}");
                currentNode = currentNode.Next;
            }

            //foreach (string regionName in shader.GetRegionNames())
            //{
            //    string shaderDirectory = $"C:\\Users\\MusNik\\Desktop\\test\\{shader.Name}";
            //    Directory.CreateDirectory(shaderDirectory);

            //    LinkedList<ShaderLine>? region = shader.GetRegion(regionName);

            //    if (region != null)
            //    {
            //        using (TextWriter textWriter = new StreamWriter($"{shaderDirectory}\\{regionName}.fsh", false, Encoding.UTF8, 65536))
            //        {
            //            foreach (ShaderLine shaderLine in region)
            //            {
            //                textWriter.WriteLine(shaderLine.Line);
            //            }
            //        }
            //    }
            //}
        }

        private static void WriteShaders(Dictionary<string, Shader> shaders)
        {
            foreach (KeyValuePair<string, Shader> shaderKeyValue in shaders)
            {
                Shader shader = shaderKeyValue.Value;
                bool isDirty = !shader.IsCached;

                if (shader.WillModify)
                {
                    HashSet<(string ShaderName, string RegionName)> imported = new HashSet<(string ShaderName, string RegionName)>();
                    imported.Add((shaderKeyValue.Key, Shader.FullRegion));

                    using (TextWriter textWriter = new StringWriter())
                    {
                        // write date of original file into mod file for caching
                        DateTime date = File.GetLastWriteTime(shader.FileName);
                        textWriter.WriteLine($"// Date: {date.ToString("O")}");

                        if (shader.IsSkipped)
                        {
                            textWriter.WriteLine($"// shader skipped by skip_compilation");
                            textWriter.WriteLine("void main() {}");
                        }
                        else
                        {

                            if (shader.VariantArguments == null)
                            {
                                ExpandRegion(shaders, textWriter, shader.Lines, imported, shader.LineOffset, true, ref isDirty);
                            }
                            else
                            {
                                textWriter.WriteLine($"// variant of {shader.VariantArguments[0]}");

                                foreach (string variantArgument in shader.VariantArguments.Skip(1))
                                {
                                    textWriter.WriteLine($"#define {variantArgument}");
                                }

                                textWriter.WriteLine();

                                if (shaders.ContainsKey(shader.VariantArguments[0]))
                                {
                                    Shader variantBaseShader = shaders[shader.VariantArguments[0]];

                                    isDirty = variantBaseShader.IsCached ? isDirty : true;
                                    ExpandRegion(shaders, textWriter, variantBaseShader.Lines, imported, shader.LineOffset, true, ref isDirty);
                                }
                                else
                                {
                                    Console.WriteLine($"[Shady] Variant Error in {
                                        shader.Name
                                    }: Cannot create a variant of '{
                                        shader.VariantArguments[0]
                                    }', shader doesn't exist!");
                                }
                            }
                        }

                        if (isDirty)
                        {
                            File.WriteAllText($"{shader.FileName}_mod", textWriter.ToString());
                        }
                    }

                    File.Copy($"{shader.FileName}_mod", shader.FileName, true);
                }
                else
                {
                    if (!File.Exists($"{shader.FileName}_mod") || !shader.IsCached)
                    {
                        using (TextWriter textWriter = new StreamWriter($"{shader.FileName}_mod", false, Encoding.UTF8, 65536))
                        {
                            // write date of original file into mod file for caching
                            DateTime date = File.GetLastWriteTime(shader.FileName);
                            textWriter.WriteLine($"// Date: {date.ToString("O")}");
                        }
                    }
                }
            }
        }

        private static void ExpandRegion(
            Dictionary<string, Shader> shaders,
            TextWriter textWriter,
            LinkedList<ShaderLine> shaderLines,
            HashSet<(string ShaderName, string RegionName)> imported,
            int lineNumber,
            bool toIncrementLine,
            ref bool isDirty)
        {
            bool toWriteLine = true;

            foreach (ShaderLine shaderLine in shaderLines)
            {
                if (shaderLine.ImportRegion == default)
                {
                    if (toWriteLine)
                    {
                        textWriter.WriteLine($"#line {lineNumber}");

                    }
                    else
                    {
                        //textWriter.WriteLine($"// #line {lineNumber}");
                    }

                    if (toIncrementLine)
                    {
                        lineNumber++;
                        toWriteLine = false;
                    }

                    textWriter.WriteLine(shaderLine.Line);
                }
                else
                {
                    if (!shaderLine.ImportRegion.RegionName.Contains(Shader.MacroRegion))
                    {
                        if (imported.Contains((shaderLine.ImportRegion.ShaderName, Shader.FullRegion)))
                        {
                            continue;
                        }

                        if (imported.Contains(shaderLine.ImportRegion))
                        {
                            continue;
                        }
                    }

                    if (shaders.ContainsKey(shaderLine.ImportRegion.ShaderName))
                    {
                        Shader shader = shaders[shaderLine.ImportRegion.ShaderName];

                        LinkedList<ShaderLine>? region = shader.GetRegion(shaderLine.ImportRegion.RegionName);

                        if (region != null)
                        {
                            imported.Add(shaderLine.ImportRegion);
                            isDirty = shader.IsCached ? isDirty : true;

                            textWriter.WriteLine($"// begin import {shaderLine.ImportRegion.ShaderName}.{shaderLine.ImportRegion.RegionName}");
                            ExpandRegion(shaders, textWriter, region, imported, lineNumber, false, ref isDirty);
                            textWriter.WriteLine($"// end import {shaderLine.ImportRegion.ShaderName}.{shaderLine.ImportRegion.RegionName}");

                            if (toIncrementLine) lineNumber++;
                            toWriteLine = true;
                        }
                        else
                        {
                            Console.WriteLine($"[Shady] Import Error in {
                                shaderLine.ShaderName
                            }, line {
                                shaderLine.LineIndex + 1
                            }: Cannot import '{
                                shaderLine.ImportRegion.RegionName
                            }' from '{
                                shaderLine.ImportRegion.ShaderName
                            }', identifier doesn't exist!");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"[Shady] Import Error in {
                            shaderLine.ShaderName
                        }, line {
                            shaderLine.LineIndex + 1
                        }: Cannot import '{
                            shaderLine.ImportRegion.ShaderName
                        }', shader doesn't exist or has no exported identifiers!");
                    }
                }
            }
        }
    }
}