using System.Collections.Immutable;

namespace Bud.Make {
  /// <summary>
  ///   This function reads the contents of the given <paramref name="files" />, add the <paramref name="salt" />, and
  ///   calculate the digest of the mix.
  /// </summary>
  /// <param name="files">the list of files for which to calculate a hash digest.</param>
  /// <param name="salt">an array of bytes to be added to the hash calculation.</param>
  public delegate byte[] FilesHasher(ImmutableArray<string> files, byte[] salt);
}