using System.IO;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class RuntimeDebugLayoutManagerTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Application.temporaryCachePath, "RuntimeDebugLayoutManagerTests", TestContext.CurrentContext.Test.ID, "DebugLayouts");
            Directory.CreateDirectory(testDirectory);
            DebugLayoutPaths.DirectoryOverride = testDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            DebugLayoutPaths.DirectoryOverride = string.Empty;
            string root = Path.GetDirectoryName(testDirectory);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void SaveAAndB_LoadsClickedLayoutImmediately()
        {
            var manager = new DebugRuntimeLayoutManager();
            var selectionA = Selection(Snapshot(10, 1, 1));
            var selectionB = Selection(Snapshot(20, 2, 2), Snapshot(21, 3, 2));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();

            Assert.IsTrue(manager.SaveAs(selectionA, null, "layout_A", provider));
            Assert.AreEqual("layout_A", manager.LoadedStructureBlock.Name);
            Assert.IsTrue(manager.SaveAs(selectionB, null, "layout_B", provider));
            Assert.AreEqual("layout_B", manager.LoadedStructureBlock.Name);

            string pathA = DebugLayoutPaths.NamedFilePath("layout_A");
            string pathB = DebugLayoutPaths.NamedFilePath("layout_B");
            Assert.IsTrue(manager.Load(pathA, provider));
            Assert.AreEqual(pathA, manager.CurrentLayoutPath);
            Assert.AreEqual(1, manager.LoadedStructureBlock.Entries.Count);
            Assert.IsTrue(manager.Load(pathB, provider));
            Assert.AreEqual(pathB, manager.CurrentLayoutPath);
            Assert.AreEqual(2, manager.LoadedStructureBlock.Entries.Count);
        }

        [Test]
        public void RenameCurrent_UpdatesCurrentPathAndFileList()
        {
            var manager = SavedManager("rename_source", Selection(Snapshot(10, 1, 1)));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();

            Assert.IsTrue(manager.RenameCurrent("rename_target", provider), manager.LastLayoutOperationResult);

            Assert.AreEqual(DebugLayoutPaths.NamedFilePath("rename_target"), manager.CurrentLayoutPath);
            Assert.IsTrue(File.Exists(DebugLayoutPaths.NamedFilePath("rename_target")));
            Assert.IsFalse(File.Exists(DebugLayoutPaths.NamedFilePath("rename_source")));
            Assert.IsTrue(ContainsPath(manager, DebugLayoutPaths.NamedFilePath("rename_target")));
        }

        [Test]
        public void DeleteCurrent_ClearsCurrentLayoutAndDoesNotLoadAnother()
        {
            var manager = SavedManager("delete_a", Selection(Snapshot(10, 1, 1)));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();
            Assert.IsTrue(manager.SaveAs(Selection(Snapshot(20, 2, 2)), null, "delete_b", provider));
            Assert.IsTrue(manager.Load(DebugLayoutPaths.NamedFilePath("delete_a"), provider));

            Assert.IsTrue(manager.DeleteCurrent(provider), manager.LastLayoutOperationResult);

            Assert.IsEmpty(manager.CurrentLayoutPath);
            Assert.IsNull(manager.LoadedStructureBlock);
            Assert.IsTrue(File.Exists(DebugLayoutPaths.NamedFilePath("delete_b")));
        }

        [Test]
        public void Refresh_DoesNotPromoteLastSavedToCurrent()
        {
            var manager = new DebugRuntimeLayoutManager();
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();

            Assert.IsTrue(manager.SaveAs(Selection(Snapshot(10, 1, 1)), null, "refresh_a", provider));
            manager.ClearLoadedLayout();
            Assert.IsTrue(manager.SaveAs(Selection(Snapshot(20, 2, 2)), null, "refresh_b", provider));
            manager.ClearLoadedLayout();
            manager.Refresh(provider);

            Assert.IsEmpty(manager.CurrentLayoutPath);
            Assert.IsNull(manager.LoadedStructureBlock);
            Assert.AreEqual(DebugLayoutPaths.NamedFilePath("refresh_b"), manager.LastSavedPath);
        }

        [Test]
        public void InvalidInputs_ReturnReadableReasons()
        {
            var manager = SavedManager("invalid_existing", Selection(Snapshot(10, 1, 1)));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();

            Assert.IsFalse(manager.SaveAs(new DebugLayoutSelectionSet(), null, "empty_selection", provider));
            StringAssert.Contains("selection is empty", manager.LastLayoutOperationResult);
            Assert.IsFalse(manager.SaveAs(Selection(Snapshot(11, 1, 2)), null, "   ", provider));
            StringAssert.Contains("name is empty", manager.LastLayoutOperationResult);
            Assert.IsFalse(manager.SaveAs(Selection(Snapshot(12, 1, 3)), null, "invalid_existing", provider));
            StringAssert.Contains("already exists", manager.LastLayoutOperationResult);
            Assert.IsFalse(manager.RenameCurrent("invalid_existing", provider));
            StringAssert.Contains("already exists", manager.LastLayoutOperationResult);
        }

        [Test]
        public void LoadCorruptFileFailure_KeepsPreviousCurrentLayout()
        {
            var manager = SavedManager("valid", Selection(Snapshot(10, 1, 1)));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();
            string beforePath = manager.CurrentLayoutPath;
            File.WriteAllText(DebugLayoutPaths.NamedFilePath("corrupt"), "{ not-json");

            Assert.IsFalse(manager.Load(DebugLayoutPaths.NamedFilePath("corrupt"), provider));

            Assert.AreEqual(beforePath, manager.CurrentLayoutPath);
            Assert.AreEqual("valid", manager.LoadedStructureBlock.Name);
        }

        [Test]
        public void Refresh_DoesNotReloadExternallyModifiedCurrentLayout()
        {
            var manager = SavedManager("external", Selection(Snapshot(10, 1, 1)));
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();
            DebugStructureBlockStorage.Save(DebugLayoutPaths.NamedFilePath("external"), Document("external", Snapshot(20, 5, 5), Snapshot(21, 6, 5)));

            manager.Refresh(provider);

            Assert.AreEqual(1, manager.LoadedStructureBlock.Entries.Count);
            Assert.AreEqual(DebugLayoutPaths.NamedFilePath("external"), manager.CurrentLayoutPath);
        }

        private static DebugRuntimeLayoutManager SavedManager(string name, DebugLayoutSelectionSet selection)
        {
            var manager = new DebugRuntimeLayoutManager();
            Assert.IsTrue(manager.SaveAs(selection, null, name, ClientGameConfigProviderFactory.Create()), manager.LastLayoutOperationResult);
            return manager;
        }

        private static bool ContainsPath(DebugRuntimeLayoutManager manager, string path)
        {
            for (int i = 0; i < manager.LayoutFiles.Count; i++)
            {
                if (manager.LayoutFiles[i].Path == path)
                {
                    return true;
                }
            }

            return false;
        }

        private static DebugLayoutSelectionSet Selection(params EntitySnapshot[] snapshots)
        {
            var selection = new DebugLayoutSelectionSet();
            for (int i = 0; i < snapshots.Length; i++)
            {
                selection.Add(snapshots[i]);
            }

            return selection;
        }

        private static DebugStructureBlockDocument Document(string name, params EntitySnapshot[] snapshots)
        {
            return DebugStructureBlockStorage.FromSnapshots(name, snapshots);
        }

        private static EntitySnapshot Snapshot(long entityId, int x, int y)
        {
            return new EntitySnapshot(entityId, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, x, y, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1);
        }
    }
}
