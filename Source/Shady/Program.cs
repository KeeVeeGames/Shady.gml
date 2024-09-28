using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            string projectPath = @"C:\Projects\VisualStudio\Shady.gml\Example";
            //string projectPath = @"C:\Users\MusNik\Documents\GameMakerStudio2\CloseYourEyes";
            string shadersPath = projectPath + @"\shaders";

            string[] shaderFiles = Directory.GetFiles(projectPath, "*.fsh", SearchOption.AllDirectories);

            foreach (string shaderFile in shaderFiles)
            {
                string shaderName = Path.GetFileNameWithoutExtension(shaderFile);
                Shader shader = ParseShader(shaderFile);
                shaders.Add(shaderName, shader);
            }

            const bool forceNonParallel = true;
            var options = new ParallelOptions { MaxDegreeOfParallelism = forceNonParallel ? 1 : -1 };

            Parallel.ForEach(shaders, options, ParseTokens);

            Console.WriteLine("[Shady] Complete!");
        }

        private static Shader ParseShader(string path)
        {
            Shader shader = new Shader(Path.GetFileName(path));

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

            LinkedListNode<ShaderLine>? currentNode = shader.Lines.First;
            while (currentNode != null)
            {
                ShaderLine shaderLine = currentNode.Value;
                string line = Regex.Replace(shaderLine.Line, @"^\s+", "");  /// remove leading whitespaces
                //string line = Regex.Replace(shaderLine.Line, @"\s+", "");  /// remove all whitespaces
                string remainingLine;

                isLineIgnored = false;

                // Parse Shady tokens
                Token? pragma = parser.Match(line, TokenType.Shady);
                if (pragma != null)
                {
                    //Console.WriteLine(pragma.Value);
                    remainingLine = Regex.Replace(pragma.RemainingInput, @"\s", ""); /// remove all whitespaces
                    //remainingLine = pragma.RemainingInput;

                    TokenType previousToken = TokenType.Shady;
                    List<TokenType> expectedTokens = new List<TokenType>() { TokenType.Import, TokenType.Inline, TokenType.Variant };
                    List<Token> lineTokens = new List<Token>();

                    while (expectedTokens.Count != 0)
                    {
                        try
                        {
                            Token token = parser.Expect(remainingLine, expectedTokens, previousToken);
                            //Console.WriteLine($"{token.Value}");

                            remainingLine = token.RemainingInput;
                            expectedTokens.Clear();

                            switch (token.TokenType)
                            {
                                case TokenType.Import:
                                case TokenType.Inline:
                                case TokenType.Variant:
                                    expectedTokens.Add(TokenType.OpenParen);
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

                                default:

                                    break;
                            }

                            previousToken = token.TokenType;
                            lineTokens.Add(token);
                        }
                        catch (UnexpectedExpression e)
                        {
                            expectedTokens.Clear();
                            Console.WriteLine($"[Shady] Syntax Error {Path.GetFileName(shaderLine.ShaderName)}, line {shaderLine.LineIndex + 1}: {e.Message}");
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
                                int indexShader = 2;
                                int indexIdentifier = 4;

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

                            // Parse uniform
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
                                    Console.WriteLine($"[Shady] Syntax Error {Path.GetFileName(shaderLine.ShaderName)}, line {shaderLine.LineIndex + 1}: {e.Message}");
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

                if (!isCommented && !isLineIgnored && !inMain)
                {
                    shader.AddToRegion(Shader.FullRegion, shaderLine);

                    if (!string.IsNullOrEmpty(regionNameFunction))
                    {
                        shader.AddToRegion(regionNameFunction, shaderLine);
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
                        regionNameFunction = string.Empty;
                    }
                }

                Debug.WriteLine($"{shaderLine.ShaderName}.{level}: {shaderLine.Line}");
                currentNode = currentNode.Next;
            }
        }
    }
}