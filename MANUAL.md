# Personal Flip Pad (pfpad) Beginner's Manual

Welcome to **Personal Flip Pad** — a Windows code editor for people who write **C#**, **C**, and **C++** and want a focused editor without a lot of ceremony.

If you're new to pfpad, don't worry. You do **not** need to memorize everything here. This manual is designed so you can read the first few sections, start working, and come back later when you need a specific feature.

---

## Quick Jump

1. [Introduction & Philosophy](#1-introduction--philosophy)
2. [Installing & First Launch](#2-installing--first-launch)
3. [The Interface](#3-the-interface)
4. [Opening Files and Projects](#4-opening-files-and-projects)
5. [Basic Editing](#5-basic-editing)
6. [Vim Mode](#6-vim-mode)
7. [Customizing pfpad](#7-customizing-pfpad)
8. [Snippets — Type Less, Code More](#8-snippets--type-less-code-more)
9. [Git & Source Control](#9-git--source-control)
10. [For C# Developers](#10-for-c-developers)
11. [For C Developers](#11-for-c-developers)
12. [For C++ Developers](#12-for-c-developers)
13. [Security Hardening](#13-security-hardening)
14. [Keyboard Shortcuts Reference](#14-keyboard-shortcuts-reference)
15. [Troubleshooting & FAQ](#15-troubleshooting--faq)

---

# 1. Introduction & Philosophy

pfpad is a **multi-tab Windows code editor** built for real programming work, especially in:

- **C#**
- **C**
- **C++**

Its philosophy is simple:

- **Open code quickly**
- **Edit comfortably**
- **Build and run from the editor**
- **Debug without leaving your workflow**
- **Stay forgiving for beginners**

pfpad does not expect you to learn everything at once. You can begin with just:

- opening files,
- typing code,
- saving,
- searching,
- and using the terminal.

Then, when you're ready, you can add snippets, symbol navigation, Git, Vim mode, and debugging.

If you come from Visual Studio, VS Code, Notepad++, Vim, or "just a terminal," pfpad can meet you where you are.

---

# 2. Installing & First Launch

## 2.1 Install pfpad

Install pfpad the same way you would install a normal Windows application.

On first launch, keep your goal small:

1. Start pfpad.
2. Open a file with **Ctrl+O**.
3. Or open a folder/project with **Ctrl+Shift+O**.
4. Make a tiny edit.
5. Save with **Ctrl+S**.

That is enough to get productive.

## 2.2 Your first five minutes

A good first session looks like this:

1. Press **Ctrl+Shift+O** to open your project folder.
2. Open the **Workspace** panel with **View → Panels → Workspace**.
3. Open a source file.
4. Press **Ctrl+`** to open the terminal.
5. Run your usual build command.
6. Press **Ctrl+Shift+P** and type `format` to discover commands.

Don't worry if you do not configure everything on day one. pfpad is happiest when learned in layers.

## 2.3 What to install for a full setup

Depending on your language, you may want these extras:

### For C#
- **.NET SDK**
- **netcoredbg** for debugging  
  Download: https://github.com/Samsung/netcoredbg

### For C
- **gcc** or **make**
- **gdb** or **cppvsdbg** for debugging
- Optional: **ctags** for better symbol navigation

### For C++
- **CMake** if your project uses it
- **gdb** or **cppvsdbg** for debugging
- Optional: **ctags** for better symbol navigation

For Windows + GDB, a common setup is:

1. Install **MSYS2**
2. Open an MSYS2 shell
3. Run:

```bash
pacman -S mingw-w64-ucrt-x86_64-gdb
```

---

# 3. The Interface

Think of pfpad as a few simple areas working together.

## 3.1 Main areas of the window

### 1. Tab bar
Your open files live here.

- New tab: **Ctrl+T**
- Close tab: **Ctrl+W**

### 2. Editor area
This is where you write code.

You can split it when you want to compare files:

- Vertical split: **Ctrl+Shift+V**
- Horizontal split: **Ctrl+Alt+H**
- Close split: **Ctrl+Shift+W**

### 3. Workspace panel
This is your file explorer for the current folder.

Open/toggle it from:
- **View → Panels → Workspace**

Useful detail: it shows **Git status colors** and automatically respects **.gitignore**.

### 4. Git panel
This is where you stage, commit, fetch, pull, and push.

Open it from:
- **View → Panels → Git Panel**

### 5. Symbol and Outline panels
Use these when you want to move around code quickly.

- **View → Panels → Symbols**
- **View → Panels → Outline**

A good mental model:
- **Outline** = structure of the current file
- **Symbols** = bigger navigation help

### 6. Integrated terminal
Open it with:
- **Ctrl+`**

This is where you run commands like:

```bash
dotnet build
make
gcc -o app *.c
cmake --build build
```

### 7. Status bar
The status bar gives you quick-access information and controls, including:

- current branch
- build configuration dropdown (**Debug/Release**)
- theme dropdown

## 3.2 Helpful visual features

### Gutter
The gutter is the strip beside your code.

It can show:
- line numbers
- breakpoints
- bookmarks

You can toggle these from:
- **View → Display**

### Minimap
The minimap is a compact overview of your file.

Toggle it from:
- **View → Layout → Minimap**

By default it uses **50% opacity** and adjusts with the scrollbar.

### Themes
pfpad includes **22 built-in themes**.

Change theme with:
- the **status bar dropdown**, or
- **View → Appearance → Theme**

---

# 4. Opening Files and Projects

## 4.1 Open one file
Press:
- **Ctrl+O**

Use this when you just want to edit a single file quickly.

## 4.2 Open a whole folder/project
Press:
- **Ctrl+Shift+O**

Or use:
- **File → Open Folder**

This is the best choice for real development work because it enables the **Workspace** view and makes navigation easier.

## 4.3 Clone a repository
Press:
- **Ctrl+Shift+C**

Or use:
- **File → Clone Repository**

Typical beginner workflow:

1. Clone the repository.
2. Open the folder if it does not open automatically.
3. Open **View → Panels → Workspace**.
4. Open **View → Panels → Git Panel**.
5. Start editing.

## 4.4 Example project opening flows

### C# project
1. Open the folder containing your `.sln` or `.csproj`.
2. Open a `.cs` file.
3. Use **F12** on symbols.
4. Use **Ctrl+`** and run:

```bash
dotnet build
```

### C project
1. Open the folder containing your `.c` and `.h` files.
2. Open `main.c`.
3. Set a profile in **Tools → User Profiles**.
4. Build from the terminal or your profile command.

### C++ project
1. Open the folder containing your `.cpp`, `.hpp`, `CMakeLists.txt`, or `src/` folder.
2. Use **Alt+O** to switch between header and source files.
3. Build with terminal commands or a workspace profile.

---

# 5. Basic Editing

## 5.1 Everyday editing shortcuts

These are the shortcuts to learn first:

| Action | Shortcut |
|---|---|
| New tab | Ctrl+T |
| Close tab | Ctrl+W |
| Open file | Ctrl+O |
| Save | Ctrl+S |
| Save all | Ctrl+Alt+S |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Go to line | Ctrl+G |
| Format document | Ctrl+Shift+I |
| Command palette | Ctrl+Shift+P |

If you only memorize six shortcuts, make them: **Ctrl+O, Ctrl+S, Ctrl+F, Ctrl+H, Ctrl+G, Ctrl+Shift+P**.

## 5.2 Working with tabs

pfpad is a **multi-tab editor**.

Try this:

1. Open `Program.cs`.
2. Press **Ctrl+T** for a new tab.
3. Open `appsettings.json` in another tab.
4. Press **Ctrl+W** to close the tab you do not need.

## 5.3 Find, replace, and search

### Find in the current file
Press:
- **Ctrl+F**

Find supports:
- **regex**
- **case-sensitive** search
- **wrap**

That means you can do simple searches or more advanced ones.

Example:
- Search for all lines containing `TODO`
- Search for exact `Main` with case sensitivity
- Use regex like `^using ` in a C# file

### Replace in the current file
Press:
- **Ctrl+H**

Good beginner use cases:
- rename a temporary variable in one file
- replace tabs/spaces in a small file
- fix repeated logging text

### Search across the project
Press:
- **Ctrl+Shift+F**

Use this when you want to answer questions like:
- "Where is `ParseConfig` used?"
- "Which files include `stdio.h`?"
- "Where do we create this class?"

## 5.4 Go to line
Press:
- **Ctrl+G**

Very useful when:
- a compiler error says `line 87`
- a stack trace points to a line number
- a teammate tells you where to look

## 5.5 Zoom in and out

- Zoom in: **Ctrl+Plus**
- Zoom out: **Ctrl+Minus**

If text feels too small, fix it immediately. A comfortable editor is a productive editor.

## 5.6 Bookmarks

Bookmarks are excellent when you're moving around a file a lot.

- Toggle bookmark: **Ctrl+B**
- Next bookmark: **Ctrl+Shift+Period**
- Previous bookmark: **Ctrl+Shift+Comma**

Example:

1. Put a bookmark on a function you are editing.
2. Put another on the place where it is called.
3. Jump back and forth while working.

## 5.7 Splitting the editor

When comparing files, use splits.

- Vertical split: **Ctrl+Shift+V**
- Horizontal split: **Ctrl+Alt+H**
- Close split: **Ctrl+Shift+W**

Example:

- Left side: `foo.cpp`
- Right side: `foo.hpp`

Or:

- Top: failing test
- Bottom: implementation file

---

# 6. Vim Mode

Vim mode is **optional**. You do not need it to use pfpad well.

If you are curious, pfpad makes it approachable.

## 6.1 Turn Vim mode on
Use:
- **View → Display → Vim Mode**

If you try it and dislike it, you can turn it off again. No harm done.

## 6.2 The big idea

Vim mode separates editing into modes.

### Normal mode
For moving around and issuing commands.

### Insert mode
For typing text normally.

### Visual mode
For selecting text.

## 6.3 Basic keys to learn first

### Movement in normal mode
- `h` = left
- `j` = down
- `k` = up
- `l` = right
- `w` = next word
- `b` = previous word

### Editing in normal mode
- `dd` = delete line
- `yy` = copy line
- `p` = paste after
- `P` = paste before
- `u` = undo
- `Ctrl+R` = redo

### Visual mode
- `v` = character selection
- `V` = line selection
- `Ctrl+V` = block selection

## 6.4 Command mode

Useful commands:
- `:w` = save
- `:q` = quit, or close split if a split is open
- `:wq` = save and quit
- `:vsp` = vertical split
- `:sp` = horizontal split
- `:close` = close split

## 6.5 Searching in Vim mode

- `/` = search forward
- `?` = search backward
- `n` = next match
- `N` = previous match

## 6.6 Snippets still work
In insert mode, snippets expand with **Tab**.

That means Vim users still get the fast templating benefits of pfpad.

---

# 7. Customizing pfpad

pfpad is meant to feel comfortable, not rigid.

Don't worry if your first instinct is to change the font, tabs, or theme. Most developers do.

## 7.1 Change the theme

Use either:
- **View → Appearance → Theme**
- or the **theme dropdown in the status bar**

pfpad includes **22 built-in themes**.

A good beginner approach:
- pick one dark theme for long sessions
- pick one light theme for daytime or screenshots

## 7.2 Open Settings

Open settings with:
- **Ctrl+,**
- or **File → Preferences → Settings...**

Inside Settings, useful categories include:
- **Editor → Font**
- **Editor → Formatting**
- **Workbench → Appearance**

## 7.3 Change the editor font

Open:
- **Ctrl+,**
- then go to **Editor → Font**

If code looks cramped or too small, change the font first before changing anything more advanced.

## 7.4 Change tab size

You have two easy ways to do this.

### Option 1: Status bar
Use the **tab size dropdown** in the status bar.

### Option 2: Settings
Open:
- **Ctrl+,**
- then **Editor → Formatting**

## 7.5 Change minimap and gutter display

### Minimap
- **View → Layout → Minimap**

### Gutter elements
- **View → Display**

This is where you manage visible editor helpers like line numbers, breakpoints, and bookmarks.

## 7.6 Workspace build/run profiles

For project-specific commands, use:
- **Tools → User Profiles**

This is especially helpful when one workspace needs commands that another does not.

Examples:

### C# workspace profile
- `BuildCommand = dotnet build`
- `RunCommand = dotnet run`

### C workspace profile
- `BuildCommand = gcc -o app *.c`
- `RunCommand = .\app.exe`

or

- `BuildCommand = make`
- `RunCommand = .\app.exe`

### C++ workspace profile
- `BuildCommand = cmake --build build`
- `RunCommand = .\build\app.exe`

You can also use a preset-based build, for example:

- `BuildCommand = cmake --preset debug`

---

# 8. Snippets — Type Less, Code More

Snippets are one of the fastest ways to get comfortable in pfpad.

They work like this:

1. Type a trigger word
2. Press **Tab**
3. pfpad expands it into code

## 8.1 General snippets

These work as quick notes:
- `todo`
- `hack`
- `note`

## 8.2 C# snippets

Available triggers:

- `for`
- `foreach`
- `while`
- `do`
- `try`
- `tryf`
- `if`
- `ife`
- `else`
- `switch`
- `class`
- `struct`
- `interface`
- `enum`
- `prop`
- `propg`
- `propfull`
- `ctor`
- `main`
- `console`
- `cw`

### Example
Type:

```csharp
prop
```

Then press **Tab**.

This is a great way to create properties without typing the full structure every time.

## 8.3 C snippets

Available triggers:

- `main`
- `for`
- `while`
- `do`
- `switch`
- `struct`
- `typedef`
- `printf`
- `malloc_free`
- `guard`
- `fori`
- `printf_err`

### Example
In a header file, type:

```c
guard
```

Then press **Tab** to create a header guard quickly.

## 8.4 C++ snippets

Available triggers:

- `main`
- `class`
- `struct`
- `template`
- `vec`
- `map`
- `uptr`
- `sptr`
- `lambda`
- `fore`
- `ctor`
- `guard`
- `ns`
- `cout`
- `cerr`
- `try`
- `assert`
- `nodiscard`

### Example
Type:

```cpp
uptr
```

Then press **Tab** to expand a `std::unique_ptr` pattern more quickly.

## 8.5 Best way to learn snippets

Pick just **three** for your language and use them for a week.

Good starter sets:

- **C#**: `class`, `prop`, `ctor`
- **C**: `main`, `fori`, `guard`
- **C++**: `class`, `template`, `uptr`

---

# 9. Git & Source Control

pfpad has built-in Git support, and it is friendly for day-to-day work.

## 9.1 Open the Git panel
Use:
- **View → Panels → Git Panel**

You can also use the sidebar if it is already visible.

## 9.2 What you can do there

In the Git panel you can:
- see changed files
- stage files by clicking them
- write a commit message
- commit
- fetch
- pull
- push

The UI stays responsive during fetch/pull/push because those actions are asynchronous.

## 9.3 Branch awareness

Your current branch is shown in the **status bar**.

That means you can quickly confirm whether you're on `main`, `feature/foo`, or the wrong branch before committing.

## 9.4 Typical beginner Git flow in pfpad

1. Open the project folder.
2. Open **View → Panels → Git Panel**.
3. Edit files.
4. Stage files by clicking them.
5. Type a commit message.
6. Commit.
7. Use **Push**.

## 9.5 Workspace file colors

In the **Workspace** panel, files can show Git status colors.

This is useful because you can glance at the tree and immediately see what changed.

Also helpful: the workspace respects **.gitignore** automatically.

---

# 10. For C# Developers

If you write C#, pfpad gives you the smoothest language-specific experience of the three.

## 10.1 Roslyn support

For C#, pfpad uses **Roslyn** for navigation.

This matters because it improves features like:
- **Go to Definition**
- code understanding
- symbol-aware navigation

Use:
- **F12** or **Ctrl+Click** for **Go to Definition**

## 10.2 A simple C# workflow

1. Open the folder containing your `.csproj` or `.sln`.
2. Open the **Workspace** panel.
3. Open a `.cs` file.
4. Use **F12** on a class or method.
5. Press **Ctrl+`** and run:

```bash
dotnet build
```

6. Run with:

```bash
dotnet run
```

## 10.3 Set a C# profile

Open:
- **Tools → User Profiles**

Set:
- `BuildCommand = dotnet build`
- `RunCommand = dotnet run`

This gives you a predictable workspace setup.

## 10.4 Formatting and snippets

Useful shortcuts and snippets for C# work:

- Format document: **Ctrl+Shift+I**
- Snippets: `class`, `ctor`, `prop`, `foreach`, `try`, `main`, `console`, `cw`

## 10.5 Debugging C# with netcoredbg

### What you need
pfpad requires **netcoredbg** for C# debugging.

Download it from:
- https://github.com/Samsung/netcoredbg

### Debug keys
- Start debug: **F5**
- Stop: **Shift+F5**
- Run without debug: **Ctrl+F5**
- Step over: **F10**
- Step into: **F11**
- Step out: **Shift+F11**
- Toggle breakpoint: **Ctrl+F9** or click in the gutter

### Conditional breakpoints
Right-click in the gutter and choose:
- **New Breakpoint with Properties**

This is useful when a line runs many times and you only want to stop under a certain condition.

### During a debug session
When debugging starts, pfpad shows:
- **Variables panel**
- **Call Stack panel**

These help answer:
- "What value does this variable have right now?"
- "How did execution get here?"

## 10.6 C# example session

Imagine you have a console app.

1. Open the folder.
2. Open `Program.cs`.
3. Add a breakpoint with **Ctrl+F9**.
4. Press **F5**.
5. When execution stops, inspect **Variables** and **Call Stack**.
6. Use **F10** to walk through the next line.

If you are new to debugging, this is a great first exercise.

---

# 11. For C Developers

pfpad works well for C when you give it a clear build command and, optionally, better symbol tooling.

## 11.1 Set up your build profile

Open:
- **Tools → User Profiles**

Then set a workspace build command.

Common choices:

### Single-folder C project
- `BuildCommand = gcc -o app *.c`
- `RunCommand = .\app.exe`

### Make-based project
- `BuildCommand = make`
- `RunCommand = .\app.exe`

If your executable has a different name, use that instead.

## 11.2 Use snippets for faster C coding

Good starter snippets:
- `main`
- `fori`
- `printf`
- `guard`
- `malloc_free`

Example:

1. Open `main.c`
2. Type `main`
3. Press **Tab**
4. Fill in the generated structure

## 11.3 Navigate symbols more effectively with ctags

For C and C++, pfpad uses a **ctags index** for stronger symbol navigation if `ctags` is installed.

Install from:
- https://github.com/universal-ctags/ctags

Then put `ctags.exe` on your **PATH**.

pfpad uses it automatically.

After that, you can use:
- **F12** or **Ctrl+Click** for **Go to Definition**
- **View → Panels → Symbols**
- **View → Panels → Outline**

## 11.4 Header/source switching

For C projects with `.c` and `.h` files, use:
- **Alt+O**

pfpad will try to toggle between matching files.

It works in:
- the same directory
- `include/`
- `src/`

## 11.5 Debugging C on Windows

pfpad supports C/C++ debugging with:
- **cppvsdbg** from Visual Studio Build Tools, or
- **gdb** from MinGW/MSYS2

Configure it in:
- **Settings → Debug Adapter**

Choose:
- adapter type
- adapter path

Debug keys stay the same:
- **F5** start
- **F10** step over
- **F11** step into
- **Shift+F11** step out

## 11.6 GDB setup example

A common Windows path is:

1. Install MSYS2
2. Install GDB:

```bash
pacman -S mingw-w64-ucrt-x86_64-gdb
```

3. In pfpad, open **Settings → Debug Adapter**
4. Select **gdb**
5. Point pfpad to the GDB executable
6. Start debugging with **F5**

## 11.7 C example workflow

1. Open your C project folder.
2. Open **Tools → User Profiles**.
3. Set `BuildCommand = gcc -o app *.c`.
4. Build from the terminal or profile.
5. Install `ctags.exe` for better navigation.
6. Use **Alt+O** to move between header/source files.
7. Configure **Settings → Debug Adapter** when you're ready to debug.

---

# 12. For C++ Developers

pfpad is a practical fit for C++ projects, especially when you combine profiles, snippets, header/source switching, and ctags.

If you are coming from a clangd-based editor, one important expectation to set is this: **pfpad's built-in C/C++ navigation is centered on ctags, not clangd**. So the first upgrade for C++ navigation in pfpad is usually **installing ctags.exe**.

## 12.1 Set up a C++ profile

Open:
- **Tools → User Profiles**

Typical options:

### CMake build folder workflow
- `BuildCommand = cmake --build build`
- `RunCommand = .\build\app.exe`

### Preset workflow
- `BuildCommand = cmake --preset debug`

You can also build directly in the integrated terminal with **Ctrl+`**.

## 12.2 Switch between header and source files

Use:
- **Alt+O**

pfpad will switch between:
- `.cpp` and `.hpp`
- `.c` and `.h`

It checks:
- the same directory
- `include/`
- `src/`

This is one of the nicest quality-of-life features for C and C++ work.

## 12.3 C++ snippets worth learning

Strong starter snippets:
- `class`
- `template`
- `ctor`
- `lambda`
- `vec`
- `map`
- `uptr`
- `sptr`
- `ns`
- `cout`
- `cerr`
- `nodiscard`

Example:

1. In a `.hpp` file, type `template`
2. Press **Tab**
3. Fill in the type parameters and declaration

Or:

1. Type `ns`
2. Press **Tab**
3. Generate a namespace skeleton quickly

## 12.4 Symbol navigation for C++

Use:
- **F12** or **Ctrl+Click** for **Go to Definition**
- **View → Panels → Symbols**
- **View → Panels → Outline**

For best results, install **ctags**:
- https://github.com/universal-ctags/ctags

Put `ctags.exe` on PATH and pfpad will pick it up automatically.

## 12.5 About clangd

Many C++ developers expect `clangd` because other editors use it heavily.

In pfpad, the practical equivalent to enable first is:
- **ctags for navigation**
- **profiles for build/run commands**
- **gdb or cppvsdbg for debugging**

So if you were planning a "clangd setup day," your pfpad version of that is usually:

1. Install `ctags.exe`
2. Configure build commands in **Tools → User Profiles**
3. Configure debug adapter in **Settings → Debug Adapter**

## 12.6 Debugging C++

You need one of these:
- **cppvsdbg** from Visual Studio Build Tools
- **gdb** from MinGW/MSYS2

Configure in:
- **Settings → Debug Adapter**

Then use the normal debug keys:
- **F5** start
- **Shift+F5** stop
- **F10** step over
- **F11** step into
- **Shift+F11** step out

## 12.7 C++ example workflow

1. Open your project folder.
2. Set `BuildCommand = cmake --build build` in **Tools → User Profiles**.
3. Open a `.cpp` file.
4. Use **Alt+O** to jump to the header.
5. Use a snippet like `class` or `template`.
6. Build with **Ctrl+`**.
7. Install `ctags.exe` if symbol navigation feels too limited.
8. Configure **Settings → Debug Adapter** when you want to debug.

---

# 13. Keyboard Shortcuts Reference

## 13.1 Core editing

| Action | Shortcut |
|---|---|
| New tab | Ctrl+T |
| Close tab | Ctrl+W |
| Open file | Ctrl+O |
| Open folder | Ctrl+Shift+O |
| Clone repository | Ctrl+Shift+C |
| Save | Ctrl+S |
| Save all | Ctrl+Alt+S |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Find | Ctrl+F |
| Replace | Ctrl+H |
| Global search | Ctrl+Shift+F |
| Go to line | Ctrl+G |
| Format document | Ctrl+Shift+I |
| Command palette | Ctrl+Shift+P |
| Zoom in | Ctrl+Plus |
| Zoom out | Ctrl+Minus |
| Integrated terminal | Ctrl+` |

## 13.2 Splits and bookmarks

| Action | Shortcut |
|---|---|
| Split vertical | Ctrl+Shift+V |
| Split horizontal | Ctrl+Alt+H |
| Close split | Ctrl+Shift+W |
| Toggle bookmark | Ctrl+B |
| Next bookmark | Ctrl+Shift+Period |
| Previous bookmark | Ctrl+Shift+Comma |
| Header/source toggle | Alt+O |

## 13.3 Debugging

| Action | Shortcut |
|---|---|
| Start debugging | F5 |
| Run without debugging | Ctrl+F5 |
| Stop debugging | Shift+F5 |
| Step over | F10 |
| Step into | F11 |
| Step out | Shift+F11 |
| Toggle breakpoint | Ctrl+F9 |
| Go to definition | F12 |

## 13.4 Vim mode essentials

| Action | Keys |
|---|---|
| Enable Vim mode | View → Display → Vim Mode |
| Move left/down/up/right | h / j / k / l |
| Next / previous word | w / b |
| Delete line | dd |
| Copy line | yy |
| Paste after / before | p / P |
| Undo / redo | u / Ctrl+R |
| Visual char / line / block | v / V / Ctrl+V |
| Save | :w |
| Quit / close split | :q |
| Save and quit | :wq |
| Vertical split | :vsp |
| Horizontal split | :sp |
| Close split | :close |
| Search forward / back | / / ? |
| Next / previous match | n / N |

---

# 14. Troubleshooting & FAQ

## 14.1 "I opened a folder, but I don't see files"
Open:
- **View → Panels → Workspace**

If needed, reopen the folder with:
- **Ctrl+Shift+O**

## 14.2 "Find only works in one file"
Use:
- **Ctrl+Shift+F** for project-wide search

Use:
- **Ctrl+F** for only the current file

## 14.3 "F12 is not finding definitions in my C or C++ project"
Install **ctags** and put `ctags.exe` on PATH:
- https://github.com/universal-ctags/ctags

pfpad uses it automatically for C/C++.

## 14.4 "Debugging won't start in C#"
Make sure **netcoredbg** is installed:
- https://github.com/Samsung/netcoredbg

Then try again with **F5**.

## 14.5 "Debugging won't start in C or C++"
Open:
- **Settings → Debug Adapter**

Then confirm:
- adapter type is correct
- adapter path is correct
- you installed **cppvsdbg** or **gdb**

## 14.6 "My build command is different for each project"
That is normal.

Use:
- **Tools → User Profiles**

Set `BuildCommand` and `RunCommand` per workspace.

## 14.7 "I accidentally turned on Vim mode"
No problem.

Turn it off at:
- **View → Display → Vim Mode**

## 14.8 "I want a bigger/smaller editor"
Use:
- **Ctrl+Plus** to zoom in
- **Ctrl+Minus** to zoom out

Or change the font in:
- **Ctrl+,**
- **Editor → Font**

## 14.9 "What should I learn first?"
A great beginner path is:

1. **Ctrl+Shift+O** to open a folder
2. **Ctrl+O** to open files
3. **Ctrl+S** to save
4. **Ctrl+F / Ctrl+H** to search and replace
5. **Ctrl+`** to use the terminal
6. **Ctrl+Shift+P** to discover commands
7. **F12** to navigate definitions

That is enough to become comfortable quickly.

## 14.10 "Do I need every feature right now?"
No.

Start simple. Use the editor, open a project, build from the terminal, and save your work. Add snippets, debugging, Git, or Vim mode only when they become useful.

pfpad is forgiving about growing with you.

---

## Final Advice

If you're an experienced developer but new to pfpad, the best approach is:

- learn **five shortcuts**
- set up **one profile**
- install **one debugger**
- use **one or two snippets**

That is enough to make pfpad feel like your editor instead of somebody else's.

And if something feels unfamiliar at first, don't worry — that's normal. A good editor should reward curiosity, not punish it. pfpad gives you room to learn at your own pace.

---

# 13. Security Hardening

pfpad includes a **built-in, graded runtime security hardening system** accessible from
**Settings → Security → Security Profile**. You do not need to be a security expert — the
system is designed to be progressive and forgiving.

## The Four Profiles

| Profile | What it means for you |
|---------|-----------------------|
| **Not Hardened** | Zero enforcement. Everything works, nothing is blocked. Use for trusted local dev only. |
| **Low** *(default)* | Blocks dangerous URI schemes (javascript:, bscript:, ms-msdt:). Zero friction otherwise. |
| **Mid** | Encrypts settings.json with your Windows account (DPAPI). Protects API keys and tokens at rest. |
| **Max** | Adds HTTPS-only for all links and AIOps connectors. For regulated or high-security environments. |

## Changing Your Profile

1. Open **Settings** (Ctrl+,) → navigate to **Security**
2. Select the desired profile with the radio buttons
3. Click **OK**
4. A **transition wizard** opens and shows exactly what will happen
5. Click **Upgrade** (or **Downgrade**) to proceed, or **Cancel** to stay where you are

## The Transition Wizard

The wizard never changes anything without your confirmation. It shows:

- Each migration step as a plain-English description
- Live status icons as steps run: ⬜ pending → ⏳ running → ✅ done / ⚠️ warning / ❌ failed
- Automatic rollback if a step fails — your original state is always restored
- A backup of any file it modifies before touching it

**You can always cancel.** If you cancel after a step has already run, the wizard rolls back
the completed steps automatically.

## Practical Examples

**Upgrading to Mid (encrypting your settings):**

The wizard backs up settings.json → settings.json.bak, then encrypts the live file with
DPAPI. If encryption fails for any reason, the backup is restored. On success, the wizard
auto-closes after 1.5 seconds — no action required.

**"I can't read settings.json any more after upgrading to Mid":**

This is expected. Use **Help > About → About tab → Open settings.json** — if the file is
encrypted, pfpad offers to export a readable copy to your Desktop. Or simply downgrade to
Low: the wizard decrypts the file back to plain JSON automatically.

**"A link stopped working after upgrading to Max":**

The status bar will tell you exactly why:
*"🔒 Link blocked (http:// links are blocked at Max — use https://). Change profile in
Settings → Security."*

Either switch the endpoint to https://, or temporarily drop to Mid for that session.

## Build-Time Status Indicators

The Security panel also shows read-only indicators that reflect how the binary you are running
was built:

- **Code signing** — whether the EXE has an Authenticode signature
- **CI security gates** — whether the pipeline has Wiz or CodeQL scanning active
- **Installer signing** — whether the Inno Setup installer has a SignTool directive

These are informational only and cannot be changed at runtime. ❌ indicators are normal for
personal development machines.

## Full Documentation

For complete details, transition behaviour, FAQ, and build-time status explanation, see
**Help > Manual → 🔒 Security Hardening** tab inside the app.

---
