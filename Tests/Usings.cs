// System.Numerics stays file-local, as in Scripts/Core: those files are compiled into this project
// and declare it themselves, so a global import here would make theirs redundant (IDE0005).
global using System.Text.Json.Nodes;
global using Breakwater.Core;
global using Xunit;
