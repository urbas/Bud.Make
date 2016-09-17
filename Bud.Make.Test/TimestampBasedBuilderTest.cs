﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Moq;
using NUnit.Framework;

namespace Bud.Make {
  public class TimestampBasedBuilderTest {
    private TmpDir dir;
    private Mock<FilesBuilder> outputGenerator;

    [SetUp]
    public void SetUp() {
      outputGenerator = new Mock<FilesBuilder>(MockBehavior.Strict);
      outputGenerator.Setup(s => s(It.IsAny<ImmutableArray<string>>(), It.IsAny<string>()))
                     .Callback<IEnumerable<string>, string>(DigestGenerator.Generate);
      dir = new TmpDir();
    }

    [TearDown]
    public void TearDown() => dir.Dispose();

    [Test]
    public void Build_creates_the_output_file() {
      var output = dir.CreatePath("a.out");
      TimestampBasedBuilder.Build(outputGenerator.Object, ImmutableArray<string>.Empty, output);
      outputGenerator.Verify(self => self(ImmutableArray<string>.Empty, output), Times.Once);
    }

    [Test]
    public void Build_does_not_invoke_the_worker_when_output_already_exists() {
      var output = dir.CreateEmptyFile("a.out");
      TimestampBasedBuilder.Build(outputGenerator.Object, ImmutableArray<string>.Empty, output);
    }

    [Test]
    public void Build_invokes_the_worker_when_the_output_is_stale() {
      var output = dir.CreateEmptyFile("a.out");
      var fileA = dir.CreateFile("foo", "a");
      File.SetLastWriteTime(output, File.GetLastWriteTime(fileA) - TimeSpan.FromSeconds(1));
      var input = ImmutableArray.Create(fileA);

      TimestampBasedBuilder.Build(outputGenerator.Object, input, output);

      outputGenerator.Verify(self => self(input, output), Times.Once);
    }

    [Test]
    public void Build_does_not_invoke_the_worker_when_the_output_is_up_to_date() {
      var fileA = dir.CreateFile("foo", "a");
      var output = dir.CreateEmptyFile("a.out");
      File.SetLastWriteTime(fileA, File.GetLastWriteTime(output) - TimeSpan.FromSeconds(1));
      var inputFiles = ImmutableArray.Create(fileA);
      TimestampBasedBuilder.Build(outputGenerator.Object, inputFiles, output);
    }
  }
}