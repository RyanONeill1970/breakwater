// Global usings for the engine-independent core. This file is also compiled into the test project.
//
// System.Numerics is deliberately *not* global: the Godot project imports Godot globally, and
// both namespaces export Vector2. Core files keep `using System.Numerics;` below their namespace
// declaration, which takes precedence over the global Godot import.
global using System.Text.Json;
global using System.Text.Json.Serialization;
