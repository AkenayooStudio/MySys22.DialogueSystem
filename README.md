# MySys22.DialogueSystem


**Also known as:** MySys22, MySys, MYSYS22, MYSYS, MySys22 Dialogue System, MYSYS22.DEn, MYSYS22.DEd, DEn, DEd, Akenayō, Akenayo, Akenayoo, Akenayō Ruby, Akenayo Ruby, Akenayo Studio, Akenayō Entertainment & Technology, Akenayo Entertainment & Technology, Ake.
Complete technical documentation of the MySys22.DialogueSystem package: the Unity execution engine
(MYSYS22.DEn) and the standalone authoring editor (MYSYS22.DEd).

| Item | Value |
|---|---|
| Document | 1.0 |
| Engine | 0.5 BETA |
| Editor | 0.5 BETA |
| Contract | `formatVersion` 1 |
| Updated | 2026-09-28 |

Support: support@akenayo.com · Legal: legal@akenayo.com · Website: akenayo.com

---

## 1. Purpose of the documentation

The present set of documents describes **MySys22.DialogueSystem**, a system for the management of
interactive dialogues intended for projects developed with Unity. The documentation is divided into
standalone documents, each of which is intended for a specific category of users and can be
consulted independently of the others.

## 2. Scope of the system

MySys22.DialogueSystem separates the authoring phase of the textual content from the execution phase
of the dialogue within the application product. The separation is implemented by means of two
distinct components that communicate exclusively through a shared file structure.

### 2.1 Components

| Commercial name | Component | Technology | Intended users |
|---|---|---|---|
| **MYSYS22.DEn** | Execution engine running within Unity | C# (Unity 6) | developers, game designers |
| **MYSYS22.DEd** | Standalone authoring editor, distributed as a `jar` with a `lib/` folder | Java 21, JavaFX | writers, translators, narrative designers |

### 2.2 Integration model

The two components share neither code nor compilation dependencies. Data exchange takes place
through the `Assets/StreamingAssets` folder of the Unity project, which constitutes the single
contract of the system: it contains dialogue graphs, the line database, the character registry, the
variable catalogue and the manifest files. The engine reads this structure at runtime; the editor
produces and updates it by means of the deploy operation.

Operational consequence: the authors of the texts can work in parallel with the definition of the
graph, and neither role must have access to the tools of the other.

## 3. Technical characteristics

| Characteristic | Description |
|---|---|
| Authoring independence | the editor is a standalone application; the drafting of texts requires neither opening Unity nor compiling the project |
| Single contract | paths, formats and denominations are defined in a single structure shared between editor and engine |
| Separation between path and text | the graph contains exclusively numeric `DID` references; the line database contains exclusively text |
| Identifier stability | identifiers already referenced by the graph cannot be deleted; new lines occupy the free intervals |
| Localization | each language corresponds to a folder of normalized files with an index; the language can also be changed at runtime |
| Application actions | the operations required by the game are implemented as C# actions with parameters, wait and boolean result |
| Platforms | single content for Windows, Linux, macOS, Android, iOS and WebGL; asynchronous reading by means of indices on platforms without direct file system access; IL2CPP support |
| Preliminary checks | project validation, identifier coverage and graph digests, also available from the command line |
| Editor interface | English, Italian, Spanish, Romanian, Japanese |
| Extension | typed variables, expressions evaluated by an interpreter without code execution, quest system, audio management per node and per line, public API |

## 4. Organization of the documentation

Every document is self-contained: only the required document is consulted.

| Document | Recipients | Content |
|---|---|---|
| [General guide](01-GUIDA-GENERALE.md) | all personnel involved | scope of the system, requirements, installation, initial configuration, data structure, complete reference of the interfaces |
| [Technical documentation](02-TECNICO-MOTORE-EDITOR.md) | technical game designers, integrators, maintainers | architecture, file formats, node types, execution cycle, actions, conditions, platforms, build process, error codes |
| [Developer API](03-DEVELOPER-API.md) | C# programmers | public API, custom actions, variables, quests, audio, position and save |

Each document exists in two forms: the Markdown file, which is the source, and the static site page
generated from it.

| Form | Path |
|---|---|
| Source | `01-GUIDA-GENERALE.md`, `02-TECNICO-MOTORE-EDITOR.md`, `03-DEVELOPER-API.md` |
| Site (Italian) | `site/index.html` |
| Site (English) | `site/index.en.html` |

Translations of the documents are stored in `i18n/<language code>/` with identical file names.

## 5. Glossary

| Term | Definition |
|---|---|
| **DID** | numeric identifier of a line within the file of a character; it constitutes the key shared between graph and line database |
| **CID** | numeric identifier of a character (for example `0` for Anna); the displayed name is defined in `characters.list.yaml` |
| **Graph** | file `*.graph.yaml` containing nodes and connections that define a dialogue |
| **Contract** | file structure internal to `Assets/StreamingAssets` comprising graphs, lines, characters, variables and manifest; it represents the single point of contact between editor and engine |
| **Digest** | file `engine.graphdigest.json`, produced by the engine, which lists the identifiers used by the game |
| **Function** | node that calls application code and waits for a result |
| **manifest.txt** | index of the files, employed on platforms where it is not possible to enumerate the content of a folder |

---

## 1. Overview

MySys22.DialogueSystem consists of two distinct programs that operate on the same data structure:
the execution engine, integrated in Unity, and the authoring editor, a standalone application
intended for the drafting of texts. The present guide describes installation, initial
configuration, the data structure and the complete reference of the two interfaces.

### 1.1 Functional architecture

- The **engine** (`MYSYS22.DEn`) resides in the Unity project, interprets the dialogue graphs,
  presents the lines and calls the actions required by the game logic.
- The **editor** (`MYSYS22.DEd`) operates on an independent writing project, produces the line
  databases and transfers the content to the Unity project by means of the deploy operation.
- The **data exchange** takes place exclusively through the `Assets/StreamingAssets` folder, which
  constitutes the contract of the system.

### 1.2 Operational roles

| Role | Tool | Responsibility |
|---|---|---|
| Writer, translator | Java editor | drafting and revision of the lines, management of characters and variables, deploy of the content |
| Game designer | Unity | definition of the graph: sequence of the lines, choices, functions, conditions, jumps between graphs |
| Developer | Unity, C# code | implementation of the actions required by the graph, management of events, variables, quests, audio and position |

### 1.3 Reasons for the separation into two components

The separation allows the drafting of texts while the graph is being modified, without requiring
either role to learn the tool of the other. The graph contains no text and the line database
contains no path references: the two activities proceed independently and meet solely through the
identifiers of the lines.

## 2. Requirements and installation

### 2.1 Requirements

| Component | Requirements |
|---|---|
| Engine | Unity 6 (version 6000.0.x), TextMeshPro, Input System |
| Editor | Java 21; for compilation from sources, Maven |

The engine is compatible with the classic input manager, with the new Input System and with the
simultaneous activation of both.

### 2.2 Installation of the engine (MYSYS22.DEn)

1. Copy the `Assets/MySys22.DialogueEngine` folder into the Unity project.
2. Select the menu `MySys22 ▸ Dialogue ▸ Setup Scene` to create and connect the dialogue interface
   in the current scene.
3. The data structure in `Assets/StreamingAssets` is created automatically on first use.

### 2.3 Installation of the editor (MYSYS22.DEd)

Two execution modes are provided:

| Mode | Procedure | Intended users |
|---|---|---|
| Portable distribution | run `run.sh` (Linux, macOS) or `run.bat` (Windows) from the folder containing `mysys22-dialogue-editor.jar` and the `lib/` folder | end users |
| Compilation from sources | run `MySys22.DialogueEditor/run.sh`, which performs the compilation by means of Maven | developers |

The editor starts on the initial screen with the list of recent projects; it does not require the
installation of Unity.

## 3. Initial configuration procedure

The following procedure produces a working dialogue consisting of a line associated with a
character.

1. In Unity, open the window `MySys22 ▸ Dialogue ▸ Dialogue Graph Editor`.
2. On the canvas, select with the right button `Add Dialogue Node` and specify the fields `Speaker`
   (identifier of the character), `Start DID` and `End DID`.
3. Select the node, open the context menu and choose `Set as Start Node`; the node concerned is
   highlighted with a green border.
4. Save the graph with `File ▸ Save`. The file is written to `Assets/StreamingAssets/Graphs`.
5. In the Java editor, select `New Project` and specify name, language and path of the Unity
   project.
6. Open the lines tab, insert the rows with identifier `LID` corresponding to the `DID` defined in
   the graph, then draft the texts.
7. Select the button `Inject` to transfer the content to the Unity project.
8. In Unity, add the `GameSetup` component to the scene, or use `Setup Scene`, and start playback:
   the dialogue is presented on screen.

If `[317] No dialogue graph found` is signalled in the Console, the configured graph has not been
located: set the field `Graph` of the `GameSetup` component to the desired graph and verify that
the corresponding file is present in `Assets/StreamingAssets/Graphs`.

## 4. Data structure on disk

### 4.1 Runtime contract (Unity project)

The engine reads the dialogue data from the `Assets/StreamingAssets` folder:

```
Assets/StreamingAssets/
├── engine.manifest.json                  project descriptor
├── engine.graphdigest.json               list of the DID used by the game
├── Graphs/
│   ├── <grafo>.graph.yaml                dialogue graphs
│   └── manifest.txt                      index of the graphs
├── Dialogue/
│   └── <LINGUA>/
│       ├── <CID>.yaml                    lines of a character
│       └── manifest.txt                  index of the line files
├── Characters/
│   └── characters.list.yaml              identifiers and names of the characters
└── Variables/
    └── variables.yaml                    variables used by the conditions
```

### 4.2 Writing project (Java editor)

The editor stores the content in a dedicated project folder, defined at creation time. The
structure comprises the file `.project.yaml`, the databases `lines/<LINGUA>/<CID>.yaml`, the folders
`Characters` and `variables`, and the digest imported from the engine.

The deploy operation copies and normalizes such content into the `Assets/StreamingAssets` folder,
without requiring the intervention of Unity.

## 5. Unity interface reference

### 5.1 Main menu (MySys22 ▸ Dialogue)

| Menu item | Description |
|---|---|
| `Dialogue Graph Editor` | opens the graph definition window |
| `Setup Scene` | creates or restores the dialogue interface in the current scene (canvas, texts, choice panel) and connects the references |
| `Auto Setup Scene On/Off` | enables or disables the automatic configuration of the scene on opening |
| `Validate Project` | verifies graphs, identifiers, actions and choices; produces a report in the Console with the error codes |
| `Export Graph Digest` | generates `engine.graphdigest.json`, containing the identifiers used by the game |
| `Reveal Graph Digest` | shows the digest file in the file manager of the operating system |
| `Generate Index Manifests` | regenerates `Graphs/manifest.txt` and all the files `Dialogue/<LINGUA>/manifest.txt`, required on Android, iOS and WebGL |
| `Generate Manifest for Current Language` | regenerates the index of the current language only |
| `Generate AOT link.xml` | generates the file `link.xml`, which prevents IL2CPP from eliminating the classes used for reflection (actions, YAML data types) |
| `Open Engine Folder` | opens `Assets/StreamingAssets` in the file manager |
| `Open Project Folder` | opens the folder of the Unity project |
| `Open Dialogue Editor (Java)` | starts the Java editor |
| `Editor Settings...` | configures the path of the Java editor (folder, Maven executable, `jar` archive) |
| `Engine Status` | displays a summary: manifest, languages, graphs, loaded rows and active provider |

### 5.2 Inspector panel

The project section is present on the inspectors of the `GameSetup` and `DialogueBridge` components
and comprises the following buttons.

| Button | Description |
|---|---|
| `Refresh` | re-reads manifest, languages and graphs without reloading the dialogue |
| `Validate` | performs the same verification as `Validate Project`, limited to the selected component |
| `Export Digest` | exports the digest of the graphs used |
| `Graph Editor` | opens the graph definition window |
| `Open StreamingAssets` | opens the contract folder |
| `Open Dialogue Editor (Java)` | starts the Java editor |
| `Editor Settings` | opens the settings of the Java editor |

Configuration parameters:

| Field | Description |
|---|---|
| `Language` | `Project default` or a specific language; it does not overwrite the preference stored by the player |
| `Graph` | `First graph found` or a specific graph |
| `Auto start dialogue` (`GameSetup`) | enables the automatic start of the dialogue at scene start |
| `Create overlay fallback` (`GameSetup`) | in the absence of a connected interface, creates a minimal one, ensuring the presentation of the dialogue |
| `Enable logs` (`GameSetup`) | enables or disables the log messages with prefix `[MySys22.DialogueEngine]` |

### 5.3 Dialogue Graph Editor window

| Command | Position | Description |
|---|---|---|
| `File ▸ New` | command bar | creates an empty graph |
| `File ▸ Load` | command bar | opens a file `*.graph.yaml` |
| `File ▸ Save` | command bar | saves the current graph and rewrites the graph index |
| `File ▸ Save As...` | command bar | saves the graph under a different name |
| `Add Dialogue Node` | context menu of the canvas | inserts a line node, with the fields `Speaker`, `Start DID`, `End DID`, audio and automatic actions |
| `Add Choice Node` | context menu of the canvas | inserts a choice node, with one output for each option (label, text identifier, condition) |
| `Add Function Node` | context menu of the canvas | inserts a node that calls a game action (`Action`, parameters, wait) |
| `Add Condition Node` | context menu of the canvas | inserts a node with two outputs, `True` and `False` |
| `Add Jump Node` | context menu of the canvas | inserts a jump node towards another graph or a return to the calling graph |
| `Add End Node` | context menu of the canvas | inserts a dialogue closing node |
| `Set as Start Node` | context menu of a node | defines the initial node of the dialogue, highlighted with a green border |
| `Clear Start Node` | context menu of the canvas | removes the designation of the initial node |

### 5.4 Node types

| Type | Function | Main fields |
|---|---|---|
| **Dialogue** | presents a line | `Speaker` (CID), `Start DID`, `End DID`, audio, repetition, voice per line, parallel and completion actions |
| **Choice** | presents a set of options | list of the options: label, identifier of the localized text, condition; one output per option |
| **Function** | calls application code and waits for its outcome | `Action`, parameters, wait for completion |
| **Condition** | routes the outcome of a function | two outputs, `True` and `False`, connectable to any node |
| **Jump** | transfers the execution to another graph | destination graph, return to the calling graph |
| **End** | concludes the dialogue | no field |

## 6. Java editor interface reference

### 6.1 Initial screen

| Element | Description |
|---|---|
| `New Project` | creates a project: name, language, version and chapter, then local path and folder of the Unity project |
| `Open Project` | opens an existing project from the file system, from SFTP or from a Git repository |
| Recent project card | reopens the corresponding project |
| Context menu ▸ `Open` | equivalent to opening from the card |
| Context menu ▸ `Edit name` | modifies the project name and updates the manifest |
| Context menu ▸ `Copy path` | copies the project path to the clipboard |
| Context menu ▸ `Remove from list` | removes the project from the recent list without deleting any data |
| Context menu ▸ `Local delete` | deletes the project folder, subject to confirmation |
| Information icon | opens the settings panel, including the interface language selector |

The languages available for the interface are: English, Italian, Spanish, Romanian and Japanese.

### 6.2 Opening a project

| Tab | Description |
|---|---|
| `Local` | selection of a folder containing a file `.project.yaml`; the import starts with `Import` |
| `SFTP` | download of the project from a remote server: host, port, user, SSH key or password and local path |
| `Git` | cloning of the project: repository URL, branch and local path |

### 6.3 Editing window

Title bar:

| Element | Description |
|---|---|
| `File ▸ Save`, `Save All` | not active: saving is automatic on every modification |
| `File ▸ Exit` | closes the editor |
| `Settings` | opens the information and settings panel |
| `Window ▸ Reset Layout` | not active |
| `Inject` | performs the deploy towards the Unity project and presents a summary: languages, files, rows, destination path and reports |

`Project Manager` panel (on the left): tree structure of the project files.

| Context menu item | Description |
|---|---|
| `New File` | creates a file, adding the extension `.yaml` if absent |
| `New Folder` | creates a folder |
| `Delete` | deletes the selected element; the protected files `.project.yaml` and `.registry.log` cannot be deleted |
| Double click on a file | opens the corresponding tab |

Side icons, which open the lower panels:

| Icon | Description |
|---|---|
| `Git` | Git console, available if Git management is active in the project |
| `SFTP` | SFTP synchronization panel |
| `Debug` | diagnostics panel, including the command `Import Digest` |
| `Console` | system console of the project (bash or PowerShell) |

### 6.4 Work tabs

Lines tab, identified as `‹CID› [LINGUA]`:

| Element | Description |
|---|---|
| Field `CID` | modifies the character associated with the file; if the identifiers are already used by the game, confirmation is requested |
| Column `LID` | identifier of the line; not modifiable |
| Column `Line` | text of the line; editing is activated with a double click |
| `Insert` | inserts a line between two existing identifiers, using a free interval; if no space is available, a report is emitted |
| `+` | adds a line with the next free identifier |
| `−` | deletes the selected line; the operation is rejected if the identifier is referenced by the game |
| `↶`, `↷` | undoes and restores the last operation; equivalent to `Ctrl+Z`, `Ctrl+Y` and `Ctrl+Shift+Z` |

The tab furthermore indicates the list `Referenced DIDs`, that is the identifiers actually used by
the graph, obtained from the digest.

Tab `characters.list`:

| Element | Description |
|---|---|
| Columns `CID`, `Name`, `Language` | character registry; the field `Name` defines the name displayed in the dialogue |
| `+`, `−` | adds or removes a character |
| `↶`, `↷` | undoes and restores the last operation |

Tab `variables`:

| Element | Description |
|---|---|
| Columns `Name`, `Type`, `Default`, `Description` | variables used by the conditions of the graph; the admitted types are `bool`, `int`, `float` and `string` |
| `+`, `−` | adds or removes a variable |
| `↶`, `↷` | undoes and restores the last operation |

Tab `.project.yaml`:

| Element | Description |
|---|---|
| Name, dialogue version, chapter | identifying data of the project, also used in the reports |
| Default language | start language of the dialogue |
| Languages | comma-separated list; codes not included among the interface languages are admitted |
| Path of the Unity project | destination of the deploy operation |
| SFTP section | connection parameters to the remote server |
| `Save` | saves the project manifest |
| `Create shortcut...` | creates a file `.mysys22` that allows the project to be opened with a double click |

The tab `.registry.log` presents the change log in read-only mode.

`Debug` panel:

| Element | Description |
|---|---|
| `Import Digest` | imports the file `engine.graphdigest.json` exported by Unity |
| `↻` | re-runs the verifications on the project and populates the table with information, warnings and errors |

## 7. Workflow

1. The game designer defines or updates the graphs in Unity, assigning the identifiers to the
   lines.
2. The game designer runs `Export Graph Digest` and transfers the digest to the text writer.
3. The writer imports the digest into the Java editor by means of `Debug ▸ Import Digest`; the lines
   tab signals the identifiers not yet documented.
4. The writer produces the texts, performs the translations and updates characters and variables.
5. The writer runs `Inject`: the editor validates the project, writes the data to
   `Assets/StreamingAssets` and removes the files no longer belonging to the project.
6. The developer starts the scene in Unity. Before a build intended for mobile platforms, the
   developer runs `Generate Index Manifests`.

The correct operation of the system depends on the correspondence between the identifiers of the
lines present in the graph and those defined in the textual databases. As long as such
correspondence is maintained, the content of the texts can be modified without intervening on the
graph.

---

This document is intended for those who integrate, maintain or extend the system. It describes the
on-disk contract, the runtime behaviour of the engine, the Java editor and the build rules.

---

## 1. Architecture overview

| Component | Location | Technology | Responsibility |
|---|---|---|---|
| **MYSYS22.DEn** — engine | `Assets/MySys22.DialogueEngine/` | C# (Unity 6) | reads the contract, executes graphs, exposes API/events |
| **MYSYS22.DEd** — editor | `MySys22.DialogueEditor/` | Java 21 + JavaFX | writes lines/characters/variables, validates, performs deployment |

The system is structured into the following Unity assemblies:

| Assembly | Root namespace | Content |
|---|---|---|
| `MySys22.DialogueEngine.Runtime` | `MySys22.DialogueEngine` | `Core`, `Bridge`, `UI`, `Audio`, `API` |
| `MySys22.DialogueEngine.Editor` | `MySys22.DialogueEngine.Editor` | graph window, node view, inspector, validation, digest, AOT, scene setup |

The runtime depends on `Unity.TextMeshPro`, `Unity.InputSystem`, `YamlDotNet`, `Unity.Burst`,
`Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`. The SIMD code is optional and always has a
scalar fallback.

**Project rule**: no game logic is contained within the engine. The engine determines *when* a
function must start and *what* must be done with the result; the manner in which a door is opened is
instead determined by the game.

---

## 2. The on-disk contract

The root is unique: `Assets/StreamingAssets/` (no wrapper folder).

| File / folder | Written by | Read by | Notes |
|---|---|---|---|
| `engine.manifest.json` | Java editor | engine | project descriptor |
| `Graphs/<id>.graph.yaml` | Graph Editor (Unity) or deployment | engine | graphs |
| `Graphs/manifest.txt` | Unity menu / deployment | engine (mobile) | graph index |
| `Dialogue/<LINGUA>/<CID>.yaml` | deployment | engine | line database |
| `Dialogue/<LINGUA>/manifest.txt` | Unity menu / deployment | engine (mobile) | line index |
| `Characters/characters.list.yaml` | deployment | engine | CID → displayed name |
| `Variables/variables.yaml` | deployment | engine | variable declaration |
| `engine.graphdigest.json` | engine (Unity menu) | Java editor | inventory for the writer |

The ownership of the data is partitioned as follows:

- The **path** of the dialogue (nodes, choices, functions) resides in the graphs and is therefore the
  responsibility of the game designer.
- The **text** resides in the line database and is therefore the responsibility of the writer.
- The two domains communicate by means of the **DID**: the graph never contains text, but only numbers.

The legacy paths (`Assets/StreamingAssets/MySys22.DialogueEngine/…`, `MySys22.DialogueAssets/…`) are
recognised and normalised by the engine; the Java deployment migrates the old folder if it detects
it.

---

## 3. Format model

### 3.1 `engine.manifest.json`

```json
{
  "formatVersion": 1,
  "projectId": "test",
  "name": "test",
  "dialogueVersion": "1.0",
  "chapter": "1",
  "defaultLanguage": "EN",
  "languages": ["EN"],
  "graphs": 0,
  "variables": 0,
  "generatedBy": "MySys22 Dialogue Editor 0.1.0",
  "generatedAt": "2026-09-24T12:18:37Z"
}
```

On mobile the file constitutes the source of truth for the language list, since it is not possible to
enumerate folders.

### 3.2 `Graphs/<id>.graph.yaml`

```yaml
graphId: test_new_graph
startNode: 2de7e841
nodes:
- id: 2de7e841
  type: dialogue
  speaker: 0
  start_did: 1
  end_did: 2
  params: {}
  next: 6cb687b5
  choices: []
  position: { x: 351, y: -62 }
  isStart: true
```

The following table lists the fields of a node (`NodeData`):

| Field | Type | Used by | Meaning |
|---|---|---|---|
| `id` | string | all | node identifier (8 characters) |
| `type` | string | all | `dialogue`, `choice`, `function`, `condition`, `jump`, `end` |
| `speaker` | string | dialogue, choice | CID of the character |
| `start_did` / `end_did` | int | dialogue | line range of the node (inclusive) |
| `next` | string | all | next node |
| `action` | string | function | id of the action to execute |
| `params` | map | function | parameters passed to the action |
| `wait` | bool | function | if `true` the dialogue waits for the end of the function |
| `conditions` | list | function | `condition` nodes to which the result is passed |
| `condition` | string | condition, dialogue, choice | boolean expression |
| `else` | string | condition | alternative branch |
| `on_true` / `on_false` | string | condition | destination node for the two outcomes |
| `option` | string | condition | free label of the branch |
| `graph` | string | jump | destination graph |
| `return` | bool | jump | if `true` returns to the calling graph at the end of the branch |
| `choices` | list | choice | options described in the following subsection |
| `audio` / `audio_loop` | string/bool | dialogue | ambient track of the node |
| `voice` | bool | dialogue | one clip for each DID of the line |
| `on_parallel` | string | dialogue | action started while the line is on screen |
| `on_complete` | string | dialogue | action executed when the line has been read |
| `position` | x/y | editor | position of the node in the canvas |
| `isStart` | bool | editor | highlights the start node |

The following table lists the fields of a choice option (`ChoiceData`):

| Field | Meaning |
|---|---|
| `text` | working label (fallback if `text_did` is 0) |
| `text_did` | DID from which the localised text of the choice is read |
| `next` | destination node |
| `portGuid` | port identifier (used by the editor) |
| `onChosen` | action executed when the option is chosen |
| `condition` | if false, the option is not shown |

### 3.3 `Dialogue/<LINGUA>/<CID>.yaml`

```yaml
character: '0'
language: EN
lines:
- did: 1
  text: Ciao mi chiamo anna
- did: 2
  text: Come stai?
```

The value `did` cannot be repeated within the same file; the DIDs need not be consecutive, since gaps
are deliberately available for future insertions.

### 3.4 `Characters/characters.list.yaml`

```yaml
characters:
- id: 0
  name: Anna
  language: EN
```

The field `id` is the CID used by the graphs; the field `name` is a UI label **only**.

### 3.5 `Variables/variables.yaml`

```yaml
variables:
- name: has_key
  type: bool
  value: 'false'
  description: The player has the key
```

The permitted types are `bool`, `int`, `float`, `string`.

### 3.6 `manifest.txt` (indices)

| File | Content | Consumer |
|---|---|---|
| `Graphs/manifest.txt` | one file name per line (`test_new_graph.yaml`) | `DialogueGraphIndex` |
| `Dialogue/<LINGUA>/manifest.txt` | one CID per line (`0`) | `YamlLineProvider` |

Lines starting with `#` are comments. On desktop the indices are ignored, since the engine enumerates
folders.

### 3.7 `engine.graphdigest.json`

```json
{
  "formatVersion": 1,
  "generatedBy": "MySys22.DialogueEngine 1.0",
  "languages": ["EN"],
  "characters": [{ "cid": "0", "name": "Anna", "language": "EN" }],
  "graphs": [
    {
      "graphId": "test_new_graph",
      "file": "test_new_graph.yaml",
      "startNode": "2de7e841",
      "nodes": [
        { "id": "2de7e841", "type": "dialogue", "speaker": "0",
          "startDid": 1, "endDid": 2, "next": "6cb687b5", "choices": [] }
      ]
    }
  ]
}
```

The file is intended for the writer, who can thereby know which DIDs are actually used. The Java
editor imports it (`Debug ▸ Import Digest`), while the `coverage` command verifies that every
referenced DID has a text available.

---

## 4. Data semantics

### 4.1 DID and CID

- **CID**: numeric string of the character, defined in `characters.list.yaml`.
- **DID**: integer of the line within the file of that character.
- The `dialogue` node with `start_did: 5` and `end_did: 7` shows lines 5, 6, 7 in sequence.
- A missing DID produces `[MISSING: <CID> - DID <n>]` on screen and a warning in the Console: the
  dialogue does not block.
- **Stable DIDs**: the `Insert` command in the editor looks for a free gap among the neighbouring
  DIDs and never reuses a number already referenced by the game; a referenced DID cannot be deleted.
  It follows that the graph continues to work even after months of editing.

### 4.2 Jump between graphs

- The `jump` node loads another graph (`LoadGraphByName`, from the in-memory cache on mobile) and
  proceeds from there.
- With `return: true`, at the end of the destination graph the dialogue returns to the calling graph,
  by means of the jump stack in the player.
- If the destination graph does not exist, the engine logs `[319]` and terminates the dialogue.

### 4.3 Audio

- The field `audio` on the Dialogue node starts a track (`DialogueAudio.Play`), while `audio_loop`
  repeats it.
- The field `voice: true` determines the search for a clip for each DID (`PlayVoice(cid, did)`).
- The clips are resolved from a registry (`DialogueAudio.RegisterAudioClip`) or from `Resources`.
- The audio can also be commanded from the API (`Dialogue.PlayAudio`, `StopAudio`, `PlayVoice`).

### 4.4 Position and saving

The engine does **not** save games: it exposes a snapshot of the position and leaves its management to
the game.

```csharp
Dialogue.Position            // current position
Dialogue.SavePosition()      // string to be saved at the required location
Dialogue.RestorePosition(s)  // reads the string back
Dialogue.OnPositionChanged   // event
Dialogue.SetFunctionProgress("step 3/7")   // progress within a long-running function
```

The fields of `DialoguePosition` are: `GraphId`, `NodeId`, `NodeType`, `Speaker`, `LineIndex`, `Did`,
`LineCount`, `WaitingForChoice`, `WaitingForAction`, `FunctionActionId`, `FunctionProgress`,
`Language`, `Running`.

It falls within the responsibility of the game to save the string and, on restore, to bring the graph
back to the desired state. See [API for developers](03-DEVELOPER-API.md#10-position-and-save).

---

## 5. Engine execution cycle

| Class | Role |
|---|---|
| `DialoguePaths` | all positions of the contract; no other file may construct paths |
| `DialogueStreamingAssets` | per-platform I/O (direct file or `UnityWebRequest`) |
| `DialogueProjectLoader` | prepares the project and loads the graph |
| `DialogueProjectManifestLoader` | reads `engine.manifest.json` |
| `LanguageManager` | current language, available languages, saved preference |
| `YamlLineProvider` | in-memory line database (`GetLine(cid, did)`) |
| `CharacterRegistry` | CID → displayed name |
| `DialogueVariableCatalog` | variable declarations |
| `DialogueGraphIndex` | graph index + in-memory graphs (used by `jump` nodes) |
| `GraphYamlParser` | YAML ↔ `GraphData` |
| `DialogueGraphPlayer` | executes the graph: nodes, choices, waits, functions, audio |
| `ActionRegistry` | discovery and instantiation of the actions |
| `DialogueBridge` (MonoBehaviour) | facade: events, language, variables, quests, position |
| `Dialogue` (static) | convenient API for the game |
| `DialogueUICanvas` / `DialogueOverlayUI` | presentation (`IDialogueView`) |
| `DialogueProjectValidator` | graph/line consistency checks |

The startup sequence, valid on both desktop and mobile, is the following:

1. `GameSetup`/`DialogueBridge.Awake` starts initialisation (asynchronous on mobile, synchronous on
   desktop).
2. `DialogueProjectLoader` reads manifest → languages → characters → variables → graph index → graph.
3. `YamlLineProvider` loads the lines of the selected language.
4. The bridge connects the variables (with `QuestVariableMirror`) and creates the player.
5. `StartDialogue()` starts from the `startNode` node; `Update()` calls `Tick()` every frame.

The call to `StartDialogue()` performed before initialisation has finished **is not lost**: it is
deferred (`_startWhenReady`).

### 5.1 `DialogueGraphPlayer` — states

| State | Meaning |
|---|---|
| `Running` | is showing a node or advancing |
| `WaitingForChoice` | waits for `Choose(index)` |
| `WaitingForAction` | waits for the end of a function (with safety timeout) |
| stopped | dialogue terminated (`End` or finished graph) |

The following rules apply:

- `Tick()` advances the typewriter and the waits; it is not necessary on desktop only because the
  events arrive from the bridge, but it is mandatory if the game drives the player on its own.
- On desktop the action is executed on the Unity main thread; the internal waits use
  `ConfigureAwait(false)` so as not to block `Tick()`.
- `StopDialogue`, language change, graph change and bridge destruction call `Cancel()` on the actions
  in progress.

---

## 6. Management of functions and actions

The interface contract is the following:

```csharp
public interface IAction
{
    void Execute(DialogueActionContext context);
    Task ExecuteAsync(DialogueActionContext context, CancellationToken cancellationToken);
    void Cancel();
}
```

With regard to automatic discovery: a class decorated with `[DialogueAction("NomeAzione")]` is found
at startup and its name appears in the dropdown of the Function node. **MonoBehaviours are
rejected** with a warning: the actions must be pure classes, since they are instantiated by the
engine.

The `DialogueActionContext` object exposes: `ActionId`, `Node`, `Graph`, `Speaker`, `Language`,
`Parameters` (with `GetParameter`, `GetInt`, `GetFloat`, `GetBool`), `Player`, `Variables`, `Quests`,
`Completion`, `Result`, plus the commands `SetResult(bool)`, `SetNeutral()`, `Complete()`.

| Concept | Behaviour |
|---|---|
| `Wait for completion` on the node | the dialogue stops until the function closes |
| `ctx.SetResult(true/false)` | routes the dialogue onto the True/False outputs of the connected Condition nodes |
| `ctx.CompleteNeutral()` | neutral result: the normal output is taken, the conditions are ignored (with a warning if any exist) |
| `CompletesExternally = true` | the function remains open until the game calls `Dialogue.Complete(...)` |
| `FunctionTimeoutSeconds` | if nobody closes within the time, the engine closes by itself and proceeds |
| Exception in the action | log `[305]`, the function closes in neutral mode, the dialogue continues |
| `On Parallel` (Dialogue node) | action started in parallel while the line is on screen |
| `On Complete` (Dialogue node) | action executed when the line has been read |

The built-in actions are the following:

| Id | Parameters | What it does |
|---|---|---|
| `LogDebug` | `message` | writes to the Console |
| `Wait` | `seconds` | waits N seconds (`WaitsForCompletion`) |
| `QuestAccept` | `questId` | accepts a quest |
| `QuestComplete` | `questId` | completes a quest |
| `QuestFail` | `questId` | fails a quest |
| `QuestStep` | `questId`, `stepId`, `done` | marks an objective |
| `ReportResult` | `result` | closes the function with true/false |
| `ExternalResult` | — | closes nothing: waits for the game |

---

## 7. Condition system

A Condition node connected to the output of a Function receives the result of the function and routes
onto `on_true` / `on_false`. A Condition node with a `condition` field is instead evaluated directly.

The supported grammar is the following (no `eval`, no access to code):

| Element | Examples |
|---|---|
| Comparison operators | `==`, `!=`, `<`, `<=`, `>`, `>=` |
| Logical operators | `&&`, `\|\|`, `!` |
| Parentheses | `(a \|\| b) && c` |
| Literals | `true`, `false`, integer and decimal numbers, strings in quotes |
| Identifiers | also dotted: `quest.rescue.state` |
| Unary minus | `-1` |

Arithmetic is **not** supported: for counters, `AddInt` from the API or the quest actions are used.

The quest variables are exposed automatically (`QuestVariableMirror`):

```
quest.<id>.state        available | active | completed | failed
quest.<id>.available    bool
quest.<id>.active       bool
quest.<id>.completed    bool
quest.<id>.failed       bool
quest.<id>.step.<stepId>  bool
```

An invalid expression is logged with `[324]` and considered false.

---

## 8. Variables and quests

- The types are `bool`, `int`, `float`, `string`; `DialogueVariables` is a typed dictionary with the
  `OnChanged` event.
- The declarations in `variables.yaml` populate the catalog (`DialogueVariableCatalog`), used for the
  validation and for the editor.
- Conditions on undeclared variables are permitted (with a warning in the validator).
- `IQuestService` is the abstraction of the quests; `InMemoryQuestService` is the default
  implementation. The game may supply its own (`SetQuestService`).
- `QuestVariableMirror` mirrors the quest state inside the variables, so that the conditions can read
  it without code.

---

## 9. Cross-platform support

The `DialogueStreamingAssets` class is divided by compilation:

```csharp
#if UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
    private const bool StreamingPlatform = true;
#else
    private const bool StreamingPlatform = false;
#endif

public static bool RequiresAsync => ForceAsync || (!Application.isEditor && StreamingPlatform);
```

| Platform | File access | Notes |
|---|---|---|
| Windows / Linux / macOS standalone | direct `File` / `Directory` | the `manifest.txt` files are ignored |
| Android / iOS | `UnityWebRequest` with URL `jar:file://…!/assets/…` | mandatory indices, asynchronous init |
| WebGL | `UnityWebRequest` with http URL | mandatory indices, no writing to disk |

Since the constant is declared `const`, the compiler eliminates the useless branch: a desktop build
does not contain the `jar:`/`http:` code, while a mobile build does not contain the direct I/O.
`Application.isEditor` keeps the play mode in Unity on the direct access path even with an Android
build target.

The following consequences arise:

- on mobile folders **cannot** be enumerated: everything passes through the indices;
- `DialoguePaths.EnsureLayout()` does not create folders when the platform is not writable;
- the restoration of the dialogue in mid-course (`RestorePosition`) must be managed by the game.

---

## 10. Java editor

### 10.1 Project structure (user folder)

| Path | Content |
|---|---|
| `.project.yaml` | project manifest (name, languages, Unity path, SFTP) |
| `.registry.log` | change registry |
| `lines/<LINGUA>/<CID>.yaml` | lines |
| `Characters/characters.list.yaml` | characters |
| `variables/variables.yaml` | variables |
| `graphs/` | graphs (optional: they normally reside in Unity) |
| `.engine/graphdigest.json` | digest imported from the engine |

The state files of the editor, located outside the project (`~/.mysys22-dialogue-editor/`), are:
`settings.yaml` (UI language), `sessions/<projectId>.json` (open tabs), `recent-projects.json` (max 20
recent). The `.mysys22` shortcuts contain a `project=<percorso>` line.

### 10.2 Deployment

The service `UnityDeployService.deploy(session, unityRoot)` performs the following steps:

1. validates the project (`validateForDeploy`): if errors exist, **it aborts without touching Unity**;
2. migrates the old `MySys22.DialogueEngine` folder if present;
3. writes `Dialogue/<LINGUA>/<CID>.yaml` normalising `character` and sorting the DIDs, plus
   `manifest.txt` per language;
4. removes the `.yaml` files no longer present and the stale language folders (with their `.meta`);
5. writes `Characters/characters.list.yaml` and `Variables/variables.yaml` (empty if absent);
6. copies `graphs/*.yaml` **only if** the project has a `graphs/` folder, then rewrites
   `Graphs/manifest.txt` from what it actually finds;
7. writes `engine.manifest.json` and realigns the languages in the `.project.yaml`.

The same operation is available from the **Inject** button (GUI) and from the `deploy` command (CLI).

### 10.3 Tabs and panels

| Tab | File | Notes |
|---|---|---|
| `‹CID› [LINGUA]` | `LinesEditorTab` | LID/Text table, `Insert`, `+`, `−`, undo/redo |
| `characters.list` | `CharacterListTab` | CID/Name/Language table |
| `variables` | `VariableListTab` | Name/Type/Default/Description table |
| `.project.yaml` | `ProjectConfigTab` | configuration + `Save` + `Create shortcut...` |
| `.registry.log` | `RegistryLogTab` | read-only |

The bottom panels are: `Console` (project shell), `Debug` (Import Digest + refresh), `SFTP`
(connect/download), `Git` (`git` commands only). The undo/redo functions are associated with the
combinations `Ctrl+Z`, `Ctrl+Y`, `Ctrl+Shift+Z` and the history comprises 200 steps.

### 10.4 CLI (headless)

```
MySys22 Dialogue Editor - headless tool

  validate --project <dir>
  coverage --project <dir>
  deploy   --project <dir> --unity <unity-project-root>
```

The exit codes of each command are reported in the following table:

| Command | Exit code |
|---|---|
| `validate` | 0 if no errors, 1 if errors exist |
| `coverage` | 0 if no referenced DID is missing, 1 otherwise |
| `deploy` | 0 if the deployment succeeded, 1 otherwise |
| usage error (missing arguments, invalid folder, unknown command) | 2 |

The `--project` option accepts exclusively the form `--project <dir>` (no `--flag=value`).

### 10.5 Packaging

The packaging scripts are `packaging/package-app.sh` (Linux/macOS) and `package-app.ps1` (Windows):

| Flag / parameter | Effect |
|---|---|
| `--type app-image\|deb\|dmg` / `-Type app-image\|msi` | output type |
| `--version X.Y.Z` / `-Version` | applied version |
| `--slim` / `-Slim` | reduced runtime with `jlink` |
| `--fat` | complete runtime (copy of the JDK) |
| `--runtime-image DIR` | runtime supplied by the user |
| `--skip-build` / `-SkipBuild` | reuses the already compiled jars |
| `--release` | produces the portable folder `jar` + `lib/` and terminates (no jpackage) |

For the writers, **`--release`** is used: the produced folder contains the jar, `lib/`, the JavaFX
jars for Linux/Windows/macOS, `run.sh`, `run.bat` and a `README.txt`. Only a JRE 21 is required.

---

## 11. Unity editor tools

| Tool | File | What it produces |
|---|---|---|
| Graph Editor | `DialogueGraphWindow` / `DialogueGraphView` | `*.graph.yaml` graphs; rewrites `Graphs/manifest.txt` at every save |
| Validation | `DialogueValidator`, `DialogueProjectValidator` | report in the Console with `[3xx]` codes |
| Digest | `DialogueDigestTools`, `DialogueGraphDigest` | `engine.graphdigest.json` |
| Indices | `ManifestGenerator` | `manifest.txt` for graphs and languages |
| AOT / IL2CPP | `DialogueAotTools`, `Assets/MySys22.DialogueEngine/link.xml` | `link.xml` to prevent the classes used by reflection from disappearing |
| Scene setup | `DialogueSceneSetup` | Canvas, texts, choice panel, references |

AOT note: the engine uses reflection (YamlDotNet, action discovery) and on IL2CPP this can break if
the linker eliminates the types. The `link.xml` file is already in the package; the
`Generate AOT link.xml` menu extends it with the user-defined actions.

---

## 12. Build process

1. **Deployment**: `Inject` from the Java editor (or `deploy` from the CLI).
2. **Indices**: `MySys22 ▸ Dialogue ▸ Generate Index Manifests` (mandatory before a mobile build,
   optional on desktop).
3. **AOT**: `Generate AOT link.xml` if IL2CPP is used.
4. **Build**: Unity Build Settings according to the usual procedure.

With regard to distribution, the engine is a folder inside `Assets` and does not require packaging;
the editor is distributed by means of `--release` (portable) or by means of `app-image` / `deb` /
`dmg` / `msi`.

## 13. Error codes

| Code | Meaning |
|---|---|
| 302 | parsing error of a YAML line database |
| 303 | reference to a non-existent node / no start node |
| 304 | unknown node type |
| 305 | error during the execution of an action |
| 306 | node without the mandatory data (speaker, DID range, choices, action) |
| 310 | choice index out of range |
| 311 | bridge initialisation error |
| 312 | invalid player state |
| 313 | `DialogueBridge` not found in the scene |
| 314 | SIMD filter failed, scalar path used |
| 315 | line index missing or empty (`Dialogue/<LINGUA>/manifest.txt`) |
| 317 | graph loading failed, or graph index missing (`Graphs/manifest.txt`) |
| 318 | graph loading/saving error (editor) |
| 319 | destination graph of a jump not found |
| 320 | parsing error of `engine.manifest.json` |
| 321 | parsing error of `characters.list.yaml` |
| 322 | digest error |
| 323 | parsing error of the variable catalog |
| 324 | evaluation of a condition failed |
| 330 | project validation failed |
| 340 | unable to start the Java editor (path or Maven not configured) |

All engine logs have the prefix `[MySys22.DialogueEngine]`, filterable in the Console and in the
logcat: `adb logcat -s Unity | grep MySys22`.

---

---

This document constitutes the programming reference for MySys22.DialogueEngine. The public namespaces, the usage conventions, the static facade `Dialogue`, the `DialogueBridge` component, the presentation contract `IDialogueView` and the functional areas of the engine are described.

## 1. Conventions

### 1.1 Namespaces and reference assembly

The namespaces listed in the following table belong to the engine assembly.

| Namespace | Content |
|---|---|
| `MySys22.Engine.API` | static facade `Dialogue`, `IDialogueView` — **game usage namespace** |
| `MySys22.DialogueEngine` | `GameSetup`, `DialogueBridge` |
| `MySys22.DialogueEngine.Core` | data, loading, actions, variables, quests, position |
| `MySys22.DialogueEngine.UI` | `DialogueUICanvas`, `DialogueOverlayUI` |
| `MySys22.DialogueEngine.Audio` | `DialogueAudio`, `IDialogueAudioProvider` |

Reference rule: game code uses `Dialogue.*`; the `Core` namespaces are used to extend the engine.

### 1.2 Naming conventions

- Action identifiers are strings declared in the `[DialogueAction("Id")]` attribute; the identifier value is the value reported in the Function node of the graph.
- The serialized fields of the components (`GameSetup`, `DialogueBridge`, `DialogueUICanvas`, `DialogueOverlayUI`) are named with the `_` prefix.
- The members exposed by the static facade `Dialogue` correspond to the members of the active `DialogueBridge`; the facade and the component adopt the same naming.
- Diagnostic codes are three-digit numeric values, reported in square brackets in the message.

### 1.3 Error handling

- Message logging is centralized in `DialogueLogger`. Qualified error messages use the form `DialogueLogger.LogError(code, description, detail = null)`, with code `[3xx]`.
- The evaluation of an invalid expression produces the log `[324]` and the return value `false`.
- Project validation is exposed by `Validate()`, which returns a `DialogueProjectValidator.Report` with the `Errors`, `Warnings`, `Infos` collections and the `NodesChecked`, `LinesChecked` counters.

### 1.4 Threading model

- The execution of actions occurs on the Unity main thread.
- The asynchronous work inside an action is expressed by means of `ExecuteAsync(DialogueActionContext context, CancellationToken cancellationToken)`.
- `Cancel()` is invoked when the dialogue stops or changes node.
- On the mobile platform, initialization requires `InitializeAsync()`; `Initialize()` is synchronous and is intended for desktop.
- `Reload()` is asynchronous on the mobile platform.

### 1.5 Initialization preconditions

- The active bridge is resolved automatically by `Dialogue.Bridge`; explicit setting is performed by means of `Dialogue.SetBridge(DialogueBridge)`.
- Adding `GameSetup` to the scene initializes the engine and starts the dialogue; in the absence of `GameSetup`, `DialogueBridge` initializes itself.
- `StartDialogue()` may also be invoked before initialization completes: under that condition the start is deferred.
- The loaded data (`Project`, `Graph`, `GraphPath`, `Manifest`) are available after initialization.

## 2. Startup and lifecycle

### 2.1 Startup by means of components

Adding the `GameSetup` component to the scene causes complete initialization of the engine and the start of the dialogue. The presence of `GameSetup` excludes double starts, since the start is managed by the component itself.

### 2.2 Startup by means of code

The following example subscribes to the bridge events and starts the dialogue.

```csharp
using MySys22.Engine.API;

public class MyGame : MonoBehaviour
{
    void Start()
    {
        Dialogue.Bridge.OnLine += (speaker, text) => Debug.Log($"{speaker}: {text}");
        Dialogue.Bridge.OnChoices += choices => Dialogue.Bridge.Choose(0);
        Dialogue.Bridge.OnDialogueEnd += () => Debug.Log("done");

        Dialogue.Bridge.StartDialogue();
    }
}
```

### 2.3 Lifecycle sequence

The expected sequence is the following:

1. Initialization of the bridge by means of `Initialize()` or `InitializeAsync()`.
2. Loading of the data: `Project`, `Graph`, `GraphPath`, `Manifest`.
3. Start of the dialogue by means of `StartDialogue()`.
4. Advancement by means of `Continue()` or selection of an option by means of `Choose(int index)`.
5. Stop by means of `StopDialogue()`, restart by means of `Restart()`, reload by means of `Reload()`.

## 3. Presentation (`IDialogueView`)

### 3.1 Interface contract

Implementing the `IDialogueView` interface allows the default presentation to be replaced; once registration has occurred the engine no longer uses its own view. The interface exposes the following members, as implemented in the example of section 3.2: `IsPanelVisible`, `SetPanelVisible(bool visible)`, `ShowText(string speaker, string text)`, `ShowText(string text)`, `SetSpeaker(string speaker)`, `SetText(string text)`, `HideText()`, `HideChoices()`.

### 3.2 Implementation of a custom view

**Example**

```csharp
using MySys22.Engine.API;

public class MyDialogueView : MonoBehaviour, IDialogueView
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text textLabel;

    public bool IsPanelVisible => panel.activeSelf;

    public void SetPanelVisible(bool visible) => panel.SetActive(visible);
    public void ShowText(string speaker, string text) { speakerLabel.text = speaker; textLabel.text = text; }
    public void ShowText(string text) => ShowText(null, text);
    public void SetSpeaker(string speaker) => speakerLabel.text = speaker;
    public void SetText(string text) => textLabel.text = text;
    public void HideText() => textLabel.text = string.Empty;
    public void HideChoices() { /* destroy or hide the buttons */ }

    void Awake() => Dialogue.SetView(this);
}
```

### 3.3 View resolution rules

The engine imposes no obligations on the view. In the absence of a registered view, the engine uses `DialogueUICanvas` if present in the scene, otherwise `DialogueOverlayUI`.

### 3.4 `EventSystem` requirement

`DialogueUICanvas` and `DialogueOverlayUI` create an `EventSystem` if one is not present, since the choice buttons do not receive pointer events in its absence. A custom view based on uGUI buttons requires the presence of an `EventSystem`.

## 4. `Dialogue` class reference

### 4.1 Bridge, view and player

| Member | Description |
|---|---|
| `Dialogue.Bridge` | the active `DialogueBridge` (auto-resolved) |
| `Dialogue.SetBridge(DialogueBridge)` | sets the bridge explicitly |
| `Dialogue.Player` | the `DialogueGraphPlayer` |
| `Dialogue.View` / `SetView(IDialogueView)` | the presentation view |
| `Dialogue.IsFunctionRunning` | a function is awaiting a result |

**Syntax**

```csharp
Dialogue.SetBridge(DialogueBridge)
```

**Parameters**

| Parameter | Description |
|---|---|
| instance of `DialogueBridge` | the bridge to set as active |

**Notes**

`Dialogue.Bridge` resolves the active bridge automatically. `Dialogue.View` and `SetView(IDialogueView)` expose and set the presentation view. `Dialogue.IsFunctionRunning` indicates the presence of a function awaiting a result.

### 4.2 Panel and text

| Member | Description |
|---|---|
| `ShowPanel()` / `HidePanel()` | shows/hides the dialogue panel |
| `IsPanelVisible` | panel state |
| `ShowText(speaker, text)` / `ShowText(text)` | writes a line |
| `SetSpeaker(string)` / `SetText(string)` | updates only one part |
| `HideText()` / `HideChoices()` | hides text or choices |

**Syntax**

```csharp
ShowPanel()
HidePanel()
ShowText(speaker, text)
ShowText(text)
SetSpeaker(string)
SetText(string)
HideText()
HideChoices()
```

**Notes**

`IsPanelVisible` indicates the panel state. The forms `ShowText(speaker, text)` and `ShowText(text)` write a complete or partial line; `SetSpeaker(string)` and `SetText(string)` update a single part; `HideText()` and `HideChoices()` hide text and choices respectively.

### 4.3 Functions

| Member | Description |
|---|---|
| `Complete(bool result)` / `Complete()` | closes the function with true/false |
| `CompleteNeutral()` | closes the function without a result (conditions are ignored) |
| `WaitForCompletion(DialogueActionContext)` | `Task` that completes when the function closes |
| `RunAction(actionId, parameters = null)` | executes an action outside the graph |
| `HasAction(id)` / `Actions` | registered actions |

**Syntax**

```csharp
Complete(bool result)
Complete()
CompleteNeutral()
WaitForCompletion(DialogueActionContext)
RunAction(actionId, parameters = null)
HasAction(id)
Actions
```

**Parameters**

| Parameter | Description |
|---|---|
| `result` | boolean outcome with which the function is closed |
| `DialogueActionContext` | context of the function whose closure is awaited |
| `actionId` | identifier of the action to execute |
| `parameters` | parameter dictionary; the default value is `null` |
| `id` | identifier of the registered action |

**Return value**

`WaitForCompletion(DialogueActionContext)` returns a `Task` that completes when the function closes. `HasAction(id)` verifies the presence of the registered action; `Actions` exposes the registered actions.

**Example**

```csharp
[DialogueAction("AskPassword")]
public sealed class AskPasswordAction : IAction
{
    public void Execute(DialogueActionContext ctx)
    {
        UIManager.OpenPasswordPanel(success =>
        {
            ctx.SetResult(success);
            ctx.Complete();
        });
    }

    public Task ExecuteAsync(DialogueActionContext ctx, CancellationToken ct) => Task.CompletedTask;
    public void Cancel() => UIManager.ClosePasswordPanel();
}
```

### 4.4 Audio

| Member | Description |
|---|---|
| `PlayAudio(track, loop = false)` / `StopAudio()` | ambience track |
| `PlayVoice(speakerCid, did)` / `StopVoice()` | voice of a line |
| `StopAllAudio()` | stops everything |
| `RegisterAudioClip(track, clip)` | registers a clip at runtime (e.g. from Addressables) |
| `IsAudioPlaying` / `CurrentAudioTrack` | state |
| `VoiceEnabled` | enables/disables voices |

**Syntax**

```csharp
PlayAudio(track, loop = false)
StopAudio()
PlayVoice(speakerCid, did)
StopVoice()
StopAllAudio()
RegisterAudioClip(track, clip)
IsAudioPlaying
CurrentAudioTrack
VoiceEnabled
```

**Parameters**

| Parameter | Description |
|---|---|
| `track` | identifier of the ambience track |
| `loop` | indicates the repetition of the track; the default value is `false` |
| `speakerCid` | identifier of the speaker for the voice |
| `did` | identifier of the line |
| `clip` | audio clip registered at runtime |

**Notes**

`RegisterAudioClip(track, clip)` registers a clip at runtime, for example originating from Addressables. `IsAudioPlaying` and `CurrentAudioTrack` indicate the playback state; `VoiceEnabled` enables or disables the voices.

## 5. `DialogueBridge` class reference

### 5.1 Events

| Event | Payload |
|---|---|
| `OnLine` | `(string speaker, string text)` |
| `OnChoices` | `List<ChoiceOption>` |
| `OnDialogueEnd` | — |
| `OnWaitingForActionChanged` | `bool waiting` |
| `OnGraphJumped` | `(string graph, string node)` |
| `OnVariableChanged` | `(string name, DialogueValue value)` |
| `OnQuestChanged` | `(string questId, QuestState state)` |
| `OnPositionChanged` | `DialoguePosition` |

`ChoiceOption` exposes `Text`, `Did`, `Next`, `Index`, `Condition`, `Enabled`/`Visible` (according to the option condition).

### 5.2 Dialogue control

| Member | Description |
|---|---|
| `Initialize()` | initializes (synchronous: desktop) |
| `InitializeAsync()` | initializes (asynchronous: mandatory on mobile) |
| `IsInitialized` | state |
| `StartDialogue()` | starts from the start node; if the init is in progress the start is deferred |
| `Continue()` | advances to the next line |
| `Choose(int index)` | selects an option |
| `StopDialogue()` | stops and cancels the actions in progress |
| `Restart()` | restarts from the beginning |
| `Reload()` | reloads the project (asynchronous on mobile) |
| `IsDialogueActive`, `IsWaitingForAction`, `PendingActionId` | state |
| `SetGraphPath(string)` | changes graph before the start |
| `Project`, `Graph`, `GraphPath`, `Manifest` | loaded data |
| `Validate()` | `DialogueProjectValidator.Report` |

**Syntax**

```csharp
Initialize()
InitializeAsync()
StartDialogue()
Continue()
Choose(int index)
StopDialogue()
Restart()
Reload()
SetGraphPath(string)
Validate()
```

**Parameters**

| Parameter | Description |
|---|---|
| `index` | index of the option to select in `Choose(int index)` |
| path | path of the graph set by `SetGraphPath(string)` |

**Return value**

`Validate()` returns a `DialogueProjectValidator.Report`.

**Notes**

- `Initialize()` is synchronous and is intended for desktop; `InitializeAsync()` is asynchronous and is mandatory on mobile.
- `StartDialogue()` starts from the start node; if initialization is in progress the start is deferred.
- `StopDialogue()` stops the dialogue and cancels the actions in progress.
- `Reload()` reloads the project and is asynchronous on mobile.
- `IsInitialized`, `IsDialogueActive`, `IsWaitingForAction` and `PendingActionId` indicate the bridge state.
- `Project`, `Graph`, `GraphPath` and `Manifest` expose the loaded data.

The example of initialization and manual start of the bridge is reported in section 12.2.

### 5.3 Language

| Member | Description |
|---|---|
| `CurrentLanguage` / `CurrentLanguageLabel` | current language (code and label) |
| `AvailableLanguages` | distributed list |
| `SetLanguage(lang, restartIfActive = true)` | changes language |
| `CycleLanguage(restartIfActive = true)` | advances to the next one |
| `RefreshLanguages()` | re-reads the languages from the manifest |

**Syntax**

```csharp
SetLanguage(lang, restartIfActive = true)
CycleLanguage(restartIfActive = true)
RefreshLanguages()
```

**Parameters**

| Parameter | Description |
|---|---|
| `lang` | code of the language to set |
| `restartIfActive` | indicates the restart of the dialogue if it is active; the default value is `true` |

**Notes**

`CurrentLanguage` and `CurrentLanguageLabel` expose the code and the label of the current language; `AvailableLanguages` exposes the distributed list. `RefreshLanguages()` re-reads the languages from the manifest.

### 5.4 Savable state

```csharp
string vars   = bridge.SaveVariableState();      // variables only
bridge.LoadVariableState(vars);

string state  = bridge.SaveNarrativeState();     // variables + quests
```

`SaveVariableState()` serializes the variables only; `LoadVariableState(vars)` restores them. `SaveNarrativeState()` serializes variables and quests.

## 6. Custom actions

### 6.1 Contract

**Syntax**

```csharp
public interface IAction
{
    void Execute(DialogueActionContext context);
    Task ExecuteAsync(DialogueActionContext context, CancellationToken cancellationToken);
    void Cancel();
}
```

**Parameters**

| Parameter | Description |
|---|---|
| `context` | execution context of the action |
| `cancellationToken` | cancellation token of the asynchronous execution |

**Notes**

`Execute(DialogueActionContext context)` executes the action; `ExecuteAsync(DialogueActionContext context, CancellationToken cancellationToken)` executes its asynchronous variant; `Cancel()` requests its cancellation.

### 6.2 Implementation rules

| Rule | Detail |
|---|---|
| No MonoBehaviour | actions are instantiated by the engine: pure classes are required, with invocation of the application singletons |
| Mandatory attribute | `[DialogueAction("Id")]` — the `Id` is the value reported in the Function node |
| Thread | the execution occurs on the Unity main thread |
| Cancellation | `Cancel()` is called if the dialogue stops or changes node |

Actions are instantiated by the engine: pure classes are required, with invocation of the application singletons. The `[DialogueAction("Id")]` attribute is mandatory and its `Id` is the value reported in the Function node.

### 6.3 Metadata

**Example**

```csharp
[DialogueAction("OpenDoor",
    Description = "Opens a door and waits for the animation.",
    RequiredParameters = new[] { "doorId" },
    OptionalParameters = new[] { "delay" },
    WaitsForCompletion = true)]
public sealed class OpenDoorAction : IAction
{
    private CancellationTokenSource _cts;

    public void Execute(DialogueActionContext ctx)
    {
        string doorId = ctx.GetParameter("doorId");
        float delay   = ctx.GetFloat("delay", 0f);
        DoorSystem.Open(doorId, delay, () =>
        {
            ctx.SetResult(true);
            ctx.Complete();
        });
    }

    public async Task ExecuteAsync(DialogueActionContext ctx, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await DoorSystem.OpenAsync(ctx.GetParameter("doorId"), _cts.Token);
        ctx.SetResult(true);
    }

    public void Cancel() => _cts?.Cancel();
}
```

The parameters declared in the metadata are retrieved from the context by means of `ctx.GetParameter("doorId")` and `ctx.GetFloat("delay", 0f)`.

### 6.4 Attribute flags

| Attribute flag | Effect |
|---|---|
| `WaitsForCompletion` | the node is marked as "waiting" even without `wait` in the graph |
| `CompletesExternally` | the function remains open: the game closes it with `Dialogue.Complete(...)` |
| `RequiredParameters` / `OptionalParameters` | used by validation and shown in the editor |

### 6.5 Function closed by the game (without waiting in the node)

```csharp
// in the graph: Function with Action = ExternalResult, Wait for completion = on
StartCoroutine(LoadLevelThenResume());

IEnumerator LoadLevelThenResume()
{
    yield return SceneManager.LoadSceneAsync("Level2");
    Dialogue.Complete(true);
}
```

### 6.6 Manual registration of an action

**Example**

```csharp
ActionRegistry.RegisterAction("Ping", () => new PingAction(),
    new ActionRegistry.ActionInfo { Description = "Test ping" });
```

Manual registration is indicated when the actions cannot reside in the engine assembly, due to distinct assembly definitions.

## 7. Variables

### 7.1 Static facade

**Example**

```csharp
Dialogue.SetBool("has_key", true);
Dialogue.SetInt("coins", 12);
Dialogue.AddInt("coins", -3);
Dialogue.SetFloat("volume", 0.8f);
Dialogue.SetString("player_name", "Anna");

bool ok  = Dialogue.GetBool("has_key");
int c    = Dialogue.GetInt("coins", 0);
float v  = Dialogue.GetFloat("volume", 1f);
string n = Dialogue.GetString("player_name", "?");
```

The write forms are `SetBool`, `SetInt`, `AddInt`, `SetFloat` and `SetString`; the read forms are `GetBool`, `GetInt`, `GetFloat` and `GetString`. The read forms accept a default value returned in the absence of the variable (`0` for `GetInt`, `1f` for `GetFloat`, `"?"` for `GetString`).

### 7.2 Evaluation of an expression

The evaluation of an expression occurs with the same grammar as the Condition nodes:

```csharp
bool found = Dialogue.Evaluate("quest.rescue.state == active && coins >= 3");
```

### 7.3 `DialogueVariables` service

**Example**

```csharp
DialogueVariables v = Dialogue.Variables;

v.OnChanged += (name, value) => Debug.Log($"{name} = {value}");
v.Declare(new VariableDeclaration { name = "hp", type = "int", value = "100" });
v.SetInt("hp", 80);
v.AddInt("hp", -10);
bool exists = v.Has("hp");
bool declared = v.IsDeclared("hp");
string snapshot = v.Serialize();
v.Deserialize(snapshot);
```

**Notes**

- `Dialogue.Variables` exposes the `DialogueVariables` service.
- `OnChanged` is the change event, with payload `(name, value)`.
- `Declare(new VariableDeclaration { name = "hp", type = "int", value = "100" })` declares a variable with type and initial value.
- `SetInt`, `AddInt`, `Has`, `IsDeclared`, `Serialize` and `Deserialize` operate on the indicated variable.

## 8. Expressions and conditions

### 8.1 Evaluation

The evaluation uses the same grammar as the Condition nodes.

```csharp
if (Dialogue.Evaluate("quest.rescue.state == active && !has_key"))
{
    // ...
}
```

### 8.2 Supported grammar

| Supported | Not supported |
|---|---|
| `==`, `!=`, `<`, `<=`, `>`, `>=` | arithmetic (`a + 1`) |
| `&&`, `\|\|`, `!`, parentheses | function calls |
| `true/false` literals, numbers, strings | access to C# objects |
| dotted identifiers (`quest.rescue.state`) | `eval` of code |

### 8.3 Invalid expressions

Invalid expressions: log `[324]`, result `false`.

## 9. Quests

### 9.1 Static facade

| Member | Description |
|---|---|
| `AcceptQuest(id)` / `CompleteQuest(id)` / `FailQuest(id)` | changes state |
| `SetQuestStep(id, stepId, done = true)` | marks an objective |
| `GetQuestState(id)` | `Unknown`, `Available`, `Active`, `Completed`, `Failed` |

**Syntax**

```csharp
AcceptQuest(id)
CompleteQuest(id)
FailQuest(id)
SetQuestStep(id, stepId, done = true)
GetQuestState(id)
```

**Parameters**

| Parameter | Description |
|---|---|
| `id` | identifier of the quest |
| `stepId` | identifier of the quest objective |
| `done` | indicates the completion of the objective; the default value is `true` |

**Return value**

`GetQuestState(id)` returns one of the values `Unknown`, `Available`, `Active`, `Completed`, `Failed`.

**Notes**

`AcceptQuest(id)`, `CompleteQuest(id)` and `FailQuest(id)` modify the quest state. `SetQuestStep(id, stepId, done = true)` marks an objective.

### 9.2 Custom quest service

**Example**

```csharp
public class MyQuestService : IQuestService { }

Dialogue.Bridge.SetQuestService(new MyQuestService());
```

With a custom service, `SaveNarrativeState()` saves the variables only: the persistence of quests remains in the application system.

## 10. Position and save

### 10.1 Static facade

| Member | Description |
|---|---|
| `Dialogue.Position` | current `DialoguePosition` (or `null`) |
| `Dialogue.SavePosition()` | string ready to be saved |
| `Dialogue.RestorePosition(snapshot)` | rebuilds a `DialoguePosition` from a string |
| `Dialogue.SetFunctionProgress(string)` | progress of a long function (e.g. `"3/7"`) |
| `Dialogue.OnPositionChanged` | event `Action<DialoguePosition>` |

**Syntax**

```csharp
Dialogue.Position
Dialogue.SavePosition()
Dialogue.RestorePosition(snapshot)
Dialogue.SetFunctionProgress(string)
Dialogue.OnPositionChanged
```

**Parameters**

| Parameter | Description |
|---|---|
| `snapshot` | position state string |
| progress value | progress of a long function, for example `"3/7"` |

**Return value**

`Dialogue.SavePosition()` returns a string ready to be saved; `Dialogue.RestorePosition(snapshot)` returns a `DialoguePosition` rebuilt from the string.

**Notes**

`Dialogue.Position` exposes the current `DialoguePosition` or `null`. `Dialogue.OnPositionChanged` is an event of type `Action<DialoguePosition>`.

**Example**

```csharp
Dialogue.OnPositionChanged += pos =>
{
    Debug.Log($"{pos.GraphId} / {pos.NodeId} / line {pos.LineIndex} of {pos.LineCount}");
    if (pos.IsInsideFunction)
        Debug.Log($"inside function {pos.FunctionActionId}: {pos.FunctionProgress}");
};

string snapshot = Dialogue.SavePosition();      // e.g. PlayerPrefs.SetString("dlg", snapshot)

DialoguePosition pos = Dialogue.RestorePosition(snapshot);
Debug.Log($"restorable at {pos.GraphId}:{pos.NodeId}");
```

### 10.2 Fields of `DialoguePosition`

| Field | Meaning |
|---|---|
| `GraphId` | current graph |
| `NodeId`, `NodeType` | current node |
| `Speaker`, `Did`, `LineIndex`, `LineCount` | current line and position in the line |
| `WaitingForChoice`, `WaitingForAction` | waits |
| `FunctionActionId`, `FunctionProgress` | function in progress and its progress |
| `Language`, `Running` | language and dialogue state |
| `IsInsideFunction` | shortcut: `WaitingForAction || FunctionActionId != null` |

### 10.3 Restore model

The engine does not write to disk: the position is captured by the engine and saved by the application code. Restore is designed to be driven by the game: the variables, the quests and the scene are brought back to the desired state and the dialogue restarts from the saved node, maintaining full control over saves, checkpoints and reopening of the scenes.

## 11. Diagnostics

### 11.1 Logging

| Member | Description |
|---|---|
| `DialogueLogger.Enabled` | switches the logs on/off |
| `DialogueLogger.Prefix` | `[MySys22.DialogueEngine]` |
| `DialogueLogger.Log(...)` / `LogWarning(...)` / `LogError(...)` | coherent logs |
| `DialogueLogger.LogError(code, description, detail = null)` | log with code `[3xx]` |
| `bridge.Validate()` | `Report` with `Errors`, `Warnings`, `Infos`, `NodesChecked`, `LinesChecked`, `ToText()` |

**Syntax**

```csharp
DialogueLogger.Enabled
DialogueLogger.Prefix
DialogueLogger.Log(...)
DialogueLogger.LogWarning(...)
DialogueLogger.LogError(...)
DialogueLogger.LogError(code, description, detail = null)
bridge.Validate()
```

**Parameters**

| Parameter | Description |
|---|---|
| `code` | numeric code of the message, in the format `[3xx]` |
| `description` | description of the message |
| `detail` | additional detail; the default value is `null` |

**Notes**

`DialogueLogger.Prefix` corresponds to `[MySys22.DialogueEngine]`. `bridge.Validate()` returns a `Report` with the `Errors`, `Warnings`, `Infos` collections, the `NodesChecked`, `LinesChecked` counters and the `ToText()` method.

### 11.2 Message filter

The prefix of the messages is `MySys22`; on device the corresponding filter is `adb logcat -s Unity | grep MySys22`.

## 12. Scene integration

### 12.1 Components

| Component | Role | Main fields |
|---|---|---|
| `GameSetup` | complete bootstrap | `_graphPath`, `_autoStartDialogue`, `_languageOverride`, `_createOverlayFallback`, `_enableLogs`, `_validateOnLoad`, `_enableSimd`, `_logSimdArchitecture` |
| `DialogueBridge` | facade/events | `_graphPath`, `_language`, `_autoStartOnAwake`, `_logEvents`, `_logEveryLine`, `_validateOnLoad` |
| `DialogueUICanvas` | TextMeshPro UI | `_panel`, `_speakerText`, `_dialogueText`, `_choicesContainer`, `_choiceButtonTemplate`, `_continueButton`, `_hideOnEnd`, `_typewriterDelay`, `_continueKey`, `_advanceOnClick` |
| `DialogueOverlayUI` | fallback IMGUI UI | `_visible`, `_fontSize`, `_panelHeight`, `_margin`, `_continueKey`, `_clickToContinue`, `_hideOnEnd` |

In the absence of `GameSetup`, `DialogueBridge` initializes itself and starts the dialogue (`Auto start dialogue`). In the presence of `GameSetup`, the start is managed by the component: no double starts occur.

### 12.2 Manual start, without components

**Example**

```csharp
var go = new GameObject("Dialogue");
var bridge = go.AddComponent<DialogueBridge>();
await bridge.InitializeAsync();       // on mobile
bridge.StartDialogue();
```
