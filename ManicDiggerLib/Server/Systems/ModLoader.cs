using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace ManicDigger;

/// <summary>
/// Server system responsible for discovering, compiling, and starting C# mods
/// found in the active game mode's <c>Mods</c> directory.
/// <para>
/// Compilation is performed at runtime via Roslyn. All scripts are first compiled
/// together into a single assembly; if that fails, each script is retried
/// individually so that one broken mod does not prevent others from loading.
/// </para>
/// <para>
/// Mods may declare dependencies via <see cref="IModManager.required"/> during
/// <see cref="IMod.PreStart"/>. The loader respects those dependencies and starts
/// mods in topological order.
/// </para>
/// </summary>
public class ServerSystemModLoader : ServerSystem
{
    /// <summary>All successfully compiled and instantiated mods, keyed by type name.</summary>
    private readonly Dictionary<string, IMod> mods = [];

    /// <summary>
    /// Dependency lists captured from each mod's <see cref="IMod.PreStart"/> call,
    /// keyed by mod type name.
    /// </summary>
    private readonly Dictionary<string, string[]> modRequirements = [];

    /// <summary>Tracks which mods have been started to prevent double-starting during dependency resolution.</summary>
    private readonly Dictionary<string, bool> loadedMods = [];

    private static readonly string[] ExtraAssemblyReferences = ["ScriptingApi.dll"];

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize(Server server)
    {
        LoadMods(server, restart: false);
    }

    /// <inheritdoc/>
    public override bool OnCommand(Server server, int sourceClientId, string command, string argument)
    {
        if (command == "mods")
        {
            RestartMods(server, sourceClientId);
            return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Mod restart (live reload)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reloads all mods at runtime without restarting the server process.
    /// Resets all mod event handlers and re-runs <see cref="ServerSystem.OnRestart"/>
    /// on every active system before recompiling.
    /// </summary>
    /// <param name="server">The running server instance.</param>
    /// <param name="sourceClientId">The client ID of the operator who issued the command.</param>
    /// <returns><c>true</c> if the caller had sufficient privileges and the reload was initiated.</returns>
    public bool RestartMods(Server server, int sourceClientId)
    {
        if (!server.PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.restart))
        {
            server.SendMessage(sourceClientId, string.Format(
                server.language.Get("Server_CommandInsufficientPrivileges"), server.colorError));
            return false;
        }

        ClientOnServer caller = server.GetClient(sourceClientId);
        server.SendMessageToAll(string.Format(
            server.language.Get("Server_CommandRestartModsSuccess"),
            server.colorImportant, caller.ColoredPlayername(server.colorImportant)));
        server.ServerEventLog($"{caller.playername} restarts mods.");

        server.modEventHandlers = new ModEventHandlers();
        for (int i = 0; i < server.systemsCount; i++)
        {
            if (server.systems[i] != null)
                server.systems[i].OnRestart(server);
        }

        LoadMods(server, restart: true);
        return true;
    }

    // -------------------------------------------------------------------------
    // Mod loading pipeline
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs the full mod loading pipeline: initialises the mod manager, discovers
    /// script sources, compiles them, and starts all loaded mods.
    /// </summary>
    /// <param name="restart">
    /// When <c>true</c>, compilation is skipped (JavaScript-only reload path).
    /// </param>
    private void LoadMods(Server server, bool restart)
    {
        server.modManager = new ModManager1();
        var manager = server.modManager;
        manager.Start(server);

        var scripts = GetScriptSources(server);
        Console.WriteLine($"[ModLoader] GetScriptSources returned {scripts.Count} scripts:");
        foreach (var k in scripts)
            Console.WriteLine($"  '{k.Key}' ({k.Value.Length} chars) - is .js: {k.Key.EndsWith(".js")}");

        CompileScripts(scripts, restart);
        Console.WriteLine($"[ModLoader] After CompileScripts, mods.Count = {mods.Count}");

        StartMods(manager, manager.required);
    }

    // -------------------------------------------------------------------------
    // Script discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans the known mod directories for <c>.cs</c> and <c>.js</c> files
    /// belonging to the active game mode and returns their contents keyed by filename.
    /// <para>
    /// The active game mode is read from <c>current.txt</c> in the mod directory.
    /// If that file does not exist it is created with the server's current game mode.
    /// </para>
    /// </summary>
    private Dictionary<string, string> GetScriptSources(Server server)
    {
        string assemblyDir = Path.GetDirectoryName(GetType().Assembly.Location)!;

        string[] modPaths =
        [
            Path.Combine(assemblyDir, "..", "..", "..", "..", "ManicDiggerLib", "Server", "Mods"),
            Path.Combine(assemblyDir, "Mods")
        ];

        foreach (string modPath in modPaths)
            Console.WriteLine($"[ModLoader] Checking modpath: {Path.GetFullPath(modPath)} - exists: {Directory.Exists(modPath)}");

        // Resolve the active game mode from / into each path
        for (int i = 0; i < modPaths.Length; i++)
        {
            string currentTxt = Path.Combine(modPaths[i], "current.txt");
            if (File.Exists(currentTxt))
            {
                server.gameMode = File.ReadAllText(currentTxt).Trim();
            }
            else if (Directory.Exists(modPaths[i]))
            {
                try { File.WriteAllText(currentTxt, server.gameMode); }
                catch { }
            }
            modPaths[i] = Path.Combine(modPaths[i], server.gameMode);
        }

        var scripts = new Dictionary<string, string>();
        foreach (string modPath in modPaths)
        {
            if (!Directory.Exists(modPath)) continue;
            server.ModPaths.Add(modPath);

            foreach (string file in Directory.GetFiles(modPath))
            {
                string ext = Path.GetExtension(file);
                if (!GameStorePath.IsValidName(Path.GetFileNameWithoutExtension(file))) continue;
                if (!ext.Equals(".cs", StringComparison.InvariantCultureIgnoreCase) &&
                    !ext.Equals(".js", StringComparison.InvariantCultureIgnoreCase)) continue;

                scripts[new FileInfo(file).Name] = File.ReadAllText(file);
            }
        }
        return scripts;
    }

    // -------------------------------------------------------------------------
    // Compilation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compiles the discovered scripts using Roslyn.
    /// <list type="bullet">
    ///   <item>On restart, compilation is skipped (JavaScript-only path).</item>
    ///   <item>All scripts are first compiled together into a single assembly.</item>
    ///   <item>If combined compilation fails, each script is retried individually
    ///         so that one broken mod does not prevent others from loading.</item>
    /// </list>
    /// Successfully compiled assemblies are passed to <see cref="RegisterModsFromAssembly"/>.
    /// </summary>
    public void CompileScripts(Dictionary<string, string> scripts, bool restart)
    {
        if (restart) return; // JavaScript-only reload; nothing to compile

        var references = BuildMetadataReferences();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        if (TryCompileScripts(scripts, references, parseOptions,
                assemblyName: "ManicDiggerMods",
                out Assembly? combined, out _))
        {
            RegisterModsFromAssembly(combined!);

            // ── Release Roslyn syntax trees from memory ───────────────────────
            references.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return;
        }

        Console.WriteLine("[ModLoader] Combined compilation failed, falling back to per-script compilation.");

        foreach (var script in scripts)
        {
            if (!TryCompileScripts(
                    new Dictionary<string, string> { { script.Key, script.Value } },
                    references, parseOptions,
                    assemblyName: "Mod_" + Path.GetFileNameWithoutExtension(script.Key),
                    out Assembly? modAssembly,
                    out IEnumerable<Diagnostic> diagnostics))
            {
                string errors = string.Join("\n", diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"{d.Id} Line:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.GetMessage()}"));

                TryShowMessageBox($"Can't load mod: {script.Key}\n{errors}");
                continue;
            }

            if (modAssembly != null)
                RegisterModsFromAssembly(modAssembly);
        }
    }

    /// <summary>
    /// Builds the list of <see cref="MetadataReference"/> objects required by mod scripts.
    /// Includes all non-dynamic assemblies currently loaded in the <see cref="AppDomain"/>,
    /// plus a set of additional assemblies that mods commonly depend on.
    /// </summary>
    private List<MetadataReference> BuildMetadataReferences()
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var parallelAsm = typeof(Parallel).Assembly;
        if (!string.IsNullOrEmpty(parallelAsm.Location))
            references.Add(MetadataReference.CreateFromFile(parallelAsm.Location));

        string assemblyDir = Path.GetDirectoryName(GetType().Assembly.Location)!;

        foreach (string asmName in ExtraAssemblyReferences)
        {
            string localPath = Path.Combine(assemblyDir, asmName);
            if (File.Exists(localPath))
            {
                references.Add(MetadataReference.CreateFromFile(localPath));
                Console.WriteLine($"[ModLoader] Added reference: {localPath}");
                continue;
            }

            // Fall back to an already-loaded assembly in the AppDomain
            string nameWithoutExt = Path.GetFileNameWithoutExtension(asmName);
            Assembly? existing = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == nameWithoutExt);

            if (existing != null && !string.IsNullOrEmpty(existing.Location))
            {
                references.Add(MetadataReference.CreateFromFile(existing.Location));
                Console.WriteLine($"[ModLoader] Added reference from AppDomain: {existing.Location}");
                continue;
            }

            Console.WriteLine($"[ModLoader] WARNING: Could not find reference: {asmName}");
        }

        return references;
    }

    /// <summary>
    /// Compiles a set of scripts into an in-memory assembly using Roslyn.
    /// A set of implicit global usings is injected automatically.
    /// </summary>
    /// <param name="scripts">Source files, keyed by filename.</param>
    /// <param name="references">Metadata references to include in the compilation.</param>
    /// <param name="parseOptions">Roslyn parse options (language version etc.).</param>
    /// <param name="assemblyName">Name of the resulting in-memory assembly.</param>
    /// <param name="assembly">The loaded assembly on success; <c>null</c> on failure.</param>
    /// <param name="diagnostics">Warnings and errors produced during compilation.</param>
    /// <returns><c>true</c> if compilation succeeded; <c>false</c> otherwise.</returns>
    private static bool TryCompileScripts(
        Dictionary<string, string> scripts,
        List<MetadataReference> references,
        CSharpParseOptions parseOptions,
        string assemblyName,
        out Assembly? assembly,
        out IEnumerable<Diagnostic> diagnostics)
    {
        // Work on a local copy so we don't mutate the caller's dictionary
        var allSources = new Dictionary<string, string>(scripts)
        {
            ["GlobalUsings.cs"] = """
                global using System;
                global using System.Collections.Generic;
                global using System.Drawing;
                global using System.IO;
                global using System.Linq;
                global using System.Text;
                global using System.Threading;
                global using System.Threading.Tasks;
                """
        };

        Console.WriteLine($"[Roslyn] Script count: {allSources.Count}");
        foreach (var k in allSources)
        {
            Console.WriteLine($"[Roslyn] Script '{k.Key}': {k.Value.Length} chars");
            Console.WriteLine($"[Roslyn] First 200 chars: {k.Value[..Math.Min(200, k.Value.Length)]}");
        }

        var syntaxTrees = allSources
            .Select(k => CSharpSyntaxTree.ParseText(k.Value, parseOptions, path: k.Key, Encoding.UTF8))
            .ToList();

        Console.WriteLine($"[Roslyn] Syntax trees: {syntaxTrees.Count}");
        foreach (var tree in syntaxTrees)
            Console.WriteLine($"[Roslyn] Tree '{tree.FilePath}': {tree.GetRoot().DescendantNodes().Count()} nodes");

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release);

        var compilation = CSharpCompilation.Create(assemblyName, syntaxTrees, references, compilationOptions);

        var ms = new MemoryStream();

#if DEBUG
        var pdbStream = new MemoryStream();
        var emitOptions = new Microsoft.CodeAnalysis.Emit.EmitOptions(
            debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb);
        var result = compilation.Emit(ms, pdbStream, options: emitOptions);
#else
        var result = compilation.Emit(ms);
#endif

        // Free the syntax trees — compilation holds the full AST in memory
        // and Roslyn won't release it until the compilation object is collected.
        syntaxTrees.Clear();
        compilation = null!;  // allow GC to collect the AST

        foreach (var diag in result.Diagnostics)
        {
            Console.WriteLine($"[Roslyn] {diag.Severity} {diag.Id}: {diag.GetMessage()} " +
                              $"(line {diag.Location.GetLineSpan().StartLinePosition.Line + 1})");
        }
        Console.WriteLine($"[Roslyn] Emit success: {result.Success}, stream length: {ms.Length}");

        diagnostics = result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning);

        if (!result.Success)
        {
            assembly = null;
            return false;
        }

        var loadContext = new AssemblyLoadContext(name: "ModLoader", isCollectible: true);
        ms.Seek(0, SeekOrigin.Begin);

#if DEBUG
        pdbStream.Seek(0, SeekOrigin.Begin);
        assembly = loadContext.LoadFromStream(ms, pdbStream);
        pdbStream.Dispose();
#else
        assembly = loadContext.LoadFromStream(ms);
#endif

        ms.Dispose();

        // Explicitly clear parse trees before returning
        // so the load context doesn't root them
        foreach (var tree in syntaxTrees)
            ((CSharpSyntaxTree)tree).GetRoot(); // force lazy evaluation then release
        syntaxTrees.Clear();

        Console.WriteLine($"[Roslyn] Assembly name: {assembly.FullName}");
        Console.WriteLine($"[Roslyn] Types: {assembly.GetTypes().Length}");
        return true;
    }

    /// <summary>
    /// Scans an assembly for types implementing <see cref="IMod"/> and registers
    /// each instantiated mod in <see cref="mods"/>.
    /// </summary>
    private void RegisterModsFromAssembly(Assembly assembly)
    {
        foreach (Type t in assembly.GetTypes())
        {
            if (typeof(IMod).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            {
                mods[t.Name] = (IMod)Activator.CreateInstance(t)!;
                Console.WriteLine($"Loaded mod: {t.Name}");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Mod startup (dependency-ordered)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calls <see cref="IMod.PreStart"/> on all mods to collect dependency
    /// declarations, then starts each mod in dependency order.
    /// </summary>
    /// <param name="manager">The mod manager passed to each mod.</param>
    /// <param name="currentRequires">
    /// The dependency list populated by the mod manager during <see cref="IMod.PreStart"/>.
    /// </param>
    private void StartMods(IModManager manager, List<string> currentRequires)
    {
        modRequirements.Clear();
        loadedMods.Clear();

        foreach (var k in mods)
        {
            k.Value.PreStart(manager);
            modRequirements[k.Key] = currentRequires.ToArray();
            currentRequires.Clear();
        }

        foreach (var k in mods)
            StartModWithDependencies(k.Key, k.Value, manager);
    }

    /// <summary>
    /// Starts a single mod, recursively ensuring all of its declared dependencies
    /// are started first. Already-started mods are skipped.
    /// </summary>
    private void StartModWithDependencies(string name, IMod mod, IModManager manager)
    {
        if (loadedMods.ContainsKey(name)) return;

        if (modRequirements.TryGetValue(name, out string[]? requirements))
        {
            foreach (string dependency in requirements)
            {
                if (!mods.TryGetValue(dependency, out IMod? depMod))
                {
                    TryShowMessageBox($"Can't load mod {name} because its dependency {dependency} couldn't be loaded.");
                    continue;
                }
                StartModWithDependencies(dependency, depMod, manager);
            }
        }

        mod.Start(manager);
        loadedMods[name] = true;
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to display a message box, falling back to <see cref="Console.WriteLine"/>
    /// on headless servers where no display is available.
    /// </summary>
    private static void TryShowMessageBox(string message)
    {
        try { MessageBox.Show(message); }
        catch { Console.WriteLine($"[Mod error] {message}"); }
    }
}