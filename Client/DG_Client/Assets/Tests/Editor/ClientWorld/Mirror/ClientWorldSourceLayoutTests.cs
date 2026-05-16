using System.IO;
using NUnit.Framework;

namespace DG.EditorTests
{
    public sealed class ClientWorldSourceLayoutTests
    {
        [Test]
        public void ClientWorld_UsesSemanticRuntimeFolders()
        {
            string root = RepositoryRoot();
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "Mirror")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "Presentation")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "DebugTools")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "EditorTools")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "World")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "View")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "Debug")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Client", "DG_Client", "Assets", "Scripts", "Editor")));
        }

        [Test]
        public void SharedGameCore_UsesSemanticSourceFolders()
        {
            string root = RepositoryRoot();
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "Domain", "Components")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "Configuration", "Generated", "LubanTables")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "ActionRuntime", "Targeting", "Selectors")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "ActionRuntime", "Strategies", "Implementations")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "ActionRuntime", "Blocking", "Outcomes")));
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "ActionRuntime", "Generated")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "Shared", "DG.GameCore", "Rules", "Actions", "ActionPipeline.cs")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "Shared", "DG.GameCore", "Config")));
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "openspec")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "Client")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "Shared")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Repository root not found.");
            return string.Empty;
        }
    }
}
