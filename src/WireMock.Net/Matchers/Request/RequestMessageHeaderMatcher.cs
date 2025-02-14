using System;
using System.Collections.Generic;
using System.Linq;
using Stef.Validation;
using WireMock.Types;

namespace WireMock.Matchers.Request;

/// <summary>
/// The request header matcher.
/// </summary>
/// <inheritdoc cref="IRequestMatcher"/>
public class RequestMessageHeaderMatcher : IRequestMatcher
{
    private readonly MatchBehaviour _matchBehaviour;
    private readonly bool _ignoreCase;

    /// <summary>
    /// The functions
    /// </summary>
    public Func<IDictionary<string, string[]>, bool>[]? Funcs { get; }

    /// <summary>
    /// The name
    /// </summary>
    public string Name { get; }

    /// <value>
    /// The matchers.
    /// </value>
    public IStringMatcher[]? Matchers { get; }

    /// <summary>
    /// The <see cref="MatchOperator"/>
    /// </summary>
    public MatchOperator MatchOperator { get; } = MatchOperator.Or;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageHeaderMatcher"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="ignoreCase">Ignore the case from the pattern.</param>
    /// <param name="matchBehaviour">The match behaviour.</param>
    public RequestMessageHeaderMatcher(MatchBehaviour matchBehaviour, string name, string pattern, bool ignoreCase)
    {
        Guard.NotNull(name);
        Guard.NotNull(pattern);

        _matchBehaviour = matchBehaviour;
        _ignoreCase = ignoreCase;
        Name = name;
        Matchers = new IStringMatcher[] { new WildcardMatcher(matchBehaviour, pattern, ignoreCase) };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageHeaderMatcher"/> class.
    /// </summary>
    /// <param name="matchBehaviour">The match behaviour.</param>
    /// <param name="matchOperator">The <see cref="MatchOperator"/> to use.</param>
    /// <param name="name">The name.</param>
    /// <param name="patterns">The patterns.</param>
    /// <param name="ignoreCase">Ignore the case from the pattern.</param>
    public RequestMessageHeaderMatcher(MatchBehaviour matchBehaviour, MatchOperator matchOperator, string name, bool ignoreCase, params string[] patterns) :
        this(matchBehaviour, matchOperator, name, ignoreCase, patterns.Select(pattern => new WildcardMatcher(matchBehaviour, pattern, ignoreCase)).Cast<IStringMatcher>().ToArray())
    {
        Guard.NotNull(patterns);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageHeaderMatcher"/> class.
    /// </summary>
    /// <param name="matchBehaviour">The match behaviour.</param>
    /// <param name="matchOperator">The <see cref="MatchOperator"/> to use.</param>
    /// <param name="name">The name.</param>
    /// <param name="matchers">The matchers.</param>
    /// <param name="ignoreCase">Ignore the case from the pattern.</param>
    public RequestMessageHeaderMatcher(MatchBehaviour matchBehaviour, MatchOperator matchOperator, string name, bool ignoreCase, params IStringMatcher[] matchers)
    {
        Guard.NotNull(name);
        Guard.NotNull(matchers);

        _matchBehaviour = matchBehaviour;
        MatchOperator = matchOperator;
        Name = name;
        Matchers = matchers;
        _ignoreCase = ignoreCase;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestMessageHeaderMatcher"/> class.
    /// </summary>
    /// <param name="funcs">The funcs.</param>
    public RequestMessageHeaderMatcher(params Func<IDictionary<string, string[]>, bool>[] funcs)
    {
        Funcs = Guard.NotNull(funcs);
        Name = string.Empty; // Not used when Func, but set to a non-null valid value.
    }

    /// <inheritdoc />
    public double GetMatchingScore(IRequestMessage requestMessage, IRequestMatchResult requestMatchResult)
    {
        double score = IsMatch(requestMessage);
        return requestMatchResult.AddScore(GetType(), score);
    }

    private double IsMatch(IRequestMessage requestMessage)
    {
        if (requestMessage.Headers == null)
        {
            return MatchBehaviourHelper.Convert(_matchBehaviour, MatchScores.Mismatch);
        }

        // Check if we want to use IgnoreCase to compare the Header-Name and Header-Value(s)
        var headers = !_ignoreCase ? requestMessage.Headers : new Dictionary<string, WireMockList<string>>(requestMessage.Headers, StringComparer.OrdinalIgnoreCase);

        if (Funcs != null)
        {
            var funcResults = Funcs.Select(f => f(headers.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()))).ToArray();
            return MatchScores.ToScore(funcResults, MatchOperator);
        }

        if (Matchers != null)
        {
            if (!headers.ContainsKey(Name))
            {
                return MatchBehaviourHelper.Convert(_matchBehaviour, MatchScores.Mismatch);
            }

            var results = new List<double>();
            foreach (var matcher in Matchers)
            {
                var resultsPerMatcher = headers[Name].Select(v => matcher.IsMatch(v)).ToArray();

                results.Add(MatchScores.ToScore(resultsPerMatcher, MatchOperator.And));
            }

            return MatchScores.ToScore(results, MatchOperator);
        }

        return MatchBehaviourHelper.Convert(_matchBehaviour, MatchScores.Mismatch);
    }
}