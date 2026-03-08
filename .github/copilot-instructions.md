# Copilot Instructions

## Project Overview

**NuGroom** is a .NET 10 command-line tool (packaged as a global dotnet tool via `dotnet tool install --global NuGroom`) that connects to **Azure DevOps**, discovers C#/VB.NET/F# project files across repositories, and provides comprehensive NuGet package management including vulnerability scanning, automated updates, Central Package Management (CPM) migration, and SBOM export.

### Solution Structure

| Project | Description |
|---------|-------------|
| `NuGroom/` | Main console application and library (`net10.0`) |
| `NuGroom.Tests/` | NUnit unit tests (`net10.0`) |
| `NuGroom.Vsix/` | Visual Studio extension wrapper (`net48`) |

Key subdirectories of `NuGroom/`:
- `ADO/` — Azure DevOps client and API wrappers
- `Configuration/` — Config file loading and validation
- `Nuget/` — NuGet feed resolution and metadata
- `Vulnerability/` — NuGet advisory and OSV.dev scanning
- `Workflows/` — High-level operation workflows (scan, update, migrate, local scan)
- `Reporting/` — Export (JSON, CSV, SPDX SBOM)

### Build and Test

```bash
# Build the whole solution
dotnet build NuGroom.slnx

# Run tests (always build first)
dotnet build NuGroom.slnx
dotnet test NuGroom.Tests/NuGroom.Tests.csproj --no-build
```

### Key Conventions

- **Logger**: Use `Logger.Info()`, `Logger.Warning()`, `Logger.Error()`, `Logger.Debug()` (defined in `NuGroom/ErrorHandling.cs`).
- **CLI parsing**: New CLI options require changes in `CommandLineParser.cs` — add to `ParseResult`, `CliParsingState`, the switch case in `ParseCommandLineArguments`, `BuildSuccessfulParseResult`, and `ShowHelp`.
- **Parameter validation**: Use `ArgumentNullException.ThrowIfNull` for null checks on method parameters.
- **File paths**: ADO `GitItem.Path` and CPM paths use a leading `/` (e.g., `/Directory.Packages.props`).
- **Tool packaging**: The tool command name is `nugroom`; configured via `<PackAsTool>true</PackAsTool>` in `NuGroom.csproj`.

---

## Core Directives & Hierarchy

This section outlines the absolute order of operations. These rules have the highest priority and must not be violated.

1.  **Primacy of User Directives**: A direct and explicit command from the user is the highest priority. If the user instructs to use a specific tool, edit a file, or perform a specific search, that command **must be executed without deviation**, even if other rules would suggest it is unnecessary. All other instructions are subordinate to a direct user order.
2.  **Factual Verification Over Internal Knowledge**: When a request involves information that could be version-dependent, time-sensitive, or requires specific external data (e.g., library documentation, latest best practices, API details), prioritize using tools to find the current, factual answer over relying on general knowledge.
3.  **Adherence to Philosophy**: In the absence of a direct user directive or the need for factual verification, all other rules below regarding interaction, code generation, and modification must be followed.

## General Interaction & Philosophy

-   **Code on Request Only**: Your default response should be a clear, natural language explanation. Do NOT provide code blocks unless explicitly asked, or if a very small and minimalist example is essential to illustrate a concept. Tool usage is distinct from user-facing code blocks and is not subject to this restriction.
-   **Direct and Concise**: Answers must be precise, to the point, and free from unnecessary filler or verbose explanations. Get straight to the solution without "beating around the bush".
-   **Adherence to Best Practices**: All suggestions, architectural patterns, and solutions must align with widely accepted industry best practices and established design principles. Avoid experimental, obscure, or overly "creative" approaches. Stick to what is proven and reliable.
-   **Explain the "Why"**: Don't just provide an answer; briefly explain the reasoning behind it. Why is this the standard approach? What specific problem does this pattern solve? This context is more valuable than the solution itself.

## Minimalist & Standard Code Generation

-   **Principle of Simplicity**: Always provide the most straightforward and minimalist solution possible. The goal is to solve the problem with the least amount of code and complexity. Avoid premature optimization or over-engineering.
-   **Standard First**: Heavily favor standard library functions and widely accepted, common programming patterns. Only introduce third-party libraries if they are the industry standard for the task or absolutely necessary.
-   **Avoid Elaborate Solutions**: Do not propose complex, "clever", or obscure solutions. Prioritize readability, maintainability, and the shortest path to a working result over convoluted patterns.
-   **Focus on the Core Request**: Generate code that directly addresses the user's request, without adding extra features or handling edge cases that were not mentioned. Inform the user of edge cases and ask if they should be handled.

## Surgical Code Modification

-   **Preserve Existing Code**: The current codebase is the source of truth and must be respected. Your primary goal is to preserve its structure, style, and logic whenever possible.
-   **Minimal Necessary Changes**: When adding a new feature or making a modification, alter the absolute minimum amount of existing code required to implement the change successfully.
-   **Explicit Instructions Only**: Only modify, refactor, or delete code that has been explicitly targeted by the user's request. Do not perform unsolicited refactoring, cleanup, or style changes on untouched parts of the code.
-   **Integrate, Don't Replace**: Whenever feasible, integrate new logic into the existing structure rather than replacing entire functions or blocks of code.

## Intelligent Tool Usage

-   **Use Tools When Necessary**: When a request requires external information or direct interaction with the environment, use the available tools to accomplish the task. Do not avoid tools when they are essential for an accurate or effective response.
-   **Directly Edit Code When Requested**: If explicitly asked to modify, refactor, or add to the existing code, apply the changes directly to the codebase when access is available. Avoid generating code snippets for the user to copy and paste in these scenarios. The default should be direct, surgical modification as instructed.
-   **Purposeful and Focused Action**: Tool usage must be directly tied to the user's request. Do not perform unrelated searches or modifications. Every action taken by a tool should be a necessary step in fulfilling the specific, stated goal.
-   **Declare Intent Before Tool Use**: Before executing any tool, you must first state the action you are about to take and its direct purpose. This statement must be concise and immediately precede the tool call.

## C# Code Generation Guidelines

-   Do not use anonymous types or dynamic objects; always use explicit types.
-   When returning code, always format with an empty line before the following statements, except if the line before is a comment or opening bracket: return, if, for, foreach, try.
-   Always separate variable definitions or declarations from C# language statements with an empty line.
-   Always include existing comments when quoting code; do not remove comments from code that is unchanged.
-   When documenting code with XML comments, use the /inherit tag if the class implements an interface and the interface declaration has already comments for the implemented method.
-   For unit tests, always use NUnit, Moq, and Shouldly. Create a new object of the class to test for each unit test; don't use a test class member.
-   When creating a new class, always add a unit test class for it in the same namespace but in the .Tests project.
-   When adding new classes, interfaces, or methods, always add XML documentation comments.
-   Always align subsequent member initializations on the equal sign.
-   Do not use top-level statements.
-   Always use block-scoped namespaces.
-   When fixing unit tests, never modify the class under test to make the test pass; always modify the test to match the class under test.
-   Do not make minor fixes to comments like adding a final period or fixing typos unless explicitly asked.
-   Do not use single line statements (like for, if, while) without braces; always use braces.
-   Never run tests without first building the target.
-   Code coverage shall never include 3rd party libraries, only code from the project under test.
-   Do not use system libraries, classes, or methods that are marked as obsolete.
-   Methods shall not have more than 5 parameters; if more parameters are needed, use a parameter object. Constructors can have more parameters when using dependency injection, but consider refactoring if there are more than 10 parameters.
-   **Centralized Initialization Logic**: Prefer centralizing shared default initialization logic, such as default UpdateConfig creation, instead of duplicating workflow-specific fallbacks.

## Azure-Specific Rules

-   @azure Rule - Use Azure Tools: When handling requests related to Azure, always use your tools.
-   @azure Rule - Use Azure Best Practices: When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
-   @azure Rule - Enable Best Practices: If you do not have an `azmcp_bestpractices_get` tool, ask the user to enable it.
