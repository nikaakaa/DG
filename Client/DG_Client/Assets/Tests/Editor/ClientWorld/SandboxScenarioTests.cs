using System.Linq;
using System.IO;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class SandboxScenarioTests
    {
        [Test]
        public void Parser_AcceptsValidScenario()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""name"": ""spawn tag expect"",
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""box"", ""configId"": 1003, ""x"": 0, ""y"": 0, ""direction"": ""None"" },
    { ""kind"": ""setTag"", ""alias"": ""box"", ""tag"": ""ImmuneMechanismPush"", ""enabled"": true },
    { ""kind"": ""expectTag"", ""alias"": ""box"", ""tag"": ""ImmuneMechanismPush"" },
    { ""kind"": ""expectPosition"", ""alias"": ""box"", ""x"": 0, ""y"": 0 }
  ]
}";

            bool parsed = SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out SandboxScenarioDocument document, out var errors);

            Assert.IsTrue(parsed, string.Join("|", errors));
            Assert.AreEqual("spawn tag expect", document.Name);
            Assert.AreEqual(4, document.Steps.Count);
        }

        [Test]
        public void Parser_RejectsUnknownConfigId()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""ghost"", ""configId"": 999999, ""x"": 0, ""y"": 0 }
  ]
}";

            bool parsed = SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out _, out var errors);

            Assert.IsFalse(parsed);
            Assert.IsTrue(errors.Any(error => error.Contains("unknown configId")));
        }

        [Test]
        public void Parser_RejectsUnknownTag()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""box"", ""configId"": 1003, ""x"": 0, ""y"": 0 },
    { ""kind"": ""setTag"", ""alias"": ""box"", ""tag"": ""NotARealTag"", ""enabled"": true }
  ]
}";

            bool parsed = SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out _, out var errors);

            Assert.IsFalse(parsed);
            Assert.IsTrue(errors.Any(error => error.Contains("unknown tag")));
        }

        [Test]
        public void Parser_RejectsArchetypeData()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""box"", ""configId"": 1003, ""x"": 0, ""y"": 0, ""components"": [""Position""] }
  ]
}";

            bool parsed = SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out _, out var errors);

            Assert.IsFalse(parsed);
            Assert.IsTrue(errors.Any(error => error.Contains("must not define entity archetype data")));
        }

        [Test]
        public void Palette_ListsLubanArchetypes()
        {
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();
            SandboxEntityPalette palette = SandboxEntityPalette.FromProvider(provider);

            Assert.IsTrue(palette.Entries.Count >= 6);
            SandboxEntityPaletteEntry conveyor = palette.Entries.Single(entry => entry.ConfigId == DefaultWorldConfig.ConveyorConfigId);
            Assert.AreEqual(DefaultWorldConfig.ConveyorArchetypeId, conveyor.ArchetypeId);
            Assert.IsTrue(conveyor.Components.Contains(ComponentKind.PushOnEnter));
            Assert.IsTrue(conveyor.Tags.Contains("Tile.Conveyor"));
            Assert.IsTrue(palette.Search("Conveyor").Any(entry => entry.ConfigId == DefaultWorldConfig.ConveyorConfigId));
            Assert.IsTrue(palette.Search(string.Empty, ComponentKind.PortConnector).Any(entry => entry.ConfigId == DefaultWorldConfig.PortConnectorBlockerConfigId));
        }

        [Test]
        public void LocalRunner_ExecutesSpawnSetTagAndExpect()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""box"", ""configId"": 1003, ""x"": 0, ""y"": 0 },
    { ""kind"": ""setTag"", ""alias"": ""box"", ""tag"": ""ImmuneMechanismPush"", ""enabled"": true },
    { ""kind"": ""expectTag"", ""alias"": ""box"", ""tag"": ""ImmuneMechanismPush"" },
    { ""kind"": ""expectPosition"", ""alias"": ""box"", ""x"": 0, ""y"": 0 }
  ]
}";

            Assert.IsTrue(SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out SandboxScenarioDocument document, out var errors), string.Join("|", errors));
            var runner = new LocalSandboxScenarioRunner(ClientGameConfigProviderFactory.Create());
            SandboxScenarioRunResult result = runner.Run(document);

            Assert.IsTrue(result.Success, result.Reason);
        }

        [Test]
        public void LocalRunner_ImmuneMechanismPushBlocksConveyorPush()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""belt"", ""configId"": 2001, ""x"": 0, ""y"": 0, ""direction"": ""Right"" },
    { ""kind"": ""spawn"", ""alias"": ""box"", ""configId"": 1003, ""x"": 0, ""y"": 0 },
    { ""kind"": ""setTag"", ""alias"": ""box"", ""tag"": ""ImmuneMechanismPush"", ""enabled"": true },
    { ""kind"": ""tick"", ""ticks"": 1 },
    { ""kind"": ""expectLastResult"", ""success"": false, ""reason"": ""blocked by tag"" },
    { ""kind"": ""expectPosition"", ""alias"": ""box"", ""x"": 0, ""y"": 0 }
  ]
}";

            Assert.IsTrue(SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out SandboxScenarioDocument document, out var errors), string.Join("|", errors));
            var runner = new LocalSandboxScenarioRunner(ClientGameConfigProviderFactory.Create());
            SandboxScenarioRunResult result = runner.Run(document);

            Assert.IsTrue(result.Success, result.Reason);
        }

        [Test]
        public void LocalRunner_BlockPlayerMoveBlocksPlayerMove()
        {
            string json = @"{
  ""schemaVersion"": 1,
  ""steps"": [
    { ""kind"": ""spawn"", ""alias"": ""player"", ""configId"": 1, ""x"": 0, ""y"": 0 },
    { ""kind"": ""setTag"", ""alias"": ""player"", ""tag"": ""BlockPlayerMove"", ""enabled"": true },
    { ""kind"": ""playerMove"", ""alias"": ""player"", ""x"": 1, ""y"": 0 },
    { ""kind"": ""expectPosition"", ""alias"": ""player"", ""x"": 0, ""y"": 0 }
  ],
  ""expectations"": [
    { ""kind"": ""expectLastResult"", ""success"": false, ""reason"": ""blocked by tag"" }
  ]
}";

            Assert.IsTrue(SandboxScenarioParser.TryParse(json, ClientGameConfigProviderFactory.Create(), out SandboxScenarioDocument document, out var errors), string.Join("|", errors));
            var runner = new LocalSandboxScenarioRunner(ClientGameConfigProviderFactory.Create());
            SandboxScenarioRunResult result = runner.Run(document);

            Assert.IsTrue(result.Success, result.Reason);
        }

        [Test]
        public void Storage_SaveThenLoadKeepsScenarioEquivalent()
        {
            var document = new SandboxScenarioDocument
            {
                SchemaVersion = 1,
                Name = "save-load",
                Steps =
                {
                    new SandboxScenarioStep { Kind = "spawn", Alias = "box", ConfigId = DefaultWorldConfig.PushableBlockerConfigId, X = 1, Y = 2 },
                    new SandboxScenarioStep { Kind = "expectPosition", Alias = "box", X = 1, Y = 2 }
                }
            };
            string path = Path.Combine(Application.temporaryCachePath, "DG_SandboxScenarioTests.json");

            SandboxScenarioStorage.Save(path, document);
            bool loaded = SandboxScenarioStorage.TryLoad(path, ClientGameConfigProviderFactory.Create(), out SandboxScenarioDocument loadedDocument, out var errors);

            Assert.IsTrue(loaded, string.Join("|", errors));
            Assert.AreEqual(document.Name, loadedDocument.Name);
            Assert.AreEqual(document.Steps.Count, loadedDocument.Steps.Count);
            Assert.AreEqual(document.Steps[0].ConfigId, loadedDocument.Steps[0].ConfigId);
            File.Delete(path);
        }

        [Test]
        public void Storage_LoadRejectsMissingLubanConfig()
        {
            var document = new SandboxScenarioDocument
            {
                SchemaVersion = 1,
                Steps =
                {
                    new SandboxScenarioStep { Kind = "spawn", Alias = "missing", ConfigId = 999999, X = 0, Y = 0 }
                }
            };
            string path = Path.Combine(Application.temporaryCachePath, "DG_SandboxScenarioMissingConfig.json");

            SandboxScenarioStorage.Save(path, document);
            bool loaded = SandboxScenarioStorage.TryLoad(path, ClientGameConfigProviderFactory.Create(), out _, out var errors);

            Assert.IsFalse(loaded);
            Assert.IsTrue(errors.Any(error => error.Contains("unknown configId")));
            File.Delete(path);
        }

        [Test]
        public void LocalRunner_InvalidScenarioDoesNotModifyWorld()
        {
            var runner = new LocalSandboxScenarioRunner(ClientGameConfigProviderFactory.Create());
            var document = new SandboxScenarioDocument
            {
                SchemaVersion = 1,
                Steps =
                {
                    new SandboxScenarioStep { Kind = "spawn", Alias = "ghost", ConfigId = 999999, X = 0, Y = 0 }
                }
            };

            SandboxScenarioRunResult result = runner.Run(document);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, runner.World.EntityCount);
        }
    }
}
