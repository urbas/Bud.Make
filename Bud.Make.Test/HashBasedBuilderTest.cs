using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Moq;
using NUnit.Framework;

namespace Bud.Make {
  public class HashBasedBuilderTest {
    private TmpDir dir;
    private Mock<FilesBuilder> outputGenerator;

    [SetUp]
    public void SetUp() {
      outputGenerator = new Mock<FilesBuilder>();
      outputGenerator.Setup(s => s(It.IsAny<ImmutableArray<string>>(), It.IsAny<string>()))
                     .Callback<IEnumerable<string>, string>(DigestGenerator.Generate);
      dir = new TmpDir();
    }

    [TearDown]
    public void TearDown() => dir.Dispose();

    [Test]
    public void Build_creates_the_output_file() {
      var output = dir.CreatePath("a.out");
      HashBasedBuilder.Build(outputGenerator.Object, ImmutableArray<string>.Empty, output);
      outputGenerator.Verify(self => self(ImmutableArray<string>.Empty, output),
                             Times.Once);
    }

    [Test]
    public void Build_not_invoked_second_time() {
      var output = dir.CreatePath("a.out");
      HashBasedBuilder.Build(outputGenerator.Object, ImmutableArray<string>.Empty, output);
      HashBasedBuilder.Build(outputGenerator.Object, ImmutableArray<string>.Empty, output);
      outputGenerator.Verify(self => self(ImmutableArray<string>.Empty, output), Times.Once);
    }

    [Test]
    public void Build_invoked_when_file_added() {
      var output = dir.CreatePath("a.out");
      var noFiles = ImmutableArray<string>.Empty;
      HashBasedBuilder.Build(outputGenerator.Object, noFiles, output);
      var singleFile = ImmutableArray.Create(dir.CreateFile("foo", "foo"));
      HashBasedBuilder.Build(outputGenerator.Object, singleFile, output);
      outputGenerator.Verify(self => self(singleFile, output), Times.Once);
    }

    [Test]
    public void Build_invoked_when_file_changed() {
      var output = dir.CreatePath("a.out");
      var fileFoo = dir.CreateFile("foo", "foo");
      var singleFile = ImmutableArray.Create(fileFoo);
      HashBasedBuilder.Build(outputGenerator.Object, singleFile, output);
      File.WriteAllText(fileFoo, "foobar");
      HashBasedBuilder.Build(outputGenerator.Object, singleFile, output);
      outputGenerator.Verify(self => self(singleFile, output), Times.Exactly(2));
    }

    [Test]
    public void Build_invoked_when_salt_changes() {
      var output = dir.CreatePath("a.out");
      var inputHashFile = dir.CreatePath("a.out.input_hash");
      var fileFoo = dir.CreateFile("foo", "foo");
      var singleFile = ImmutableArray.Create(fileFoo);
      HashBasedBuilder.Build(outputGenerator.Object, singleFile, output, inputHashFile, salt: new byte[] {0x00});
      HashBasedBuilder.Build(outputGenerator.Object, singleFile, output, inputHashFile, salt: new byte[] {0x01});
      outputGenerator.Verify(self => self(singleFile, output), Times.Exactly(2));
    }
  }
}