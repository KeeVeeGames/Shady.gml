using System.Text.RegularExpressions;
using static Shady.Parser;
using static System.Net.Mime.MediaTypeNames;

namespace Shady
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser parser = new Parser();

            string path = "C:\\Projects\\VisualStudio\\Shady.gml\\Example\\shaders\\sh_example\\sh_example.fsh";

            Parallel.ForEach(File.ReadLines(path), (line, state, index) =>
            {
                line = Regex.Replace(line, @"^\s+", "");

                Token? pragma = parser.Match(line, TokenType.Shady);
                if (pragma != null)
                {
                    Console.WriteLine(pragma.Value);
                    line = Regex.Replace(pragma.RemainingInput, @"\s", "");

                    TokenType previousToken = TokenType.Shady;
                    List<TokenType> expectedTokens = new List<TokenType>() { TokenType.Import, TokenType.Variant };

                    while (expectedTokens.Count != 0)
                    {
                        try
                        {
                            Token token = parser.Expect(line, expectedTokens, previousToken);

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
                                    expectedTokens.Add(TokenType.CloseBracket);
                                    break;

                                default:
                                    
                                    break;
                            }
                        }
                        catch (UnexpectedExpression e)
                        {
                            expectedTokens.Clear();
                            Console.WriteLine($"[Shady] Syntax Error {Path.GetFileName(path)}, line {index + 1}: {e.Message}");
                        }
                    }
                }
            });
        }
    }
}