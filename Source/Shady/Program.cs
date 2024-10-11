using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Intrinsics.X86;
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
                Console.WriteLine("[Shady] Wrong number of arguments when calling Shady. Try using \"Shady PrjectDir --pre\" and \"Shady ProjectDir --post\"");
                return;
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

                        Console.WriteLine("[Shady] Parse shaders");

                        Parallel.ForEach(shaders, options, ParseTokens);

                        Console.WriteLine("[Shady] Backup original shaders");

                        foreach (KeyValuePair<string, Shader> shaderKeyValue in shaders)
                        {
                            Shader shader = shaderKeyValue.Value;

                            if (shader.WillModify)
                            {
                                File.Copy(shader.FileName, $"{shader.FileName}_bak", true);
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

                    Restore(shaderFiles);

                    Console.WriteLine("[Shady] Post-Texture Complete!");

                    break;
            }
        }

        private static void Restore(string[] shaderFiles)
        {
            foreach (string shaderFile in shaderFiles)
            {
                string backupFile = $"{shaderFile}_bak";
                if (File.Exists(backupFile))
                {
                    File.Move(backupFile, shaderFile, true);
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
                        shader.IsCahced = true;
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
            bool isLineIgnored = false;
            bool inMain = false;
            string regionNameFunction = string.Empty;
            LinkedList<string> regionNameMacros = new LinkedList<string>();

            LinkedListNode<ShaderLine>? currentNode = shader.Lines.First;
            while (currentNode != null)
            {
                ShaderLine shaderLine = currentNode.Value;
                string line = Regex.Replace(shaderLine.Line, @"^\s+", "");  /// remove leading whitespaces
                string remainingLine;

                isLineIgnored = false;

                // Parse Shady tokens
                Token? pragma = parser.Match(line, TokenType.Shady);
                if (pragma != null)
                {
                    remainingLine = Regex.Replace(pragma.RemainingInput, @"^\s", ""); /// remove leading whitespaces

                    TokenType previousToken = TokenType.Shady;
                    List<TokenType> expectedTokens = new List<TokenType>() { TokenType.Import, TokenType.Inline, TokenType.Variant, TokenType.MacroBegin, TokenType.MacroEnd };
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
                    lineTokens.ForEach(token => Debug.Write($"[{token.Value}]"));
                    Debug.WriteLine("");

                    if (lineTokens.Count > 0)
                    {
                        switch (lineTokens[0].TokenType)
                        {
                            case TokenType.Import:
                                shaderLine.ImportRegion.ShaderName = lineTokens[2].Value + shader.Extension;

                                if (lineTokens[3].TokenType == TokenType.Dot)
                                {
                                    shaderLine.ImportRegion.RegionName = lineTokens[4].Value;
                                }
                                else
                                {
                                    shaderLine.ImportRegion.RegionName = Shader.FullRegion;
                                }

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
                                isLineIgnored = true;
                                shader.WillModify = true;

                                break;

                            case TokenType.MacroBegin:
                                regionNameMacros.AddLast(lineTokens[1].Value);
                                isLineIgnored = true;
                                break;

                            case TokenType.MacroEnd:
                                regionNameMacros.RemoveLast();
                                isLineIgnored = true;
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
                                isLineIgnored = true;
                                return;
                            }

                            // Parse line comment
                            Token? lineComment = parser.Match(line, TokenType.LineComment);
                            if (lineComment != null)
                            {
                                isLineIgnored = true;
                                Debug.WriteLine("!!!! Line Comment!");
                                return;
                            }

                            // Parse multi-line comment
                            Token? openComment = parser.Match(line, TokenType.OpenComment);
                            if (openComment != null)
                            {
                                Debug.WriteLine("!!!! Open Comment!");

                                Token? closeComment = parser.Match(line, TokenType.CloseComment);
                                if (closeComment != null)
                                {
                                    Debug.WriteLine("!!!! Close Comment!");
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
                                isLineIgnored = true;
                                Debug.WriteLine("!!!! Varying!");
                                return;
                            }

                            // Parse uniform
                            Token? uniform = parser.Match(line, TokenType.Uniform);
                            if (uniform != null)
                            {
                                isLineIgnored = true;
                                Debug.WriteLine("!!!! Uniform!");
                                return;
                            }

                            // Parse precision
                            Token? precision = parser.Match(line, TokenType.Precision);
                            if (precision != null)
                            {
                                isLineIgnored = true;
                                Debug.WriteLine("!!!! Precision!");
                                return;
                            }

                            // Parse main() region
                            Token? main = parser.Match(line, TokenType.Main);
                            if (main != null)
                            {
                                inMain = true;
                                Debug.WriteLine("!!!! Main!");
                                return;
                            }

                            // Parse #define region
                            Token? define = parser.Match(line, TokenType.Define);
                            if (define != null)
                            {
                                Debug.WriteLine("!!!! Define!");

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
                                Debug.WriteLine("!!!! Assignment!");

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
                                Debug.WriteLine("!!!! Function!");

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
                                Debug.WriteLine("!!!! Close Comment!");
                                isCommented = false;
                            }
                        }
                    })();
                }

                if (!isCommented && !isLineIgnored)
                {
                    if (!inMain)
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
                }

                // Parse open brace
                Token? openBrace = parser.Match(line, TokenType.OpenBrace);
                if (openBrace != null)
                {
                    level++;
                }

                // Parse close brace
                Token? closeBrace = parser.Match(line, TokenType.CloseBrace);
                if (closeBrace != null)
                {
                    level--;

                    if (level == 0)
                    {
                        inMain = false;
                        regionNameFunction = string.Empty;
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
                bool isDirty = false;

                //if (!shader.IsCahced)
                //{
                if (shader.WillModify)
                {
                    HashSet<(string ShaderName, string RegionName)> imported = new HashSet<(string ShaderName, string RegionName)>();
                    imported.Add((shaderKeyValue.Key, Shader.FullRegion));

                    //using (TextWriter textWriter = new StreamWriter($"{shader.FileName}_mod", false, Encoding.UTF8, 65536))
                    using (TextWriter textWriter = new StringWriter())
                    {
                        // write date of original file into mod file for caching
                        DateTime date = File.GetLastWriteTime(shader.FileName);
                        textWriter.WriteLine($"// Date: {date.ToString("O")}");

                        if (shader.VariantArguments == null)
                        {
                            isDirty = shader.IsCahced ? isDirty : true;
                            ExpandRegion(shaders, textWriter, shader.Lines, imported, ref isDirty);
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

                                isDirty = variantBaseShader.IsCahced ? isDirty : true;
                                ExpandRegion(shaders, textWriter, variantBaseShader.Lines, imported, ref isDirty);
                            }
                            else
                            {
                                Console.WriteLine($"[Shady] Variant Error in {shader.Name}: Cannot create a variant of '{shader.VariantArguments[0]}', shader doesn't exist!");
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
                    if (!File.Exists($"{shader.FileName}_mod") || !shader.IsCahced)
                    {
                        using (TextWriter textWriter = new StreamWriter($"{shader.FileName}_mod", false, Encoding.UTF8, 65536))
                        {
                            // write date of original file into mod file for caching
                            DateTime date = File.GetLastWriteTime(shader.FileName);
                            textWriter.WriteLine($"// Date: {date.ToString("O")}");
                        }
                    }
                }
                //}
                //else
                //{
                //    File.Copy($"{shader.FileName}_mod", shader.FileName, true);
                //}
            }
        }

        private static void ExpandRegion(Dictionary<string, Shader> shaders, TextWriter textWriter, LinkedList<ShaderLine> shaderLines, HashSet<(string ShaderName, string RegionName)> imported, ref bool isDirty)
        {
            foreach (ShaderLine shaderLine in shaderLines)
            {
                if (shaderLine.ImportRegion == default)
                {
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
                            isDirty = shader.IsCahced ? isDirty : true;

                            textWriter.WriteLine($"// begin import {shaderLine.ImportRegion.ShaderName}.{shaderLine.ImportRegion.RegionName}");
                            ExpandRegion(shaders, textWriter, region, imported, ref isDirty);
                            textWriter.WriteLine($"// end import {shaderLine.ImportRegion.ShaderName}.{shaderLine.ImportRegion.RegionName}");
                        }
                        else
                        {
                            Console.WriteLine($"[Shady] Import Error in {shaderLine.ShaderName}, line {shaderLine.LineIndex + 1}: Cannot import '{shaderLine.ImportRegion.RegionName}' from '{shaderLine.ImportRegion.ShaderName}', identifier doesn't exist!");
                        }

                    }
                    else
                    {
                        Console.WriteLine($"[Shady] Import Error in {shaderLine.ShaderName}, line {shaderLine.LineIndex + 1}: Cannot import '{shaderLine.ImportRegion.ShaderName}', shader doesn't exist or has no exported identifiers!");
                    }
                }
            }
        }
    }
}