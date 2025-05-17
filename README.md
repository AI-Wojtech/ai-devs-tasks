# AI Devs Tasks

## Configuration

Before running the application, make sure to configure your API keys:

1. Open the file `config-example.json`.
2. Remove the word `example` from the filename, renaming it to `config.json`.
3. Replace placeholder values with your actual API keys for the required services.

## Running the Application

To build the application, run:

```bash
dotnet build
````

Then to start the application and see available episodes and commands, use:

```bash
dotnet run
```

You will see a list like this:

```
AVAILABLE EPISODES:
- S01E01 — Interakcja z dużym modelem językowym
- S01E02 — Przygotowanie własnych danych dla modelu
- S01E03 — Limity Dużych Modeli językowych i API
- S01E05 — Produkcja
...
```

## Commands

Run a specific episode:

```bash
dotnet run -- Episode01
```

Display a description of a specific episode:

```bash
dotnet run -- Episode01 --desc
```

## Debugging

You can debug any episode by pressing `F5` and selecting the appropriate episode from the list (e.g. `Episode01`).

```

Teraz możesz bezpiecznie wkleić tę wersję do pliku `README.md`, a Markdown będzie się poprawnie renderować na GitHubie i w edytorach.
```
