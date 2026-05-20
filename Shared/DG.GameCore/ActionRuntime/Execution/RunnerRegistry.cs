using System;
using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class RunnerRegistry
    {
        private static readonly RunnerRegistry empty = new();
        private readonly Dictionary<string, BehaviorRunner> runners = new();

        public static RunnerRegistry Default => empty;

        public RunnerRegistry()
        {
        }

        public RunnerRegistry(IEnumerable<KeyValuePair<string, BehaviorRunner>> initial)
        {
            if (initial == null)
            {
                return;
            }

            foreach (KeyValuePair<string, BehaviorRunner> pair in initial)
            {
                Register(pair.Key, pair.Value);
            }
        }

        public int Count => runners.Count;

        public void Register(string runnerId, BehaviorRunner runner)
        {
            if (string.IsNullOrEmpty(runnerId))
            {
                throw new ArgumentException("Runner id must be non-empty.", nameof(runnerId));
            }

            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            runners[runnerId] = runner;
        }

        public bool TryGet(string runnerId, out BehaviorRunner runner)
        {
            if (!string.IsNullOrEmpty(runnerId) && runners.TryGetValue(runnerId, out BehaviorRunner found))
            {
                runner = found;
                return true;
            }

            runner = null!;
            return false;
        }

        public BehaviorRunner Get(string runnerId)
        {
            if (!TryGet(runnerId, out BehaviorRunner runner))
            {
                throw new KeyNotFoundException($"No BehaviorRunner registered for runner id '{runnerId}'.");
            }

            return runner;
        }

        public bool Contains(string runnerId)
        {
            return !string.IsNullOrEmpty(runnerId) && runners.ContainsKey(runnerId);
        }
    }
}
