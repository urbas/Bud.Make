using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Bud.Make {
  public class HasherTest {
    private static readonly byte[] Salt = {0x13};

    [Test]
    public void HashFiles_produces_a_different_hash_for_different_salts_and_empty_input()
      => Assert.AreNotEqual(Hasher.HashFiles(ImmutableArray<string>.Empty, Salt),
                     Hasher.HashFiles(ImmutableArray<string>.Empty, new byte[] {0x37}));

    [Test]
    public void HashFiles_produces_a_different_hash_for_different_input() {
      using (var dir = new TmpDir()) {
        Assert.AreNotEqual(Hasher.HashFiles(ImmutableArray<string>.Empty, Salt),
                    Hasher.HashFiles(ImmutableArray.Create(dir.CreateFile("foo", "a")), Salt));
      }
    }

    [Test]
    public void HashFiles_produces_the_same_hash_for_same_input() {
      using (var dir = new TmpDir()) {
        var file = dir.CreateFile("foo bar", "a");
        Assert.AreEqual(Hasher.HashFiles(ImmutableArray.Create(file), Salt),
                 Hasher.HashFiles(ImmutableArray.Create(file), Salt));
      }
    }

    [Test]
    public void HashFiles_produces_different_hash_for_different_order_of_input() {
      using (var dir = new TmpDir()) {
        var fileA = dir.CreateFile("foo bar", "a");
        var fileB = dir.CreateFile("1337", "b");
        Assert.AreNotEqual(Hasher.HashFiles(ImmutableArray.Create(fileA, fileB), Salt),
                    Hasher.HashFiles(ImmutableArray.Create(fileB, fileA), Salt));
      }
    }

    [Test]
    public void HashFiles_produces_the_SHA256_hash_of_an_empty_string_by_default()
      => Assert.AreEqual(ToBytesFromHexString("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"),
                  Hasher.HashFiles(Enumerable.Empty<string>()));

    [Test]
    public void HashFiles_produces_the_MD5_hash_of_an_empty_string()
      => Assert.AreEqual(ToBytesFromHexString("d41d8cd98f00b204e9800998ecf8427e"),
                  Hasher.HashFiles(Enumerable.Empty<string>(), hashAlgorithm: MD5.Create()));

    public static byte[] ToBytesFromHexString(string hex) {
      var numberOfBytes = hex.Length >> 1;
      var arr = new byte[numberOfBytes];
      for (int i = 0; i < numberOfBytes; ++i) {
        var hexDigitIndex = i << 1;
        arr[i] = (byte) ((ToByteFromHexDigit(hex[hexDigitIndex]) << 4) + ToByteFromHexDigit(hex[hexDigitIndex + 1]));
      }
      return arr;
    }

    public static int ToByteFromHexDigit(char hex) {
      int val = hex;
      return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
    }
  }
}