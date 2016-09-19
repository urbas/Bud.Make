[![Build status](https://ci.appveyor.com/api/projects/status/190xdtdaq6dotbjb/branch/master?svg=true)](https://ci.appveyor.com/project/urbas/bud-make/branch/master)

__Table of contents__

* [About](#about)


# About

Bud.Make is a C# library that provides functionality similar to GNU make. The rules are invoked in parallel.


## Example

The following example will build the file `foo.out` from the file `foo.in`.

```csharp
using System.IO;
using static Bud.Make.Rules;

class Build {
  static void Main(string[] args)
    => DoMake("foo.out", Rule("foo.out", RemoveSpaces, "foo.in"));

  static void RemoveSpaces(string inputFile, string outputFile)
    => File.WriteAllText(outputFile, File.ReadAllText(inputFile).Replace(" ", ""));
}
```

## Missing features

This library does not support pattern rules. You can implement pattern rules by generating rules for each file. The pseudocode below illustrates the idea:

```csharp
var cpp2ObjRules = new [] {"a.cpp", "b.cpp", ...}
  .Select(cppFile => Rule(ChangeExtension(cppFile, ".o"), CompileCppToObj, cppFile));
DoMake(cpp2ObjRules);
```

# TODO

-   Hash-based build (instead of timestamps).