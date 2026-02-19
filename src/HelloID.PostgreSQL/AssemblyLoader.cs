using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HelloID.PostgreSQL
{
    internal static class AssemblyLoader
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();
        private static string _tempDir;
        private static readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] _expectedDlls = new[]
        {
            "Npgsql.dll",
            "Microsoft.Extensions.Logging.Abstractions.dll",
            "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
            "Microsoft.Extensions.Options.dll",
            "Microsoft.Extensions.Primitives.dll",
            "System.Threading.Tasks.Extensions.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Memory.dll",
            "Microsoft.Bcl.AsyncInterfaces.dll",
            "System.Text.Json.dll",
            "System.Buffers.dll",
            "System.Numerics.Vectors.dll",
            "System.Threading.Channels.dll",
            "System.Diagnostics.DiagnosticSource.dll"
        };

        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _tempDir = Path.Combine(Path.GetTempPath(), "HelloID.PostgreSQL", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(_tempDir);

                    ExtractEmbeddedAssemblies();

                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                    LoadNpgsql();

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

        private static void LoadNpgsql()
        {
            var npgsqlPath = Path.Combine(_tempDir, "Npgsql.dll");
            if (!File.Exists(npgsqlPath))
            {
                throw new FileNotFoundException($"Npgsql.dll not found at: {npgsqlPath}");
            }

            var npgsqlAssembly = Assembly.LoadFrom(npgsqlPath);
            _loadedAssemblies["Npgsql"] = npgsqlAssembly;
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
