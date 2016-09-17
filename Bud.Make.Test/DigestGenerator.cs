using System.Collections.Generic;

namespace Bud {
  public class DigestGenerator {
    public static void Generate(IEnumerable<string> inputFiles, string outputFile)
      => Digest.CreateDigestsJsonFile(inputFiles, outputFile);
  }
}