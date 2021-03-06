﻿using System;
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
    /// <param name="rules">a list of rules. This list is the definition of what can be built.</param>
    /// <param name="rulesToBuild">
    ///   the outputs to build. These strings are matched against the outputs as defined in
    ///   <paramref name="rules" />.
    /// </param>
    /// <param name="workingDir">the directory relative to which the output and input files will be matched.</param>
    /// <exception cref="Exception">
    ///   thrown if there are duplicate rules specified in <paramref name="rules" /> or if there are
    ///   cycles between rules.
    /// </exception>
    public static void DoMake(IEnumerable<Rule> rules, IEnumerable<string> rulesToBuild = null, string workingDir = null) {
      workingDir = workingDir ?? Directory.GetCurrentDirectory();
      var rulesAsList = rules as IList<Rule> ?? rules.ToList();
      var allRules = ToOutput2RulesDict(rulesAsList);
      var taskGraph = TaskGraph.ToTaskGraph(GetRulesToBuild(rulesToBuild, allRules, rulesAsList),
                                           rule => rule.Output,
                                           rule => rule.Inputs.Select(name => allRules.Get(name)).Gather(),
                                           rule => () => InvokeRecipe(workingDir, rule));
      taskGraph.Run();
    }

    private static IEnumerable<Rule> GetRulesToBuild(IEnumerable<string> rulesToBuild,
                                                     IDictionary<string, Rule> allRules,
                                                     IEnumerable<Rule> rulesAsList) {
      if (rulesToBuild != null) {
        var toBuild = rulesToBuild as IList<string> ?? rulesToBuild.ToList();
        if (toBuild.Count > 0) {
          return toBuild.Select(name => allRules.Get(name).GetOrElse(() => {
            throw new Exception($"Could not find rule '{name}'.");
          }));
        }
      }
      return rulesAsList;
    }

    private static Dictionary<string, Rule> ToOutput2RulesDict(IList<Rule> rulesAsList) {
      var allRules = new Dictionary<string, Rule>();
      foreach (var r in rulesAsList) {
        if (allRules.ContainsKey(r.Output)) {
          throw new Exception($"Found a duplicate rule '{r.Output}'.");
        }
        allRules.Add(r.Output, r);
      }
      return allRules;
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
      => DoMake(rules, new[] {ruleToBuild}, workingDir);

    private static void InvokeRecipe(string workingDir, Rule rule)
      => TimestampBasedBuilder.Build(rule.Recipe,
                                     rule.Inputs
                                         .Select(input => Path.Combine(workingDir, input))
                                         .ToImmutableArray(),
                                     Path.Combine(workingDir, rule.Output));
  }
}