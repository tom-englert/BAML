namespace Test
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Resources;

    using Baml;

    using Xunit;

    public class UnitTest
    {
        [Fact]
        public void Test1()
        {
            var assembly = GetType().Assembly;
            var assembyResources = assembly.GetManifestResourceNames().FirstOrDefault(res => res.EndsWith("g.resources", StringComparison.Ordinal));
            var resourceStream = assembly.GetManifestResourceStream(assembyResources);

            var referencedAssemblies = new HashSet<string>();
            var usedTypes = new HashSet<string>();

            using (var resourceReader = new ResourceReader(resourceStream))
            {
                foreach (DictionaryEntry entry in resourceReader)
                {
                    if ((entry.Key as string)?.EndsWith(".baml", StringComparison.Ordinal) != true)
                        continue;

                    var bamlStream = (Stream)entry.Value;

                    var records = Baml.ReadDocument(bamlStream);

                    foreach (var referencedAssembly in records.OfType<AssemblyInfoRecord>().Select(ai => new AssemblyName(ai.AssemblyFullName).Name))
                    {
                        referencedAssemblies.Add(referencedAssembly);
                    }

                    foreach (var usedType in records.OfType<TypeInfoRecord>().Select(ti => ti.TypeFullName.Split('.').Last()))
                    {
                        usedTypes.Add(usedType);
                    }
                }
            }

            var expectedAssemblies = new[]
            {
                "mscorlib",
                "PresentationCore",
                "PresentationFramework",
                "System.Windows.Interactivity",
                "System.Xaml",
                "Test",
                "TomsToolbox.Wpf",
                "TomsToolbox.Wpf.Styles",
                "WindowsBase"
            };

            Assert.Equal(expectedAssemblies, referencedAssemblies.OrderBy(name => name));

            var expectedTypes = new[]
            {
                "Interaction",
                "ListBoxSelectAllBehavior",
                "ResourceKeys",
                "UserControl"
            };

            Assert.Equal(expectedTypes, usedTypes.OrderBy(name => name));
        }
    }
}
