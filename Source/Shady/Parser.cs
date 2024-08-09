using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shady
{
    internal enum TokenType
    {
        Shady,
        Import,
        Inline,
        Variant,
        OpenBracket,
        CloseBracket,
        Identifier,
        Dot,
        Comma
    }

    internal class Parser
    {
        private TokenRegex[] tokensRegexes = new TokenRegex[Enum.GetNames(typeof(TokenType)).Length];

        public Parser()
        {
            tokensRegexes[(int)TokenType.Shady] = new TokenRegex(TokenType.Shady, "^#pragma shady:", "'shady:'");
            tokensRegexes[(int)TokenType.Import] = new TokenRegex(TokenType.Import, "^import", "'import'");
            tokensRegexes[(int)TokenType.Inline] = new TokenRegex(TokenType.Inline, "^inline", "'inline'");
            tokensRegexes[(int)TokenType.Variant] = new TokenRegex(TokenType.Variant, "^variant", "'variant'");
            tokensRegexes[(int)TokenType.OpenBracket] = new TokenRegex(TokenType.OpenBracket, @"^\(", "open bracket '('");
            tokensRegexes[(int)TokenType.CloseBracket] = new TokenRegex(TokenType.CloseBracket, @"^\)", "close bracket ')'");
            tokensRegexes[(int)TokenType.Identifier] = new TokenRegex(TokenType.Identifier, @"^\w+", "shader/function/macro identifier");
            tokensRegexes[(int)TokenType.Dot] = new TokenRegex(TokenType.Dot, @"^[.]", "dot '.'");
            tokensRegexes[(int)TokenType.Comma] = new TokenRegex(TokenType.Comma, @"^[,]", "comma ','");
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

            throw new UnexpectedExpression($"Undexpected expression found! The {tokensRegexes[(int)expectedToken].Description} is expected after {tokensRegexes[(int)previousToken].Description}.");
        }

        public Token Expect(string input, List<TokenType> expectedTokens, TokenType previousToken)
        {
            foreach (TokenType tokenType in expectedTokens)
            {
                Token? token = tokensRegexes[(int)tokenType].Match(input);
                if (token != null) {
                    return token;
                }
            }

            string expectedExpressions = string.Join(" or ", expectedTokens.Select(t => $"{tokensRegexes[(int)t].Description}"));

            throw new UnexpectedExpression($"Undexpected expression found! The {expectedExpressions} is expected after {tokensRegexes[(int)previousToken].Description}.");
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
