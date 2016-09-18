using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace Bud.Make {
  public class RulesTest {
    [Test]
    public void DoMake_invokes_the_recipe_when_output_file_not_present() {
      using (var dir = new TmpDir()) {
        dir.CreateFile("This is Sparta!", "foo.in");
        Rules.DoMake("foo.out", dir.Path, Rules.Rule("foo.out", RemoveSpaces, "foo.in"));
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
        Rules.DoMake("foo.out", dir.Path, Rules.Rule("foo.out", recipeMock.Object, "foo.in"));
        recipeMock.Verify(s => s(It.IsAny<string>(), It.IsAny<string>()),
                          Times.Never);
      }
    }

    [Test]
    public void DoMake_throws_when_given_duplicate_rules() {
      var exception = Assert.Throws<Exception>(() => {
        Rules.DoMake("foo",
                     Rules.Rule("foo", RemoveSpaces, "bar"),
                     Rules.Rule("foo", RemoveSpaces, "moo"));
      });
      Assert.That(exception.Message, Does.Contain("'foo'"));
    }

    [Test]
    public void DoMake_throws_when_rule_does_not_exist() {
      var exception = Assert.Throws<Exception>(() => {
        Rules.DoMake("invalid.out", "/foo/bar", Rules.Rule("out", RemoveSpaces, "in"));
      });
      Assert.That(exception.Message, Does.Contain("'invalid.out'"));
    }

    [Test]
    public void DoMake_invokes_dependent_recipes() {
      using (var dir = new TmpDir()) {
        dir.CreateFile("foo bar", "foo");
        var expectedOutput = dir.CreateFile("FOO BAR and foobar", "expected_output");
        Rules.DoMake("foo.joined",
                     dir.Path,
                     Rules.Rule("foo.upper", Uppercase, "foo"),
                     Rules.Rule("foo.nospace", RemoveSpaces, "foo"),
                     Rules.Rule("foo.joined", WriteAndSeparatedFileContents, "foo.upper", "foo.nospace"));
        FileAssert.AreEqual(expectedOutput, dir.CreatePath("foo.joined"));
      }
    }

    [Test]
    public void DoMake_does_not_invoke_dependent_rules_twice() {
      var recipeMock = new Mock<SingleFileBuilder>();
      Rules.DoMake("foo.out3",
                   "/foo/bar",
                   Rules.Rule("foo.out1", recipeMock.Object, "foo.in"),
                   Rules.Rule("foo.out2", (string inFile, string outFile) => {}, "foo.out1"),
                   Rules.Rule("foo.out3", (inFiles, outFile) => {}, "foo.out1", "foo.out2"));
      recipeMock.Verify(s => s(It.IsAny<string>(), It.IsAny<string>()),
                        Times.Once);
    }

    [Test]
    public void DoMake_throws_when_there_is_a_cycle() {
      var recipeMock = new Mock<SingleFileBuilder>();
      var ex = Assert.Throws<Exception>(() => {
        Rules.DoMake("foo.out2",
                     "/foo/bar",
                     Rules.Rule("foo.out1", recipeMock.Object, "foo.in1"),
                     Rules.Rule("foo.out2", recipeMock.Object, "foo.in2"),
                     Rules.Rule("foo.in1", recipeMock.Object, "foo.out2"),
                     Rules.Rule("foo.in2", recipeMock.Object, "foo.out1"));
      });
      Assert.That(ex.Message,
                  Does.Contain("'foo.out2 <- foo.in2 <- foo.out1 <- foo.in1 <- foo.out2'"));
    }

    [Test]
    public void DoMake_executes_rules_in_parallel() {
      using (var dir = new TmpDir()) {
        var latchA = new CountdownEvent(1);
        var latchB = new CountdownEvent(1);
        dir.CreateFile("should be upper", "foo.in1");
        dir.CreateFile("SHOULD BE LOWER", "foo.in2");
        var expectedOutput = dir.CreateFile("SHOULD BE UPPER and should be lower", "expected_output");
        Rules.DoMake("foo.joined",
                     dir.Path,
                     Rules.Rule("foo.upper", (input, output) => {
                       latchA.Signal();
                       latchB.Wait();
                       Uppercase(input, output);
                     }, "foo.in1"),
                     Rules.Rule("foo.lower", (s, file) => {
                       latchB.Signal();
                       latchA.Wait();
                       Lowercase(s, file);
                     }, "foo.in2"),
                     Rules.Rule("foo.joined", WriteAndSeparatedFileContents, "foo.upper", "foo.lower"));
        FileAssert.AreEqual(expectedOutput, dir.CreatePath("foo.joined"));
      }
    }

    [Test]
    public void DoMake_executes_many_independent_rules() {
      var recipeMock = new Mock<FilesBuilder>();
      Rules.DoMake(new[] {"foo.nospace", "bar.nospace"}, GetComplexRules(recipeMock), "/dir");

      var fooInput = ImmutableArray.Create(Path.Combine("/dir", "foo"));
      recipeMock.Verify(r => r(fooInput, Path.Combine("/dir", "foo.nospace")));

      var barInput = ImmutableArray.Create(Path.Combine("/dir", "bar"));
      recipeMock.Verify(r => r(barInput, Path.Combine("/dir", "bar.nospace")));
    }

    [Test]
    public void DoMake_does_not_invoke_repeated() {
      var recipeMock = new Mock<FilesBuilder>();
      Rules.DoMake(new[] { "foobar.nospace", "foobar.nospace.lu" }, GetComplexRules(recipeMock), "/dir");

      var fooInput = ImmutableArray.Create(Path.Combine("/dir", "foo"));
      recipeMock.Verify(r => r(fooInput, Path.Combine("/dir", "foo.nospace")), Times.Once);
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
      Rules.Rule("foo.nospace", recipeMock.Object, "foo"),
      Rules.Rule("foo.nospace.upper", recipeMock.Object, "foo.nospace"),
      Rules.Rule("bar.nospace", recipeMock.Object, "bar"),
      Rules.Rule("bar.nospace.lower", recipeMock.Object, "bar.nospace"),
      Rules.Rule("foobar.nospace.lu", recipeMock.Object, "foo.nospace.upper", "bar.nospace.lower"),
      Rules.Rule("foobar.nospace", recipeMock.Object, "foo.nospace", "bar.nospace")
    };
  }
}