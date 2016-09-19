using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using static Bud.Make.Rules;

namespace Bud.Make {
  public class RulesTest {
    [Test]
    public void DoMake_invokes_the_recipe_when_output_file_not_present() {
      using (var dir = new TmpDir()) {
        dir.CreateFile("This is Sparta!", "foo.in");
        DoMake("foo.out", dir.Path, Rule("foo.out", RemoveSpaces, "foo.in"));
        FileAssert.AreEqual(dir.CreateFile("ThisisSparta!", "expected_output"),
                            dir.CreatePath("foo.out"));
      }
    }

    [Test]
    public void DoMake_does_not_invoke_the_recipe_when_output_file_is_newer() {
      using (var dir = new TmpDir()) {
        var recipeMock = new Mock<SingleFileBuilder>();
        var inputFile = dir.CreateEmptyFile("foo.in");
        var outputFile = dir.CreateEmptyFile("foo.out");
        File.SetLastWriteTimeUtc(inputFile, File.GetLastWriteTimeUtc(outputFile) - TimeSpan.FromSeconds(5));
        DoMake("foo.out", dir.Path, Rule("foo.out", recipeMock.Object, "foo.in"));
        recipeMock.Verify(s => s(It.IsAny<string>(), It.IsAny<string>()),
                          Times.Never);
      }
    }

    [Test]
    public void DoMake_throws_when_given_duplicate_rules() {
      var exception = Assert.Throws<Exception>(() => {
        DoMake("foo",
               Rule("foo", RemoveSpaces, "bar"),
               Rule("foo", RemoveSpaces, "moo"));
      });
      Assert.That(exception.Message, Does.Contain("'foo'"));
    }

    [Test]
    public void DoMake_throws_when_rule_does_not_exist() {
      var exception = Assert.Throws<Exception>(() => {
        DoMake("invalid.out", "/foo/bar", Rule("out", RemoveSpaces, "in"));
      });
      Assert.That(exception.Message, Does.Contain("'invalid.out'"));
    }

    [Test]
    public void DoMake_invokes_dependent_recipes() {
      using (var dir = new TmpDir()) {
        dir.CreateFile("foo bar", "foo");
        var expectedOutput = dir.CreateFile("FOO BAR and foobar", "expected_output");
        DoMake("foo.joined",
               dir.Path,
               Rule("foo.upper", Uppercase, "foo"),
               Rule("foo.nospace", RemoveSpaces, "foo"),
               Rule("foo.joined", WriteAndSeparatedFileContents, "foo.upper", "foo.nospace"));
        FileAssert.AreEqual(expectedOutput, dir.CreatePath("foo.joined"));
      }
    }

    [Test]
    public void DoMake_does_not_invoke_dependent_rules_twice() {
      var recipeMock = new Mock<SingleFileBuilder>();
      DoMake("foo.out3",
             "/foo/bar",
             Rule("foo.out1", recipeMock.Object, "foo.in"),
             Rule("foo.out2", (string inFile, string outFile) => {}, "foo.out1"),
             Rule("foo.out3", (inFiles, outFile) => {}, "foo.out1", "foo.out2"));
      recipeMock.Verify(s => s(It.IsAny<string>(), It.IsAny<string>()),
                        Times.Once);
    }

    [Test]
    public void DoMake_throws_when_there_is_a_cycle() {
      var recipeMock = new Mock<SingleFileBuilder>();
      var ex = Assert.Throws<Exception>(() => {
        DoMake("foo.out2",
               "/foo/bar",
               Rule("foo.out1", recipeMock.Object, "foo.in1"),
               Rule("foo.out2", recipeMock.Object, "foo.in2"),
               Rule("foo.in1", recipeMock.Object, "foo.out2"),
               Rule("foo.in2", recipeMock.Object, "foo.out1"));
      });
      Assert.That(ex.Message,
                  Does.Contain("'foo.out2 depends on foo.in2 depends on foo.out1 depends on foo.in1 depends on foo.out2'"));
    }

    [Test]
    public void DoMake_executes_rules_in_parallel() {
      using (var dir = new TmpDir()) {
        var latchA = new CountdownEvent(1);
        var latchB = new CountdownEvent(1);
        dir.CreateFile("should be upper", "foo.in1");
        dir.CreateFile("SHOULD BE LOWER", "foo.in2");
        var expectedOutput = dir.CreateFile("SHOULD BE UPPER and should be lower", "expected_output");
        DoMake("foo.joined",
               dir.Path,
               Rule("foo.upper", (input, output) => {
                 latchA.Signal();
                 latchB.Wait();
                 Uppercase(input, output);
               }, "foo.in1"),
               Rule("foo.lower", (s, file) => {
                 latchB.Signal();
                 latchA.Wait();
                 Lowercase(s, file);
               }, "foo.in2"),
               Rule("foo.joined", WriteAndSeparatedFileContents, "foo.upper", "foo.lower"));
        FileAssert.AreEqual(expectedOutput, dir.CreatePath("foo.joined"));
      }
    }

    [Test]
    public void DoMake_executes_many_independent_rules() {
      var recipeMock = new Mock<FilesBuilder>();
      DoMake(GetComplexRules(recipeMock), new[] {"foo.nospace", "bar.nospace"}, "/dir");

      var fooInput = ImmutableArray.Create(Path.Combine("/dir", "foo"));
      recipeMock.Verify(r => r(fooInput, Path.Combine("/dir", "foo.nospace")));

      var barInput = ImmutableArray.Create(Path.Combine("/dir", "bar"));
      recipeMock.Verify(r => r(barInput, Path.Combine("/dir", "bar.nospace")));
    }

    [Test]
    public void DoMake_does_not_invoke_repeated() {
      var recipeMock = new Mock<FilesBuilder>();
      DoMake(GetComplexRules(recipeMock), new[] {"foobar.nospace", "foobar.nospace.lu"}, "/dir");

      var fooInput = ImmutableArray.Create(Path.Combine("/dir", "foo"));
      recipeMock.Verify(r => r(fooInput, Path.Combine("/dir", "foo.nospace")), Times.Once);
    }

    [Test]
    public void DoMake_builds_all_by_default() {
      var recipeMock = new Mock<FilesBuilder>();
      var rules = GetComplexRules(recipeMock);
      DoMake(rules, workingDir: "/dir");

      foreach (var rule in rules) {
        var inputs = rule.Inputs.Select(s => Path.Combine("/dir", s)).ToImmutableArray();
        recipeMock.Verify(r => r(inputs, Path.Combine("/dir", rule.Output)), Times.Once);
      }
    }

    private static void RemoveSpaces(string inputFile, string outputFile) {
      var inputFileContent = File.ReadAllText(inputFile);
      var outputFileContent = inputFileContent.Replace(" ", "");
      File.WriteAllText(outputFile, outputFileContent);
    }

    private static void Uppercase(string inputFile, string outputFile) {
      var inputFileContent = File.ReadAllText(inputFile);
      var outputFileContent = inputFileContent.ToUpperInvariant();
      File.WriteAllText(outputFile, outputFileContent);
    }

    private static void Lowercase(string inputFile, string outputFile) {
      var inputFileContent = File.ReadAllText(inputFile);
      var outputFileContent = inputFileContent.ToLowerInvariant();
      File.WriteAllText(outputFile, outputFileContent);
    }

    private static void WriteAndSeparatedFileContents(ImmutableArray<string> files, string outputFile) {
      var inputFilesContent = files.Select(File.ReadAllText);
      var outputFileContent = string.Join(" and ", inputFilesContent);
      File.WriteAllText(outputFile, outputFileContent);
    }

    private static Rule[] GetComplexRules(IMock<FilesBuilder> recipeMock) => new[] {
      Rule("foo.nospace", recipeMock.Object, "foo"),
      Rule("foo.nospace.upper", recipeMock.Object, "foo.nospace"),
      Rule("bar.nospace", recipeMock.Object, "bar"),
      Rule("bar.nospace.lower", recipeMock.Object, "bar.nospace"),
      Rule("foobar.nospace.lu", recipeMock.Object, "foo.nospace.upper", "bar.nospace.lower"),
      Rule("foobar.nospace", recipeMock.Object, "foo.nospace", "bar.nospace")
    };
  }
}