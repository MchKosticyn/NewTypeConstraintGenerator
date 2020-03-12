using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Linq;
using VSharp.Interpreter;

namespace NewTypeConstraintGenerator
{
    public class DumpStackTraceListener : TraceListener
    {
        public override void Write(string message)
        {
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public override void Fail(string message)
        {
            Fail(message, String.Empty);
        }

        public override void Fail(string message1, string message2)
        {
            Console.WriteLine("ASSERT FAILED");
            Console.WriteLine("{0}: {1}", message1, message2);
            Console.WriteLine("Stack Trace:");

            StackTrace trace = new StackTrace( true );
            StackFrame[] stackFrames = trace.GetFrames();
            if (stackFrames != null)
            {
                foreach (StackFrame frame in stackFrames)
                {
                    MethodBase frameClass = frame.GetMethod();
                    Console.WriteLine("  {2}.{3} {0}:{1}",
                        frame.GetFileName(),
                        frame.GetFileLineNumber(),
                        frameClass.DeclaringType,
                        frameClass.Name);
                }
            }
        }
    }

    public sealed class Generator
    {

        private const string TestsDirectoryName = "Tests";

        private static string _currentTestDirectory = "";

        private static Assembly LoadFromTestFolder(object sender, ResolveEventArgs args)
        {
            // This handler is called only when the common language runtime tries to bind to the assembly and fails.
            string name = new AssemblyName(args.Name).Name;
            string additionalPath = _currentTestDirectory + Path.DirectorySeparatorChar + name + ".dll";
            if (File.Exists(additionalPath))
            {
                return Assembly.LoadFrom(additionalPath);
            }

            return null;
        }

        public static void Generate()
        {
            Trace.Listeners.Add(new DumpStackTraceListener());
            Trace.Listeners.Add(new DumpStackTraceListener());

            CultureInfo ci = new CultureInfo("en-GB");
            ci.NumberFormat.PositiveInfinitySymbol = "Infinity";
            ci.NumberFormat.NegativeInfinitySymbol = "-Infinity";
            Thread.CurrentThread.CurrentCulture = ci;

            var ignoredLibs = new List<string>
            {
            };

            var ignoredTypes = new List<string>
            {
            };

            var whiteTypes = new List<string>
            {
            };

            string pathToTests = Path.Combine(Path.GetFullPath("."), "..", "..", "..", TestsDirectoryName);
            string[] tests = Directory.GetDirectories(pathToTests);
            foreach (string testDir in tests)
            {
                string[] libEntries = Directory.GetFiles(testDir);
                //var paths = new[] {"NewTypeConstraintGenerator.dll", "mscorlib.dll"};
                //var context = new MetadataLoadContext(new PathAssemblyResolver(paths));
                //Console.WriteLine(context.CoreAssembly);

                foreach (string lib in libEntries)
                {
                    if (!lib.EndsWith(".dll", StringComparison.Ordinal) || ignoredLibs.Exists(i => lib.EndsWith(i)))
                    {
                        continue;
                    }

                    _currentTestDirectory = testDir;
                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.AssemblyResolve += LoadFromTestFolder;



                    IDictionary<MethodInfo, string /*IEnumerable<API.Term>*/> got =
                        SVM.Run( Assembly.LoadFrom(lib), /*context.LoadFromAssemblyPath(lib),*/
                            ignoredTypes, whiteTypes);

                    foreach (var str in got.Values)
                    {
                        Console.WriteLine(str);
                    }
                }
            }


        }
    }

    internal static class Program
    {
        public static void Main(string[] args)
        {
            Generator.Generate();
        }
    }
}