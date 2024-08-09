using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Shady.Parser;

namespace Shady
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser parser = new Parser();

            string projectPath = @"C:\Projects\VisualStudio\Shady.gml\Example";
            string shadersPath = projectPath + @"\shaders";
            string path = shadersPath + @"\sh_example\sh_example.fsh";

            const bool forceNonParallel = true;
            var options = new ParallelOptions { MaxDegreeOfParallelism = forceNonParallel ? 1 : -1 };

            Parallel.ForEach(File.ReadLines(path), options, (line, state, index) =>
            {
                line = Regex.Replace(line, @"^\s+", "");

                Token? pragma = parser.Match(line, TokenType.Shady);
                if (pragma != null)
                {
                    //Console.WriteLine(pragma.Value);
                    line = Regex.Replace(pragma.RemainingInput, @"\s", "");

                    TokenType previousToken = TokenType.Shady;
                    List<TokenType> expectedTokens = new List<TokenType>() { TokenType.Import, TokenType.Inline, TokenType.Variant };
                    List<Token> lineTokens = new List<Token>();

                    while (expectedTokens.Count != 0)
                    {
                        try
                        {
                            Token token = parser.Expect(line, expectedTokens, previousToken);
                            //Console.WriteLine($"{token.Value}");

                            line = token.RemainingInput;
                            previousToken = token.TokenType;
                            expectedTokens.Clear();

                            switch (token.TokenType)
                            {
                                case TokenType.Import:
                                case TokenType.Inline:
                                case TokenType.Variant:
                                    expectedTokens.Add(TokenType.OpenBracket);
                                    break;

                                case TokenType.OpenBracket:
                                    expectedTokens.Add(TokenType.Identifier);
                                    break;

                                case TokenType.Identifier:
                                    expectedTokens.Add(TokenType.Dot);
                                    expectedTokens.Add(TokenType.CloseBracket);
                                    break;

                                case TokenType.Dot:
                                    expectedTokens.Add(TokenType.Identifier);
                                    expectedTokens.Add(TokenType.CloseBracket);
                                    break;

                                default:
                                    
                                    break;
                            }

                            lineTokens.Add(token);
                        }
                        catch (UnexpectedExpression e)
                        {
                            expectedTokens.Clear();
                            Console.WriteLine($"[Shady] Syntax Error {Path.GetFileName(path)}, line {index + 1}: {e.Message}");
                        }
                    }

                    Console.Write($"{index} ");
                    lineTokens.ForEach(token => Console.Write($"{token.Value} +"));
                    Console.WriteLine();

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
            });
        }
    }
}