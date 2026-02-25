using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SQLite.Wrapper
{
    internal static class AssemblyLoader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static bool _initialized = false;
        private static readonly object _lock = new object();
        private static string _tempDir;
        private static readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] _expectedDlls = new[]
        {
            "Microsoft.Data.Sqlite.dll",
            "SQLitePCLRaw.batteries_v2.dll",
            "SQLitePCLRaw.core.dll",
            "SQLitePCLRaw.provider.e_sqlite3.dll",
            "System.Memory.dll",
            "System.Buffers.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Numerics.Vectors.dll"
        };

        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _tempDir = Path.Combine(Path.GetTempPath(), "SQLite.Wrapper", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(_tempDir);

                    ExtractEmbeddedAssemblies();
                    SetupNativeLibraryPath();

                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                    LoadMicrosoftDataSqlite();

                    _initialized = true;
                }
                catch
                {
                    Cleanup();
                    throw;
                }
            }
        }

        private static void ExtractEmbeddedAssemblies()
        {
            var assembly = typeof(AssemblyLoader).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var expectedDll in _expectedDlls)
            {
                var resourceName = resourceNames.FirstOrDefault(n => 
                    n.EndsWith(expectedDll, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                {
                    throw new FileNotFoundException($"Embedded resource not found: {expectedDll}. Available: {string.Join(", ", resourceNames)}");
                }

                var filePath = Path.Combine(_tempDir, expectedDll);

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException($"Failed to get stream for: {resourceName}");
                    }

                    using (var fileStream = File.Create(filePath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }

        private static void SetupNativeLibraryPath()
        {
            var assembly = typeof(AssemblyLoader).Assembly;
            var wrapperLocation = Path.GetDirectoryName(assembly.Location);
            
            if (string.IsNullOrEmpty(wrapperLocation))
            {
                return;
            }

            var nativeDir = Path.Combine(wrapperLocation, "runtimes", "win-x64", "native");

            if (Directory.Exists(nativeDir))
            {
                SetDllDirectory(nativeDir);
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            var simpleName = assemblyName.Name;

            if (_loadedAssemblies.TryGetValue(simpleName, out var cached))
            {
                return cached;
            }

            var possibleFiles = new[]
            {
                $"{simpleName}.dll",
                $"{simpleName}.exe"
            };

            foreach (var file in possibleFiles)
            {
                var filePath = Path.Combine(_tempDir, file);
                if (File.Exists(filePath))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(filePath);
                        _loadedAssemblies[simpleName] = assembly;
                        return assembly;
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static void LoadMicrosoftDataSqlite()
        {
            var dllPath = Path.Combine(_tempDir, "Microsoft.Data.Sqlite.dll");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Microsoft.Data.Sqlite.dll not found at: {dllPath}");
            }

            var sqlAssembly = Assembly.LoadFrom(dllPath);
            _loadedAssemblies["Microsoft.Data.Sqlite"] = sqlAssembly;
        }

        private static void Cleanup()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
            }
        }
    }
}
