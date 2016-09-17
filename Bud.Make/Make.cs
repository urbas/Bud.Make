using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Bud {
  /// <summary>
  ///   A library that provides functionality similar to GNU Make.
  /// </summary>
  public static class Make {
    /// <summary>
    ///   Creates a <see cref="Bud.Rule" />. A rule contains a <paramref name="recipe" /> that describes how to build the
    ///   <paramref name="output" /> from the given <paramref name="input" />.
    /// </summary>
    /// <param name="output">
    ///   the file that the <paramref name="recipe" /> generated from the given <paramref name="input" />
    /// </param>
    /// <param name="recipe">
    ///   the algorithm that generates the <paramref name="output" /> from the given
    ///   <paramref name="input" />.
    /// </param>
    /// <param name="input">the file from which to build the <paramref name="output" /></param>
    /// <returns>
    ///   a <see cref="Bud.Rule" /> initialised with a <paramref name="recipe" />, <paramref name="output" />, and
    ///   <paramref name="input" />.
    /// </returns>
    public static Rule Rule(string output,
                            SingleFileBuilder recipe,
                            string input)
      => new Rule(output,
                  (inputFiles, outputFile) => recipe(inputFiles[0], outputFile),
                  ImmutableArray.Create(input));

    /// <summary>
    ///   Creates a <see cref="Bud.Rule" />. A rule contains a <paramref name="recipe" /> that describes how to build the
    ///   <paramref name="output" /> from the given <paramref name="input" />.
    /// </summary>
    /// <param name="output">
    ///   the file that the <paramref name="recipe" /> generated from the given <paramref name="input" />
    /// </param>
    /// <param name="recipe">
    ///   the algorithm that generates the <paramref name="output" /> from the given
    ///   <paramref name="input" />.
    /// </param>
    /// <param name="input">the files from which to build the <paramref name="output" /></param>
    /// <returns>
    ///   a <see cref="Bud.Rule" /> initialised with a <paramref name="recipe" />, <paramref name="output" />, and
    ///   <paramref name="input" />.
    /// </returns>
    public static Rule Rule(string output,
                            FilesBuilder recipe,
                            params string[] input)
      => new Rule(output, recipe, ImmutableArray.CreateRange(input));

    /// <summary>
    ///   Executes rule <paramref name="ruleToBuild" /> as defined in <paramref name="rules" />.
    ///   This method executes the rules in a single thread synchronously.
    /// </summary>
    public static void DoMake(string ruleToBuild, params Rule[] rules)
      => DoMake(ruleToBuild, Directory.GetCurrentDirectory(), rules);

    /// <summary>
    ///   Executes rule <paramref name="ruleToBuild" /> as defined in <paramref name="rules" />.
    ///   This method executes the rules in a single thread synchronously.
    /// </summary>
    public static void DoMake(string ruleToBuild, string workingDir, params Rule[] rules) {
      var rulesDictionary = new Dictionary<string, Rule>();
      foreach (var r in rules) {
        if (rulesDictionary.ContainsKey(r.Output)) {
          throw new Exception($"Found a duplicate rule '{r.Output}'.");
        }
        rulesDictionary.Add(r.Output, r);
      }
      var ruleOptional = rulesDictionary.Get(ruleToBuild);
      if (!ruleOptional.HasValue) {
        throw new Exception($"Could not find rule '{ruleToBuild}'.");
      }
      var rule = ruleOptional.Value;
      InvokeRecipe(workingDir, rulesDictionary, rule, new HashSet<string>(), new List<string>());
    }

    private static void InvokeRecipe(string workingDir,
                                     IDictionary<string, Rule> rulesDictionary,
                                     Rule rule,
                                     ISet<string> alreadyInvokedRules,
                                     IList<string> currentlyExecutingRules) {
      if (currentlyExecutingRules.Contains(rule.Output)) {
        throw new Exception($"Detected a cycle in rule dependencies: " +
                            $"'{rule.Output} -> {string.Join(" -> ", currentlyExecutingRules.Reverse())}'.");
      }
      if (alreadyInvokedRules.Contains(rule.Output)) {
        return;
      }
      currentlyExecutingRules.Add(rule.Output);
      foreach (var dependentRule in rule.Inputs.Gather(rulesDictionary.Get)) {
        InvokeRecipe(workingDir, rulesDictionary, dependentRule, alreadyInvokedRules, currentlyExecutingRules);
      }
      var inputAbsPaths = rule.Inputs
                              .Select(input => Path.Combine(workingDir, input))
                              .ToImmutableArray();
      TimestampBasedBuilder.Build(rule.Recipe,
                                  inputAbsPaths,
                                  Path.Combine(workingDir, rule.Output));
      alreadyInvokedRules.Add(rule.Output);
      currentlyExecutingRules.RemoveAt(currentlyExecutingRules.Count - 1);
    }
  }
}