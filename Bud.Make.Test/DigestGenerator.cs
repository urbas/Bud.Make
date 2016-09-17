using System.Collections.Generic;

namespace Bud.Make {
  public class DigestGenerator {
    public static void Generate(IEnumerable<string> inputFiles, string outputFile)
      => Digest.CreateDigestsJsonFile(inputFiles, outputFile);
  }
}