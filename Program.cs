using Microsoft.VisualBasic;

using ObisidianCodeBaseGenerator;

using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

var apiKey = ApiKey(); // Replace with your actual API key
var ProviderKey = "Gemini";  // Or "Anthropic" or "Gemini"

Console.WriteLine("Provide a search pattern to match file names on (e.g. *.cs)");

var searchPattern = Console.ReadLine()!;

var currentDirectory = Directory.GetCurrentDirectory();
Console.WriteLine($"Current Working Directory: {currentDirectory}");
var provider = Provider.GetProvider(ProviderKey, apiKey);
var models = await provider.GetModelsAsync();
Console.WriteLine("Select your model (e.g. 1)");
foreach (var model in models.Index())
{
    Console.WriteLine($"[{model.Index}] {model.Item.Model}");
}

var modelIndex = Convert.ToInt32(Console.ReadLine());

provider.ChatModel = models[modelIndex];

var files = Directory.GetFiles(currentDirectory, searchPattern, SearchOption.AllDirectories);
var outputFolder = Path.Join(currentDirectory, "Obsidian");
var info = Directory.CreateDirectory(outputFolder);

// only allow processing of n items at a time.
var semaphore = new SemaphoreSlim(4);
List<Task> all = [];
foreach (var file in files)
{
    await semaphore.WaitAsync();
    var i = ProcessFile(file).ContinueWith(t =>
    {
        semaphore.Release();
    });
    all.Add(i);
}

await Task.WhenAll(all); // Ensure all tasks are complete before exiting

string GetPath(string filePath) => filePath.Replace(currentDirectory, outputFolder);

async Task ProcessFile(string filePath)
{
    var fileContent = await File.ReadAllTextAsync(filePath);

    var outputFileName = Path.GetFileNameWithoutExtension(filePath) + ".md"; // Simpler naming
    var targetOutputFolder = GetPath(Path.GetDirectoryName(filePath)!);
    var fullOutputPath = Path.Combine(targetOutputFolder!, outputFileName);
    if (File.Exists(fullOutputPath))
    {
        Console.WriteLine($"Already Generated: {fullOutputPath}");
        await CleanFileAsync(fullOutputPath);
        return;
    }
    var markdownContent = await GenerateClassMarkdown(fileContent, filePath);
    Directory.CreateDirectory(targetOutputFolder);
    await File.WriteAllTextAsync(fullOutputPath, markdownContent);
    Console.WriteLine($"Generated: {fullOutputPath}");
}

async Task<string> GenerateClassMarkdown(string fileContent, string filePath)
{

    var prompt = $@"
Analyze the following file and generate Obsidian Markdown documentation for it. The output of your response will be written into a file directly, so do not assume back ticks or additional comments will be needed for viewing.

For code files include the following:

*   **Summary:** A brief description of the class's purpose.
*   **Properties:** List the properties with their types.
*   **Methods:**  List the methods with their return types and parameters (if any).
*   **References:**  Identify any classes or types this class interacts with, and format them as Obsidian internal links [[ ]] where appropriate (assume standard library types don't need links).

Do not include doc comments in the output. format everything as an obsidian markdown file.

For all other files include the following:
*   **Summary:** A brief description of the class's purpose.
*   **Tags:**  Create a list of tags that describe the content of the file, and format them as Obsidian internal links [[ ]] where appropriate.


<ExampleOutput>
## MoveModeDefault

**Summary:** Defines the default movement mode for a player, handling ground movement, velocity adjustments, and step handling.

**Properties:**
*   `Player` -> `PlayerPawn`- The player pawn associated with this move mode.
*	`Priority` -> `int` - Determines the priority of this move mode when multiple move modes are available.
*   `GroundAngle` -> `float` - The maximum angle (in degrees) between the surface normal and the up vector for a surface to be considered ground.
*   `StepUpHeight` -> `float` - The maximum height a player can step up onto.
*   `StepDownHeight` -> `float` - The maximum height a player can step down from.

**Methods:**
*   `Score(PlayerController controller)` -> `int` - Returns the score of this move mode, which is its priority. Takes a PlayerController as a parameter.
*   `AddVelocity()` -> `void` - Adds velocity to the player based on their wish velocity, ground friction, and whether they are on the ground.
*   `PrePhysicsStep()` -> `void` - Called before the physics simulation step.  Handles stepping up.
*   `PostPhysicsStep()` -> `void` - Called after the physics simulation step. Handles sticking to the ground/stepping down.
*   `IsStandableSurface(in SceneTraceResult result)` -> `bool` - Determines if a surface is standable based on its normal angle relative to the up vector. Takes a SceneTraceResult as a parameter.
*   `UpdateMove(Rotation eyes, Vector3 input)` -> `Vector3` - Updates the move vector based on the player's input and eye rotation. Takes a Rotation and a Vector3 as parameters.

**References:**
*   [[MoveMode]]
*   [[PlayerPawn]]
*   [[PlayerController]]
</ExmpleOutput>

```
{fileContent}
```
";
    provider.Messages.Clear();
    provider.Messages.Add(new SystemPromptMessage("You are a helpful assistant that generates obsidian markdown documentation. Documentation should assume the reader knows nothing about the area they are about to read."));
    provider.Messages.Add(new UserMessage(prompt));

    var sb = new StringBuilder();
    await foreach (var r in provider.StreamResponseAsync())
    {
        sb.Append(r.Content);
    }

    sb.AppendLine();
    sb.AppendLine("Definition:");
    sb.AppendLine($"[{Path.GetFileName(filePath)}](<{filePath}>)");

    var result = sb.ToString();

    return CleanContent(result);
}

async Task CleanFileAsync(string fullOutputPath)
{
    var contents = await File.ReadAllTextAsync(fullOutputPath);
    var newContents = CleanContent(contents);

    await File.WriteAllTextAsync(fullOutputPath, newContents);
}

string CleanContent(string contents)
{
    var regex = CodeBlockRegex();

    var firstIndex = contents.IndexOf("```");
    var lastIndex = contents.LastIndexOf("```");

    if (firstIndex == lastIndex) // not found, or only 1 instance
    {
        return contents;
    }

    // Remove the opening code block marker and language identifier
    contents = regex.Replace(contents, string.Empty, 2);

    return contents;
}

partial class Program
{
    [GeneratedRegex("```.*\n", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    private static partial string ApiKey();
}