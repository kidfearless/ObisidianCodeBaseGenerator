# ObisidianCodeBaseGenerator

Obsidian code base generator is a quick and easy tool to generate obsidian vaults for programming code bases, specifically targetted towards c# code bases. This provides graph views of a folders references and quick summaries of what you are working with. Tested with Gemini's  `models/gemini-2.0-flash-lite-preview` model. Confirmed compatibility with openai and gemini through the openai compatibility layer. Anthropic should work but is not tested.

## Setup
1. Clone the repo
2. Implement Program.ApiKey()
3. Publish
4. Add publish directory to PATH Environment Variable.aa
5. run `obsidian-generate.exe` in the target folder and follow the prompts.
6. Using Obsidian open the newly created Obisidian folder as a vault and enjoy.
