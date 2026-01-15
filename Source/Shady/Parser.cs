using System.Text.RegularExpressions;

namespace Shady
{
    internal enum TokenType
    {
        Shady,
        Import,
        Inline,
        MacroBegin,
        MacroEnd,
        Variant,
        SkipCompilation,
        PrintPath,
        OpenParen,
        CloseParen,
        Identifier,
        Argument,
        Name,
        Dot,
        Comma,
        Empty,

        LineComment,
        OpenComment,
        CloseComment,
        OpenBrace,
        CloseBrace,
        Varying,
        Uniform,
        Precision,
        Define,
        Assignment,
        Function,
        Main
    }

    internal class Parser
    {
        private TokenRegex[] tokensRegexes = new TokenRegex[Enum.GetNames(typeof(TokenType)).Length];

        public Parser()
        {
            // expected
            tokensRegexes[(int)TokenType.Shady] = new TokenRegex(TokenType.Shady, "^#pragma shady:", "'shady:'");
            tokensRegexes[(int)TokenType.Import] = new TokenRegex(TokenType.Import, "^import", "'import'");
            tokensRegexes[(int)TokenType.Inline] = new TokenRegex(TokenType.Inline, "^inline", "'inline'");
            tokensRegexes[(int)TokenType.MacroBegin] = new TokenRegex(TokenType.MacroBegin, "^macro_begin", "'macro_begin'");
            tokensRegexes[(int)TokenType.MacroEnd] = new TokenRegex(TokenType.MacroEnd, "^macro_end", "'macro_end'");
            tokensRegexes[(int)TokenType.Variant] = new TokenRegex(TokenType.Variant, "^variant", "'variant'");
            tokensRegexes[(int)TokenType.SkipCompilation] = new TokenRegex(TokenType.SkipCompilation, "^skip_compilation", "'skip_compilation'");
            tokensRegexes[(int)TokenType.PrintPath] = new TokenRegex(TokenType.PrintPath, "^print_path", "'print_path'");
            tokensRegexes[(int)TokenType.OpenParen] = new TokenRegex(TokenType.OpenParen, @"^\(", "open paren '('");
            tokensRegexes[(int)TokenType.CloseParen] = new TokenRegex(TokenType.CloseParen, @"^\)", "close paren ')'");
            tokensRegexes[(int)TokenType.Identifier] = new TokenRegex(TokenType.Identifier, @"^\w+", "shader/function/variable/macro identifier");
            tokensRegexes[(int)TokenType.Argument] = new TokenRegex(TokenType.Argument, @"^\w+", "pragma argument");
            tokensRegexes[(int)TokenType.Name] = new TokenRegex(TokenType.Name, @"^\w+", "macro name");
            tokensRegexes[(int)TokenType.Dot] = new TokenRegex(TokenType.Dot, @"^[.]", "dot '.'");
            tokensRegexes[(int)TokenType.Comma] = new TokenRegex(TokenType.Comma, @"^[,]", "comma ','");
            tokensRegexes[(int)TokenType.Empty] = new TokenRegex(TokenType.Empty, @"^$", "nothing (end-of-line)");

            // free
            tokensRegexes[(int)TokenType.LineComment] = new TokenRegex(TokenType.LineComment, @"^\/{2}", "line comment '//'");
            tokensRegexes[(int)TokenType.OpenComment] = new TokenRegex(TokenType.OpenComment, @"\/\*", "open comment '/*'");
            tokensRegexes[(int)TokenType.CloseComment] = new TokenRegex(TokenType.CloseComment, @"\*\/", "close comment '*/'");
            tokensRegexes[(int)TokenType.OpenBrace] = new TokenRegex(TokenType.OpenBrace, @"\{", "open brace '{'");
            tokensRegexes[(int)TokenType.CloseBrace] = new TokenRegex(TokenType.CloseBrace, @"\}", "close brace '}'");
            tokensRegexes[(int)TokenType.Varying] = new TokenRegex(TokenType.Varying, @"^varying", "varying");
            tokensRegexes[(int)TokenType.Uniform] = new TokenRegex(TokenType.Uniform, @"^uniform", "uniform");
            tokensRegexes[(int)TokenType.Precision] = new TokenRegex(TokenType.Precision, @"^precision", "precision");
            tokensRegexes[(int)TokenType.Define] = new TokenRegex(TokenType.Define, @"^#define", "#define");
            tokensRegexes[(int)TokenType.Assignment] = new TokenRegex(TokenType.Assignment, @"\w+\s*\=", "assignemnt '='");
            tokensRegexes[(int)TokenType.Function] = new TokenRegex(TokenType.Function, @"\w+\s*\(", "function()");
            tokensRegexes[(int)TokenType.Main] = new TokenRegex(TokenType.Main, @"main\(", "main()");

        }

        public Token? Match(string input, TokenType tokenType)
        {
            return tokensRegexes[(int)tokenType].Match(input);
        }

        public Token Expect(string input, TokenType expectedToken, TokenType previousToken)
        {
            Token? token = tokensRegexes[(int)expectedToken].Match(input);
            if (token != null)
            {
                return token;
            }

            throw new UnexpectedExpression($"Undexpected expression found! The {
                tokensRegexes[(int)expectedToken].Description
            } is expected after {
                tokensRegexes[(int)previousToken].Description
            }.");
        }

        public Token Expect(string input, List<TokenType> expectedTokens, TokenType previousToken)
        {
            foreach (TokenType tokenType in expectedTokens)
            {
                Token? token = tokensRegexes[(int)tokenType].Match(input);
                if (token != null)
                {
                    return token;
                }
            }

            string expectedExpressions = string.Join(" or ", expectedTokens.Select(t => $"{tokensRegexes[(int)t].Description}"));

            throw new UnexpectedExpression($"Undexpected expression found! The {
                expectedExpressions
            } is expected after {
                tokensRegexes[(int)previousToken].Description
            }, got '{
                input
            }' instead.");
        }

        private class TokenRegex
        {
            private readonly TokenType _tokenType;
            private readonly Regex _regex;
            public string Description { get; private set; }

            public TokenRegex(TokenType tokenType, string regexPattern, string description = "")
            {
                Description = description;
                _tokenType = tokenType;
                _regex = new Regex(regexPattern);
            }

            public Token? Match(string input)
            {
                var match = _regex.Match(input);
                if (match.Success)
                {
                    string remainingInput = input.Substring(match.Length);

                    return new Token(_tokenType, match.Value, remainingInput);
                }
                else
                {
                    return null;
                }
            }
        }

        public class Token
        {
            public TokenType TokenType { get; private set; }
            public string Value { get; private set; }
            public string RemainingInput { get; private set; }

            public Token(TokenType tokenType, string value, string remainingInput)
            {
                TokenType = tokenType;
                Value = value;
                RemainingInput = remainingInput;
            }
        }

        public class UnexpectedExpression : Exception
        {
            public string ExpectedExpression { get; private set; }
            public UnexpectedExpression(string message) : base(message)
            {

            }
        }
    }

}
