__Table of contents__

* [About](#about)


# About

Bud.Make is a C# library that provides functionality similar to GNU make.


## Example

The following example will build the file `foo.out` from the file `foo.in`.

```csharp
using System.IO;
using static Bud.Make;

class Build {
  static void Main(string[] args)
    => DoMake("foo.out", Rule("foo.out", RemoveSpaces, "foo.in"));

  static void RemoveSpaces(string inputFile, string outputFile)
    => File.WriteAllText(outputFile, File.ReadAllText(inputFile).Replace(" ", ""));
}
```