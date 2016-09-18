using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Bud.Make {
  /// <summary>
  ///   A library that provides functionality similar to GNU Make.
  /// </summary>
  public static class Rules {
    /// <summary>
    ///   Creates a <see cref="Make.Rule" />. A startRule contains a <paramref name="recipe" /> that describes how to build the
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
    ///   a <see cref="Make.Rule" /> initialised with a <paramref name="recipe" />, <paramref name="output" />, and
    ///   <paramref name="input" />.
    /// </returns>
    public static Rule Rule(string output, SingleFileBuilder recipe, string input)
      => new Rule(output,
                  (inputFiles, outputFile) => recipe(inputFiles[0], outputFile),
                  ImmutableArray.Create(input));

    /// <summary>
    ///   Creates a <see cref="Make.Rule" />. A startRule contains a <paramref name="recipe" /> that describes how to build the
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
    ///   a <see cref="Make.Rule" /> initialised with a <paramref name="recipe" />, <paramref name="output" />, and
    ///   <paramref name="input" />.
    /// </returns>
    public static Rule Rule(string output, FilesBuilder recipe, params string[] input)
      => new Rule(output, recipe, ImmutableArray.CreateRange(input));

    /// <summary>
    ///   Executes <paramref name="rulesToBuild" /> in parallel.
    /// </summary>
    /// <param name="rulesToBuild">
    ///   the outputs to build. These strings are matched against the outputs as defined in
    ///   <paramref name="rules" />.
    /// </param>
    /// <param name="rules">a list of rules. This list is the definition of what can be built.</param>
    /// <param name="workingDir">the directory relative to which the output and input files will be matched.</param>
    /// <exception cref="Exception">
    ///   thrown if there are duplicate rules specified in <paramref name="rules" /> or if there are
    ///   cycles between rules.
    /// </exception>
    public static void DoMake(IEnumerable<string> rulesToBuild, IEnumerable<Rule> rules, string workingDir = null) {
      workingDir = workingDir ?? Directory.GetCurrentDirectory();
      var allRules = new Dictionary<string, Rule>();
      foreach (var r in rules) {
        if (allRules.ContainsKey(r.Output)) {
          throw new Exception($"Found a duplicate rule '{r.Output}'.");
        }
        allRules.Add(r.Output, r);
      }
      var resolvedRulesToBuild = rulesToBuild.Select(name => allRules.Get(name).GetOrElse(() => {
        throw new Exception($"Could not find rule '{name}'.");
      }));
      var taskGraph = TaskGraph.ToTaskGraph(resolvedRulesToBuild,
                                            rule => rule.Output,
                                            rule => rule.Inputs.Select(name => allRules.Get(name)).Gather(),
                                            rule => () => InvokeRecipe(workingDir, rule));
      taskGraph.Run();
    }

    /// <summary>
    ///   Executes <paramref name="ruleToBuild" /> as defined in <paramref name="rules" />.
    ///   This method executes the rules asynchronously and in parallel.
    /// </summary>
    public static void DoMake(string ruleToBuild, params Rule[] rules)
      => DoMake(ruleToBuild, Directory.GetCurrentDirectory(), rules);

    /// <summary>
    ///   Executes startRule <paramref name="ruleToBuild" /> as defined in <paramref name="rules" />.
    ///   This method executes the rules in a single thread synchronously.
    /// </summary>
    public static void DoMake(string ruleToBuild, string workingDir, params Rule[] rules)
      => DoMake(new[] {ruleToBuild}, rules, workingDir);

    private static void InvokeRecipe(string workingDir, Rule rule)
      => TimestampBasedBuilder.Build(rule.Recipe,
                                     rule.Inputs
                                         .Select(input => Path.Combine(workingDir, input))
                                         .ToImmutableArray(),
                                     Path.Combine(workingDir, rule.Output));
  }
}