using System;
using System.Collections.Generic;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public enum ClientAnimationMotionKind
    {
        Unknown = 0,
        PlayerMove = 1,
        MechanismPush = 2,
        AutoMove = 3,
        DebugDrag = 4,
        Spawn = 5,
        Remove = 6
    }

    public enum ClientAnimationEasing
    {
        Linear = 1,
        EaseOut = 2,
        EaseInOut = 3
    }

    public readonly struct ClientAnimationStyle
    {
        public ClientAnimationStyle(string styleId, ClientAnimationMotionKind motionKind, float durationSeconds, ClientAnimationEasing easing, Color flashColor, float scaleFeedback, bool instant)
        {
            StyleId = string.IsNullOrWhiteSpace(styleId) ? "unknown" : styleId;
            MotionKind = motionKind;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            Easing = easing;
            FlashColor = flashColor;
            ScaleFeedback = Mathf.Max(0f, scaleFeedback);
            Instant = instant;
        }

        public string StyleId { get; }
        public ClientAnimationMotionKind MotionKind { get; }
        public float DurationSeconds { get; }
        public ClientAnimationEasing Easing { get; }
        public Color FlashColor { get; }
        public float ScaleFeedback { get; }
        public bool Instant { get; }

        public static ClientAnimationStyle Unknown => new("unknown", ClientAnimationMotionKind.Unknown, 0.1f, ClientAnimationEasing.Linear, new Color(0.78f, 0.82f, 0.85f, 1f), 1.03f, false);
    }

    public readonly struct ClientAnimationMetadata
    {
        public ClientAnimationMetadata(long entityId, long serverTick, ClientAnimationMotionKind motionKind, string styleKey)
            : this(entityId, serverTick, motionKind, styleKey, Direction.None)
        {
        }

        public ClientAnimationMetadata(long entityId, long serverTick, ClientAnimationMotionKind motionKind, string styleKey, Direction direction)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            MotionKind = motionKind;
            StyleKey = styleKey ?? string.Empty;
            Direction = direction;
        }

        public long EntityId { get; }
        public long ServerTick { get; }
        public ClientAnimationMotionKind MotionKind { get; }
        public string StyleKey { get; }
        public Direction Direction { get; }
    }

    public readonly struct ClientAnimationEvent
    {
        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style)
            : this(entityId, serverTick, fromCoord, toCoord, fromDirection, toDirection, motionKind, styleId, style, Direction.None)
        {
        }

        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style, Direction impulseDirection)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            FromCoord = fromCoord;
            ToCoord = toCoord;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            MotionKind = motionKind;
            StyleId = styleId ?? string.Empty;
            Style = style;
            ImpulseDirection = impulseDirection;
        }

        public long EntityId { get; }
        public long ServerTick { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
        public ClientAnimationMotionKind MotionKind { get; }
        public string StyleId { get; }
        public ClientAnimationStyle Style { get; }
        public Direction ImpulseDirection { get; }
        public bool IsMovement => MotionKind != ClientAnimationMotionKind.Spawn && MotionKind != ClientAnimationMotionKind.Remove && FromCoord != ToCoord;
        public bool IsImpulse => MotionKind == ClientAnimationMotionKind.MechanismPush && FromCoord == ToCoord && ImpulseDirection != Direction.None;
    }

    public sealed class ClientAnimationStyleProvider
    {
        private readonly Dictionary<string, ClientAnimationStyle> styles;

        public ClientAnimationStyleProvider(IReadOnlyDictionary<string, ClientAnimationStyle> styles)
        {
            this.styles = new Dictionary<string, ClientAnimationStyle>(StringComparer.OrdinalIgnoreCase);
            if (styles != null)
            {
                foreach (KeyValuePair<string, ClientAnimationStyle> pair in styles)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                    {
                        this.styles[pair.Key] = pair.Value;
                    }
                }
            }

            if (!this.styles.ContainsKey("unknown"))
            {
                this.styles["unknown"] = ClientAnimationStyle.Unknown;
            }
        }

        public static ClientAnimationStyleProvider FromTables(cfg.Tables tables)
        {
            var mapped = new Dictionary<string, ClientAnimationStyle>(StringComparer.OrdinalIgnoreCase);
            if (tables != null)
            {
                foreach (cfg.gamecore.AnimationStyle row in tables.TbAnimationStyle.DataList)
                {
                    mapped[row.StyleId] = Convert(row);
                }
            }

            return new ClientAnimationStyleProvider(mapped);
        }

        public static ClientAnimationStyleProvider Fallback()
        {
            return new ClientAnimationStyleProvider(new Dictionary<string, ClientAnimationStyle>(StringComparer.OrdinalIgnoreCase)
            {
                ["player_move"] = new("player_move", ClientAnimationMotionKind.PlayerMove, 0.12f, ClientAnimationEasing.EaseOut, new Color(0.1f, 0.8f, 1f, 1f), 1.05f, false),
                ["mechanism_push"] = new("mechanism_push", ClientAnimationMotionKind.MechanismPush, 0.18f, ClientAnimationEasing.EaseInOut, new Color(1f, 0.7f, 0.1f, 1f), 1.18f, false),
                ["auto_move"] = new("auto_move", ClientAnimationMotionKind.AutoMove, 0.16f, ClientAnimationEasing.Linear, new Color(1f, 0.88f, 0.18f, 1f), 1.1f, false),
                ["debug_drag"] = new("debug_drag", ClientAnimationMotionKind.DebugDrag, 0.06f, ClientAnimationEasing.Linear, Color.white, 1f, true),
                ["spawn"] = new("spawn", ClientAnimationMotionKind.Spawn, 0.1f, ClientAnimationEasing.EaseOut, new Color(0.4f, 1f, 0.6f, 1f), 1.25f, true),
                ["remove"] = new("remove", ClientAnimationMotionKind.Remove, 0.08f, ClientAnimationEasing.EaseOut, new Color(1f, 0.33f, 0.33f, 1f), 0.4f, true),
                ["unknown"] = ClientAnimationStyle.Unknown
            });
        }

        public ClientAnimationStyle Resolve(ClientAnimationMotionKind kind, string styleKey, EntitySnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(styleKey) && styles.TryGetValue(styleKey, out ClientAnimationStyle byKey))
            {
                return byKey;
            }

            string motionKey = MotionKindKey(kind);
            if (styles.TryGetValue(motionKey, out ClientAnimationStyle byMotion))
            {
                return byMotion;
            }

            if ((snapshot.AutoMove || snapshot.Bouncable) && styles.TryGetValue("auto_move", out ClientAnimationStyle autoMove))
            {
                return autoMove;
            }

            return styles["unknown"];
        }

        public bool TryGet(string styleId, out ClientAnimationStyle style)
        {
            return styles.TryGetValue(styleId, out style);
        }

        private static ClientAnimationStyle Convert(cfg.gamecore.AnimationStyle row)
        {
            return new ClientAnimationStyle(
                row.StyleId,
                ParseMotionKind(row.MotionKind),
                row.DurationSeconds,
                (ClientAnimationEasing)(int)row.Easing,
                ParseColor(row.FlashColor),
                row.ScaleFeedback,
                row.Instant);
        }

        public static ClientAnimationMotionKind ParseMotionKind(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "player_move" => ClientAnimationMotionKind.PlayerMove,
                "mechanism_push" => ClientAnimationMotionKind.MechanismPush,
                "auto_move" => ClientAnimationMotionKind.AutoMove,
                "debug_drag" => ClientAnimationMotionKind.DebugDrag,
                "spawn" => ClientAnimationMotionKind.Spawn,
                "remove" => ClientAnimationMotionKind.Remove,
                _ => ClientAnimationMotionKind.Unknown
            };
        }

        public static string MotionKindKey(ClientAnimationMotionKind kind)
        {
            return kind switch
            {
                ClientAnimationMotionKind.PlayerMove => "player_move",
                ClientAnimationMotionKind.MechanismPush => "mechanism_push",
                ClientAnimationMotionKind.AutoMove => "auto_move",
                ClientAnimationMotionKind.DebugDrag => "debug_drag",
                ClientAnimationMotionKind.Spawn => "spawn",
                ClientAnimationMotionKind.Remove => "remove",
                _ => "unknown"
            };
        }

        private static Color ParseColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : ClientAnimationStyle.Unknown.FlashColor;
        }
    }

    public sealed class ClientAnimationLayer
    {
        private readonly Queue<ClientAnimationEvent> pendingEvents = new();
        private readonly List<ClientAnimationEvent> recentEvents = new();
        private readonly ClientAnimationStyleProvider styleProvider;
        private readonly int recentLimit;

        public ClientAnimationLayer(ClientAnimationStyleProvider styleProvider, int recentLimit = 16)
        {
            this.styleProvider = styleProvider ?? ClientAnimationStyleProvider.Fallback();
            this.recentLimit = Mathf.Max(1, recentLimit);
            Enabled = true;
        }

        public bool Enabled { get; set; }
        public int PendingCount => pendingEvents.Count;
        public IReadOnlyList<ClientAnimationEvent> RecentEvents => recentEvents;
        public string RecentSummary { get; private set; } = string.Empty;

        public void Clear()
        {
            pendingEvents.Clear();
            recentEvents.Clear();
            RecentSummary = string.Empty;
        }

        public bool TryDequeue(out ClientAnimationEvent animationEvent)
        {
            if (!Enabled || pendingEvents.Count == 0)
            {
                animationEvent = default;
                return false;
            }

            animationEvent = pendingEvents.Dequeue();
            return true;
        }

        public void CaptureSnapshotApply(long serverTick, IReadOnlyList<EntitySnapshot> before, IReadOnlyList<EntitySnapshot> after, IReadOnlyList<ClientAnimationMetadata> metadata)
        {
            if (!Enabled)
            {
                return;
            }

            var metadataByEntity = MapMetadata(metadata);
            var beforeByEntity = MapSnapshots(before);
            var afterByEntity = MapSnapshots(after);

            foreach (KeyValuePair<long, EntitySnapshot> pair in afterByEntity)
            {
                if (!beforeByEntity.TryGetValue(pair.Key, out EntitySnapshot oldSnapshot))
                {
                    Enqueue(CreateEvent(pair.Key, serverTick, pair.Value, pair.Value, ResolveKind(pair.Key, ClientAnimationMotionKind.Spawn, metadataByEntity), metadataByEntity));
                    continue;
                }

                EntitySnapshot newSnapshot = pair.Value;
                ClientAnimationMotionKind metadataKind = ResolveKind(pair.Key, ClientAnimationMotionKind.Unknown, metadataByEntity);
                if (oldSnapshot.X == newSnapshot.X && oldSnapshot.Y == newSnapshot.Y && oldSnapshot.Direction == newSnapshot.Direction)
                {
                    if (ShouldCreateSameStateFeedback(pair.Key, metadataKind, newSnapshot, metadataByEntity))
                    {
                        Enqueue(CreateEvent(pair.Key, serverTick, oldSnapshot, newSnapshot, metadataKind, metadataByEntity));
                    }

                    continue;
                }

                Enqueue(CreateEvent(pair.Key, serverTick, oldSnapshot, newSnapshot, metadataKind, metadataByEntity));
            }

            foreach (KeyValuePair<long, EntitySnapshot> pair in beforeByEntity)
            {
                if (afterByEntity.ContainsKey(pair.Key))
                {
                    continue;
                }

                Enqueue(CreateEvent(pair.Key, serverTick, pair.Value, pair.Value, ResolveKind(pair.Key, ClientAnimationMotionKind.Remove, metadataByEntity), metadataByEntity));
            }
        }

        private ClientAnimationEvent CreateEvent(long entityId, long serverTick, EntitySnapshot from, EntitySnapshot to, ClientAnimationMotionKind fallbackKind, IReadOnlyDictionary<long, ClientAnimationMetadata> metadataByEntity)
        {
            metadataByEntity.TryGetValue(entityId, out ClientAnimationMetadata metadata);
            ClientAnimationMotionKind kind = metadata.MotionKind == ClientAnimationMotionKind.Unknown ? fallbackKind : metadata.MotionKind;
            ClientAnimationStyle style = styleProvider.Resolve(kind, metadata.StyleKey, to);
            return new ClientAnimationEvent(
                entityId,
                serverTick,
                new Vector2Int(from.X, from.Y),
                new Vector2Int(to.X, to.Y),
                from.Direction,
                to.Direction,
                kind,
                style.StyleId,
                style,
                metadata.Direction);
        }

        private ClientAnimationMotionKind ResolveKind(long entityId, ClientAnimationMotionKind fallbackKind, IReadOnlyDictionary<long, ClientAnimationMetadata> metadataByEntity)
        {
            if (metadataByEntity.TryGetValue(entityId, out ClientAnimationMetadata metadata) && metadata.MotionKind != ClientAnimationMotionKind.Unknown)
            {
                return metadata.MotionKind;
            }

            return fallbackKind;
        }

        private static bool ShouldCreateSameStateFeedback(long entityId, ClientAnimationMotionKind kind, EntitySnapshot snapshot, IReadOnlyDictionary<long, ClientAnimationMetadata> metadataByEntity)
        {
            return kind == ClientAnimationMotionKind.MechanismPush &&
                   metadataByEntity.TryGetValue(entityId, out ClientAnimationMetadata metadata) &&
                   metadata.ServerTick >= snapshot.ServerTick &&
                   metadata.Direction != Direction.None;
        }

        private void Enqueue(ClientAnimationEvent animationEvent)
        {
            pendingEvents.Enqueue(animationEvent);
            recentEvents.Add(animationEvent);
            while (recentEvents.Count > recentLimit)
            {
                recentEvents.RemoveAt(0);
            }

            RecentSummary = $"{animationEvent.ServerTick}:{animationEvent.EntityId}:{ClientAnimationStyleProvider.MotionKindKey(animationEvent.MotionKind)}:{animationEvent.StyleId}";
        }

        private static Dictionary<long, EntitySnapshot> MapSnapshots(IReadOnlyList<EntitySnapshot> snapshots)
        {
            var result = new Dictionary<long, EntitySnapshot>();
            if (snapshots == null)
            {
                return result;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                result[snapshots[i].EntityId] = snapshots[i];
            }

            return result;
        }

        private static Dictionary<long, ClientAnimationMetadata> MapMetadata(IReadOnlyList<ClientAnimationMetadata> metadata)
        {
            var result = new Dictionary<long, ClientAnimationMetadata>();
            if (metadata == null)
            {
                return result;
            }

            for (int i = 0; i < metadata.Count; i++)
            {
                result[metadata[i].EntityId] = metadata[i];
            }

            return result;
        }
    }
}
