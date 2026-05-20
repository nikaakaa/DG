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
        Remove = 6,
        RotatePivot = 7,
        RotatePivotBounce = 8
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

        public ClientAnimationStyle WithDuration(float durationSeconds)
        {
            return new ClientAnimationStyle(StyleId, MotionKind, durationSeconds, Easing, FlashColor, ScaleFeedback, Instant);
        }
    }

    public readonly struct ClientAnimationEvent
    {
        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style)
            : this(entityId, serverTick, fromCoord, toCoord, fromDirection, toDirection, motionKind, styleId, style, Direction.None)
        {
        }

        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style, Direction impulseDirection)
            : this(entityId, serverTick, fromCoord, toCoord, fromDirection, toDirection, DirectionMask.None, motionKind, styleId, style, impulseDirection, 0, default, RotatePivotDirection.None, false, default)
        {
        }

        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style, Direction impulseDirection, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, bool bounce, Vector2Int impactCoord)
            : this(entityId, serverTick, fromCoord, toCoord, fromDirection, toDirection, DirectionMask.None, motionKind, styleId, style, impulseDirection, pivotEntityId, pivotCoord, rotateDirection, bounce, impactCoord)
        {
        }

        public ClientAnimationEvent(long entityId, long serverTick, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, DirectionMask fromPortLocalPorts, ClientAnimationMotionKind motionKind, string styleId, ClientAnimationStyle style, Direction impulseDirection, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, bool bounce, Vector2Int impactCoord)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            FromCoord = fromCoord;
            ToCoord = toCoord;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            FromPortLocalPorts = fromPortLocalPorts;
            MotionKind = motionKind;
            StyleId = styleId ?? string.Empty;
            Style = style;
            ImpulseDirection = impulseDirection;
            PivotEntityId = pivotEntityId;
            PivotCoord = pivotCoord;
            RotateDirection = rotateDirection;
            Bounce = bounce;
            ImpactCoord = impactCoord;
        }

        public long EntityId { get; }
        public long ServerTick { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
        public DirectionMask FromPortLocalPorts { get; }
        public ClientAnimationMotionKind MotionKind { get; }
        public string StyleId { get; }
        public ClientAnimationStyle Style { get; }
        public Direction ImpulseDirection { get; }
        public long PivotEntityId { get; }
        public Vector2Int PivotCoord { get; }
        public RotatePivotDirection RotateDirection { get; }
        public bool Bounce { get; }
        public Vector2Int ImpactCoord { get; }
        public bool IsRotatePivot => MotionKind == ClientAnimationMotionKind.RotatePivot || MotionKind == ClientAnimationMotionKind.RotatePivotBounce;
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
                ["rotate_pivot"] = new("rotate_pivot", ClientAnimationMotionKind.RotatePivot, 0.54f, ClientAnimationEasing.EaseInOut, new Color(0.65f, 0.95f, 1f, 1f), 1.08f, false),
                ["rotate_pivot_bounce"] = new("rotate_pivot_bounce", ClientAnimationMotionKind.RotatePivotBounce, 0.54f, ClientAnimationEasing.EaseInOut, new Color(1f, 0.82f, 0.24f, 1f), 1.12f, false),
                ["pivot.impact"] = new("pivot.impact", ClientAnimationMotionKind.MechanismPush, 0.54f, ClientAnimationEasing.EaseOut, new Color(1f, 0.44f, 0.24f, 1f), 1.2f, false),
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
                "rotate_pivot" => ClientAnimationMotionKind.RotatePivot,
                "rotate_pivot_bounce" => ClientAnimationMotionKind.RotatePivotBounce,
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
                ClientAnimationMotionKind.RotatePivot => "rotate_pivot",
                ClientAnimationMotionKind.RotatePivotBounce => "rotate_pivot_bounce",
                _ => "unknown"
            };
        }

        private static Color ParseColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : ClientAnimationStyle.Unknown.FlashColor;
        }

    }

    public readonly struct PresentationFactKey : IEquatable<PresentationFactKey>
    {
        public PresentationFactKey(long serverTick, long factId)
        {
            ServerTick = serverTick;
            FactId = factId;
        }

        public long ServerTick { get; }
        public long FactId { get; }
        public bool Equals(PresentationFactKey other) => ServerTick == other.ServerTick && FactId == other.FactId;
        public override bool Equals(object obj) => obj is PresentationFactKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ServerTick, FactId);
    }

    public readonly struct RotatePivotGroupMemberPlaybackPlan
    {
        public RotatePivotGroupMemberPlaybackPlan(long entityId, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, DirectionMask fromPortLocalPorts, DirectionMask toPortLocalPorts)
        {
            EntityId = entityId;
            FromCoord = fromCoord;
            ToCoord = toCoord;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            FromPortLocalPorts = fromPortLocalPorts;
            ToPortLocalPorts = toPortLocalPorts;
        }

        public long EntityId { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
        public DirectionMask FromPortLocalPorts { get; }
        public DirectionMask ToPortLocalPorts { get; }
    }

    public readonly struct RotatePivotGroupPlaybackPlan
    {
        public RotatePivotGroupPlaybackPlan(string groupId, long serverTick, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, PresentationFactResultKind resultKind, ClientAnimationStyle style, IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> members)
            : this(groupId, serverTick, 0, 0, 0, 0, pivotEntityId, pivotCoord, rotateDirection, resultKind, style, members)
        {
        }

        public RotatePivotGroupPlaybackPlan(string groupId, long serverTick, long startTick, long contactTick, long endTick, double contactProgress, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, PresentationFactResultKind resultKind, ClientAnimationStyle style, IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> members)
        {
            GroupId = string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId;
            ServerTick = serverTick;
            StartTick = startTick;
            ContactTick = contactTick;
            EndTick = endTick;
            ContactProgress = contactProgress;
            PivotEntityId = pivotEntityId;
            PivotCoord = pivotCoord;
            RotateDirection = rotateDirection;
            ResultKind = resultKind;
            Style = style;
            Members = members == null || members.Count == 0 ? Array.Empty<RotatePivotGroupMemberPlaybackPlan>() : new List<RotatePivotGroupMemberPlaybackPlan>(members).ToArray();
        }

        public string GroupId { get; }
        public long ServerTick { get; }
        public long StartTick { get; }
        public long ContactTick { get; }
        public long EndTick { get; }
        public double ContactProgress { get; }
        public long PivotEntityId { get; }
        public Vector2Int PivotCoord { get; }
        public RotatePivotDirection RotateDirection { get; }
        public PresentationFactResultKind ResultKind { get; }
        public ClientAnimationStyle Style { get; }
        public IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> Members { get; }
        public bool Bounce => ResultKind == PresentationFactResultKind.Bounce;
    }

    public enum RigidBodyGroupMotionKind
    {
        RotateAroundPivot = 1,
        Translate = 2
    }

    public readonly struct RigidBodyGroupTrackPlaybackPlan
    {
        public RigidBodyGroupTrackPlaybackPlan(string trackId, RigidBodyGroupMotionKind motionKind, RotatePivotGroupPlaybackPlan rotateAroundPivot)
            : this(trackId, motionKind, rotateAroundPivot, default)
        {
        }

        public RigidBodyGroupTrackPlaybackPlan(string trackId, RigidBodyGroupMotionKind motionKind, TranslateGroupPlaybackPlan translate)
            : this(trackId, motionKind, default, translate)
        {
        }

        private RigidBodyGroupTrackPlaybackPlan(string trackId, RigidBodyGroupMotionKind motionKind, RotatePivotGroupPlaybackPlan rotateAroundPivot, TranslateGroupPlaybackPlan translate)
        {
            TrackId = string.IsNullOrWhiteSpace(trackId)
                ? motionKind == RigidBodyGroupMotionKind.Translate ? translate.GroupId : rotateAroundPivot.GroupId
                : trackId;
            MotionKind = motionKind;
            RotateAroundPivot = rotateAroundPivot;
            Translate = translate;
        }

        public string TrackId { get; }
        public RigidBodyGroupMotionKind MotionKind { get; }
        public RotatePivotGroupPlaybackPlan RotateAroundPivot { get; }
        public TranslateGroupPlaybackPlan Translate { get; }
        public IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> Members => MotionKind == RigidBodyGroupMotionKind.Translate ? Translate.Members : RotateAroundPivot.Members;
    }

    public readonly struct TranslateGroupPlaybackPlan
    {
        public TranslateGroupPlaybackPlan(string groupId, long serverTick, long startTick, long endTick, Direction direction, ClientAnimationStyle style, IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> members)
        {
            GroupId = string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId;
            ServerTick = serverTick;
            StartTick = startTick;
            EndTick = endTick;
            Direction = direction;
            Style = style;
            Members = members == null || members.Count == 0 ? Array.Empty<RotatePivotGroupMemberPlaybackPlan>() : new List<RotatePivotGroupMemberPlaybackPlan>(members).ToArray();
        }

        public string GroupId { get; }
        public long ServerTick { get; }
        public long StartTick { get; }
        public long EndTick { get; }
        public Direction Direction { get; }
        public ClientAnimationStyle Style { get; }
        public IReadOnlyList<RotatePivotGroupMemberPlaybackPlan> Members { get; }
    }

    public readonly struct EntityTrackPlaybackPlan
    {
        public EntityTrackPlaybackPlan(ClientAnimationEvent animationEvent)
        {
            AnimationEvent = animationEvent;
        }

        public ClientAnimationEvent AnimationEvent { get; }
        public long EntityId => AnimationEvent.EntityId;
    }

    public readonly struct PortConnectionTrackPlaybackPlan
    {
        public PortConnectionTrackPlaybackPlan(string connectionKey, long fromEntityId, long toEntityId)
        {
            ConnectionKey = connectionKey ?? string.Empty;
            FromEntityId = fromEntityId;
            ToEntityId = toEntityId;
        }

        public string ConnectionKey { get; }
        public long FromEntityId { get; }
        public long ToEntityId { get; }
    }

    public readonly struct FeedbackTrackPlaybackPlan
    {
        public FeedbackTrackPlaybackPlan(ClientAnimationEvent animationEvent)
        {
            AnimationEvent = animationEvent;
        }

        public ClientAnimationEvent AnimationEvent { get; }
    }

    public readonly struct BehaviorPlaybackPlan
    {
        public BehaviorPlaybackPlan(string planId, long sourceActionId, long serverTick, long startTick, long contactTick, long endTick, double contactProgress, IReadOnlyList<EntityTrackPlaybackPlan> entityTracks, IReadOnlyList<RigidBodyGroupTrackPlaybackPlan> groupTracks, IReadOnlyList<PortConnectionTrackPlaybackPlan> connectionTracks, IReadOnlyList<FeedbackTrackPlaybackPlan> feedbackTracks)
        {
            PlanId = string.IsNullOrWhiteSpace(planId) ? serverTick.ToString() : planId;
            SourceActionId = sourceActionId;
            ServerTick = serverTick;
            StartTick = startTick;
            ContactTick = contactTick;
            EndTick = endTick;
            ContactProgress = contactProgress;
            EntityTracks = entityTracks == null || entityTracks.Count == 0 ? Array.Empty<EntityTrackPlaybackPlan>() : new List<EntityTrackPlaybackPlan>(entityTracks).ToArray();
            GroupTracks = groupTracks == null || groupTracks.Count == 0 ? Array.Empty<RigidBodyGroupTrackPlaybackPlan>() : new List<RigidBodyGroupTrackPlaybackPlan>(groupTracks).ToArray();
            ConnectionTracks = connectionTracks == null || connectionTracks.Count == 0 ? Array.Empty<PortConnectionTrackPlaybackPlan>() : new List<PortConnectionTrackPlaybackPlan>(connectionTracks).ToArray();
            FeedbackTracks = feedbackTracks == null || feedbackTracks.Count == 0 ? Array.Empty<FeedbackTrackPlaybackPlan>() : new List<FeedbackTrackPlaybackPlan>(feedbackTracks).ToArray();
        }

        public string PlanId { get; }
        public long SourceActionId { get; }
        public long ServerTick { get; }
        public long StartTick { get; }
        public long ContactTick { get; }
        public long EndTick { get; }
        public double ContactProgress { get; }
        public IReadOnlyList<EntityTrackPlaybackPlan> EntityTracks { get; }
        public IReadOnlyList<RigidBodyGroupTrackPlaybackPlan> GroupTracks { get; }
        public IReadOnlyList<PortConnectionTrackPlaybackPlan> ConnectionTracks { get; }
        public IReadOnlyList<FeedbackTrackPlaybackPlan> FeedbackTracks { get; }
    }

    public readonly struct ClientPresentationPlaybackPlan
    {
        public ClientPresentationPlaybackPlan(IReadOnlyList<ClientAnimationEvent> entityEvents, IReadOnlyList<RotatePivotGroupPlaybackPlan> rotateGroups)
            : this(entityEvents, rotateGroups, Array.Empty<BehaviorPlaybackPlan>())
        {
        }

        public ClientPresentationPlaybackPlan(IReadOnlyList<ClientAnimationEvent> entityEvents, IReadOnlyList<RotatePivotGroupPlaybackPlan> rotateGroups, IReadOnlyList<BehaviorPlaybackPlan> behaviorPlans)
        {
            EntityEvents = entityEvents == null || entityEvents.Count == 0 ? Array.Empty<ClientAnimationEvent>() : new List<ClientAnimationEvent>(entityEvents).ToArray();
            RotateGroups = rotateGroups == null || rotateGroups.Count == 0 ? Array.Empty<RotatePivotGroupPlaybackPlan>() : new List<RotatePivotGroupPlaybackPlan>(rotateGroups).ToArray();
            BehaviorPlans = behaviorPlans == null || behaviorPlans.Count == 0 ? Array.Empty<BehaviorPlaybackPlan>() : new List<BehaviorPlaybackPlan>(behaviorPlans).ToArray();
        }

        public IReadOnlyList<ClientAnimationEvent> EntityEvents { get; }
        public IReadOnlyList<RotatePivotGroupPlaybackPlan> RotateGroups { get; }
        public IReadOnlyList<BehaviorPlaybackPlan> BehaviorPlans { get; }
    }

    public sealed class ClientPresentationPlaybackPlanner
    {
        private readonly ClientAnimationStyleProvider styleProvider;
        private readonly HashSet<PresentationFactKey> playedFacts = new();

        public ClientPresentationPlaybackPlanner(ClientAnimationStyleProvider styleProvider)
        {
            this.styleProvider = styleProvider ?? ClientAnimationStyleProvider.Fallback();
        }

        public void Clear()
        {
            playedFacts.Clear();
        }

        public ClientPresentationPlaybackPlan Plan(long serverTick, IReadOnlyDictionary<long, EntitySnapshot> before, IReadOnlyDictionary<long, EntitySnapshot> after, IReadOnlyList<ClientPresentationFact> facts)
        {
            var entityEvents = new List<ClientAnimationEvent>();
            var rotateGroups = new List<RotatePivotGroupPlaybackPlan>();
            var translateGroups = new List<TranslateGroupPlaybackPlan>();
            var behaviors = new List<BehaviorPlaybackPlan>();
            if (facts == null || facts.Count == 0)
            {
                return new ClientPresentationPlaybackPlan(entityEvents, rotateGroups, behaviors);
            }

            HashSet<long> groupMembers = CollectGroupMembers(facts);
            for (int i = 0; i < facts.Count; i++)
            {
                ClientPresentationFact fact = facts[i];
                if (fact.FactType == PresentationFactType.Unknown)
                {
                    continue;
                }

                long subjectEntityId = fact.PrimarySubjectEntityId;
                if (groupMembers.Contains(subjectEntityId) && fact.FactType != PresentationFactType.RotatePivotGroup && fact.FactType != PresentationFactType.BodyMoved)
                {
                    continue;
                }

                PresentationFactKey key = fact.FactId == 0
                    ? new PresentationFactKey(fact.ServerTick == 0 ? serverTick : fact.ServerTick, subjectEntityId)
                    : new PresentationFactKey(fact.ServerTick == 0 ? serverTick : fact.ServerTick, fact.FactId);
                if (!playedFacts.Add(key))
                {
                    continue;
                }

                int entityStart = entityEvents.Count;
                int rotateStart = rotateGroups.Count;
                int translateStart = translateGroups.Count;
                PlanFact(serverTick, before, after, fact, entityEvents, rotateGroups, translateGroups);
                AddBehaviorPlan(serverTick, fact, entityEvents, rotateGroups, translateGroups, entityStart, rotateStart, translateStart, behaviors);
            }

            return new ClientPresentationPlaybackPlan(entityEvents, rotateGroups, behaviors);
        }

        private void PlanFact(long serverTick, IReadOnlyDictionary<long, EntitySnapshot> before, IReadOnlyDictionary<long, EntitySnapshot> after, ClientPresentationFact fact, List<ClientAnimationEvent> entityEvents, List<RotatePivotGroupPlaybackPlan> rotateGroups, List<TranslateGroupPlaybackPlan> translateGroups)
        {
            if (fact.FactType == PresentationFactType.RotatePivotGroup)
            {
                PlanRotateGroup(serverTick, before, after, fact, rotateGroups);
                return;
            }

            if (fact.FactType == PresentationFactType.BodyMoved)
            {
                PlanTranslateGroup(serverTick, before, after, fact, translateGroups);
                return;
            }

            if (fact.FactType == PresentationFactType.RotatePivotImpact)
            {
                PlanImpact(serverTick, before, after, fact, entityEvents);
                return;
            }

            ClientAnimationMotionKind motionKind = ResolveMotionKind(fact, after, before);
            if (motionKind == ClientAnimationMotionKind.Unknown)
            {
                return;
            }

            long entityId = fact.PrimarySubjectEntityId;
            if (entityId == 0)
            {
                return;
            }

            EntitySnapshot snapshot = TryGetSnapshot(after, before, entityId, out EntitySnapshot found)
                ? found
                : ClientPresentationHelpers.EmptySnapshot(entityId, fact.ToCoord, serverTick);
            EntitySnapshot from = before.TryGetValue(entityId, out EntitySnapshot beforeSnapshot) ? beforeSnapshot : snapshot;
            EntitySnapshot to = after.TryGetValue(entityId, out EntitySnapshot afterSnapshot) ? afterSnapshot : snapshot;
            Vector2Int fromCoord = fact.FromCoord == default ? new Vector2Int(from.X, from.Y) : fact.FromCoord;
            Vector2Int toCoord = fact.ToCoord == default ? new Vector2Int(to.X, to.Y) : fact.ToCoord;
            Direction fromDirection = from.Direction;
            Direction toDirection = to.Direction;
            if (IsNoOpEntityFact(fact, from, to, fromCoord, toCoord))
            {
                return;
            }

            ClientAnimationStyle style = styleProvider.Resolve(motionKind, string.Empty, to);
            entityEvents.Add(new ClientAnimationEvent(
                entityId,
                fact.ServerTick == 0 ? serverTick : fact.ServerTick,
                fromCoord,
                toCoord,
                fromDirection,
                toDirection,
                motionKind,
                style.StyleId,
                style,
                fact.Direction));
        }

        private void PlanRotateGroup(long serverTick, IReadOnlyDictionary<long, EntitySnapshot> before, IReadOnlyDictionary<long, EntitySnapshot> after, ClientPresentationFact fact, List<RotatePivotGroupPlaybackPlan> rotateGroups)
        {
            if (fact.Members.Count == 0)
            {
                return;
            }

            ClientAnimationMotionKind motionKind = fact.ResultKind == PresentationFactResultKind.Bounce
                ? ClientAnimationMotionKind.RotatePivotBounce
                : ClientAnimationMotionKind.RotatePivot;
            EntitySnapshot pivotSnapshot = TryGetSnapshot(after, before, fact.PivotEntityId, out EntitySnapshot foundPivot)
                ? foundPivot
                : ClientPresentationHelpers.EmptySnapshot(fact.PivotEntityId, fact.PivotCoord, serverTick);
            ClientAnimationStyle style = styleProvider.Resolve(motionKind, string.Empty, pivotSnapshot);
            long startTick = fact.StartTick == 0 ? fact.ServerTick == 0 ? serverTick : fact.ServerTick : fact.StartTick;
            long endTick = fact.EndTick == 0 ? startTick : fact.EndTick;
            long contactTick = fact.ContactTick;
            if (endTick > startTick)
            {
                style = style.WithDuration((endTick - startTick) * 0.2f);
            }

            var members = new List<RotatePivotGroupMemberPlaybackPlan>(fact.Members.Count);
            for (int i = 0; i < fact.Members.Count; i++)
            {
                ClientPresentationFactMember member = fact.Members[i];
                Vector2Int toCoord = after.TryGetValue(member.EntityId, out EntitySnapshot memberAfter)
                    ? new Vector2Int(memberAfter.X, memberAfter.Y)
                    : member.ToCoord;
                members.Add(new RotatePivotGroupMemberPlaybackPlan(
                    member.EntityId,
                    member.FromCoord,
                    toCoord,
                    member.FromDirection,
                    member.ToDirection,
                    member.FromPortLocalPorts,
                    member.ToPortLocalPorts));
            }

            string groupId = RotateGroupId(fact.ServerTick == 0 ? serverTick : fact.ServerTick, fact.FactId, fact.PivotEntityId, fact.PivotCoord, fact.RotateDirection, fact.ResultKind);
            rotateGroups.Add(new RotatePivotGroupPlaybackPlan(
                groupId,
                fact.ServerTick == 0 ? serverTick : fact.ServerTick,
                startTick,
                contactTick,
                endTick,
                fact.ContactProgress,
                fact.PivotEntityId,
                fact.PivotCoord,
                fact.RotateDirection,
                fact.ResultKind,
                style,
                members));
        }

        private void PlanTranslateGroup(long serverTick, IReadOnlyDictionary<long, EntitySnapshot> before, IReadOnlyDictionary<long, EntitySnapshot> after, ClientPresentationFact fact, List<TranslateGroupPlaybackPlan> translateGroups)
        {
            if (fact.Members.Count == 0)
            {
                return;
            }

            EntitySnapshot sourceSnapshot = TryGetSnapshot(after, before, fact.PrimarySubjectEntityId, out EntitySnapshot found)
                ? found
                : ClientPresentationHelpers.EmptySnapshot(fact.PrimarySubjectEntityId, fact.ToCoord, serverTick);
            ClientAnimationStyle style = styleProvider.Resolve(ClientAnimationMotionKind.MechanismPush, string.Empty, sourceSnapshot);
            long startTick = fact.StartTick == 0 ? fact.ServerTick == 0 ? serverTick : fact.ServerTick : fact.StartTick;
            long endTick = fact.EndTick == 0 ? startTick : fact.EndTick;
            if (endTick > startTick)
            {
                style = style.WithDuration((endTick - startTick) * 0.2f);
            }

            var members = new List<RotatePivotGroupMemberPlaybackPlan>(fact.Members.Count);
            for (int i = 0; i < fact.Members.Count; i++)
            {
                ClientPresentationFactMember member = fact.Members[i];
                Vector2Int toCoord = after.TryGetValue(member.EntityId, out EntitySnapshot memberAfter)
                    ? new Vector2Int(memberAfter.X, memberAfter.Y)
                    : member.ToCoord;
                members.Add(new RotatePivotGroupMemberPlaybackPlan(
                    member.EntityId,
                    member.FromCoord,
                    toCoord,
                    member.FromDirection,
                    member.ToDirection,
                    member.FromPortLocalPorts,
                    member.ToPortLocalPorts));
            }

            string groupId = TranslateGroupId(fact.ServerTick == 0 ? serverTick : fact.ServerTick, fact.FactId, fact.SourceActionId);
            translateGroups.Add(new TranslateGroupPlaybackPlan(groupId, fact.ServerTick == 0 ? serverTick : fact.ServerTick, startTick, endTick, fact.Direction, style, members));
        }

        private void PlanImpact(long serverTick, IReadOnlyDictionary<long, EntitySnapshot> before, IReadOnlyDictionary<long, EntitySnapshot> after, ClientPresentationFact fact, List<ClientAnimationEvent> output)
        {
            if (fact.Impacts.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fact.Impacts.Count; i++)
            {
                ClientPresentationFactImpact impact = fact.Impacts[i];
                long entityId = impact.ImpactMemberId == 0 ? impact.BlockerEntityId : impact.ImpactMemberId;
                EntitySnapshot snapshot = TryGetSnapshot(after, before, entityId, out EntitySnapshot found)
                    ? found
                    : ClientPresentationHelpers.EmptySnapshot(entityId, impact.ImpactToCoord, serverTick);
                ClientAnimationStyle style = styleProvider.Resolve(ClientAnimationMotionKind.MechanismPush, "pivot.impact", snapshot);
                output.Add(new ClientAnimationEvent(
                    entityId,
                    fact.ServerTick == 0 ? serverTick : fact.ServerTick,
                    impact.ImpactFromCoord,
                    impact.ImpactToCoord,
                    snapshot.Direction,
                    snapshot.Direction,
                    ClientAnimationMotionKind.MechanismPush,
                    style.StyleId,
                    style,
                    impact.PushDirection));
            }
        }

        private static ClientAnimationMotionKind ResolveMotionKind(ClientPresentationFact fact, IReadOnlyDictionary<long, EntitySnapshot> after, IReadOnlyDictionary<long, EntitySnapshot> before)
        {
            return fact.FactType switch
            {
                PresentationFactType.EntityMoved => TryGetSnapshot(after, before, fact.PrimarySubjectEntityId, out EntitySnapshot snapshot) && snapshot.AutoMove
                    ? ClientAnimationMotionKind.AutoMove
                    : TryGetSnapshot(after, before, fact.PrimarySubjectEntityId, out snapshot) && !snapshot.PlayerControlled
                    ? ClientAnimationMotionKind.DebugDrag
                    : ClientAnimationMotionKind.PlayerMove,
                PresentationFactType.EntityPushed => ClientAnimationMotionKind.MechanismPush,
                PresentationFactType.BodyMoved => ClientAnimationMotionKind.MechanismPush,
                PresentationFactType.EntitySpawned => ClientAnimationMotionKind.Spawn,
                PresentationFactType.EntityRemoved => ClientAnimationMotionKind.Remove,
                _ => ClientAnimationMotionKind.Unknown
            };
        }

        private static bool IsNoOpEntityFact(ClientPresentationFact fact, EntitySnapshot from, EntitySnapshot to, Vector2Int fromCoord, Vector2Int toCoord)
        {
            if (fact.FactType != PresentationFactType.EntityMoved && fact.FactType != PresentationFactType.EntityPushed)
            {
                return false;
            }

            return fromCoord == toCoord && from.Direction == to.Direction && fact.Direction == Direction.None;
        }

        private static bool TryGetSnapshot(IReadOnlyDictionary<long, EntitySnapshot> after, IReadOnlyDictionary<long, EntitySnapshot> before, long entityId, out EntitySnapshot snapshot)
        {
            if (after.TryGetValue(entityId, out snapshot))
            {
                return true;
            }

            return before.TryGetValue(entityId, out snapshot);
        }

        private static HashSet<long> CollectGroupMembers(IReadOnlyList<ClientPresentationFact> facts)
        {
            var members = new HashSet<long>();
            for (int i = 0; i < facts.Count; i++)
            {
                ClientPresentationFact fact = facts[i];
                if (fact.FactType != PresentationFactType.RotatePivotGroup && fact.FactType != PresentationFactType.BodyMoved)
                {
                    continue;
                }

                for (int memberIndex = 0; memberIndex < fact.Members.Count; memberIndex++)
                {
                    members.Add(fact.Members[memberIndex].EntityId);
                }
            }

            return members;
        }

        private static void AddBehaviorPlan(long fallbackServerTick, ClientPresentationFact fact, List<ClientAnimationEvent> entityEvents, List<RotatePivotGroupPlaybackPlan> rotateGroups, List<TranslateGroupPlaybackPlan> translateGroups, int entityStart, int rotateStart, int translateStart, List<BehaviorPlaybackPlan> output)
        {
            int entityCount = entityEvents.Count - entityStart;
            int rotateCount = rotateGroups.Count - rotateStart;
            int translateCount = translateGroups.Count - translateStart;
            if (entityCount <= 0 && rotateCount <= 0 && translateCount <= 0)
            {
                return;
            }

            var entityTracks = new List<EntityTrackPlaybackPlan>(entityCount);
            var feedbackTracks = new List<FeedbackTrackPlaybackPlan>();
            for (int i = entityStart; i < entityEvents.Count; i++)
            {
                ClientAnimationEvent animationEvent = entityEvents[i];
                entityTracks.Add(new EntityTrackPlaybackPlan(animationEvent));
                if (fact.FactType == PresentationFactType.RotatePivotImpact)
                {
                    feedbackTracks.Add(new FeedbackTrackPlaybackPlan(animationEvent));
                }
            }

            var groupTracks = new List<RigidBodyGroupTrackPlaybackPlan>(rotateCount);
            var connectionTracks = new List<PortConnectionTrackPlaybackPlan>();
            for (int i = rotateStart; i < rotateGroups.Count; i++)
            {
                RotatePivotGroupPlaybackPlan rotateGroup = rotateGroups[i];
                groupTracks.Add(new RigidBodyGroupTrackPlaybackPlan(rotateGroup.GroupId, RigidBodyGroupMotionKind.RotateAroundPivot, rotateGroup));
                for (int first = 0; first < rotateGroup.Members.Count; first++)
                {
                    for (int second = first + 1; second < rotateGroup.Members.Count; second++)
                    {
                        long from = rotateGroup.Members[first].EntityId;
                        long to = rotateGroup.Members[second].EntityId;
                        connectionTracks.Add(new PortConnectionTrackPlaybackPlan(ConnectionKey(from, to), from, to));
                    }
                }
            }

            for (int i = translateStart; i < translateGroups.Count; i++)
            {
                TranslateGroupPlaybackPlan translateGroup = translateGroups[i];
                groupTracks.Add(new RigidBodyGroupTrackPlaybackPlan(translateGroup.GroupId, RigidBodyGroupMotionKind.Translate, translateGroup));
                for (int first = 0; first < translateGroup.Members.Count; first++)
                {
                    for (int second = first + 1; second < translateGroup.Members.Count; second++)
                    {
                        long from = translateGroup.Members[first].EntityId;
                        long to = translateGroup.Members[second].EntityId;
                        connectionTracks.Add(new PortConnectionTrackPlaybackPlan(ConnectionKey(from, to), from, to));
                    }
                }
            }

            long serverTick = fact.ServerTick == 0 ? fallbackServerTick : fact.ServerTick;
            var plan = new BehaviorPlaybackPlan(
                BehaviorPlanId(fact, serverTick),
                fact.SourceActionId,
                serverTick,
                fact.StartTick,
                fact.ContactTick,
                fact.EndTick,
                fact.ContactProgress,
                entityTracks,
                groupTracks,
                connectionTracks,
                feedbackTracks);
            AddOrMergeBehaviorPlan(output, plan);
        }

        private static void AddOrMergeBehaviorPlan(List<BehaviorPlaybackPlan> output, BehaviorPlaybackPlan incoming)
        {
            for (int i = 0; i < output.Count; i++)
            {
                BehaviorPlaybackPlan existing = output[i];
                if (!string.Equals(existing.PlanId, incoming.PlanId, StringComparison.Ordinal))
                {
                    continue;
                }

                output[i] = new BehaviorPlaybackPlan(
                    existing.PlanId,
                    existing.SourceActionId == 0 ? incoming.SourceActionId : existing.SourceActionId,
                    existing.ServerTick,
                    existing.StartTick == 0 ? incoming.StartTick : existing.StartTick,
                    existing.ContactTick == 0 ? incoming.ContactTick : existing.ContactTick,
                    existing.EndTick == 0 ? incoming.EndTick : existing.EndTick,
                    existing.ContactProgress == 0d ? incoming.ContactProgress : existing.ContactProgress,
                    Concat(existing.EntityTracks, incoming.EntityTracks),
                    Concat(existing.GroupTracks, incoming.GroupTracks),
                    Concat(existing.ConnectionTracks, incoming.ConnectionTracks),
                    Concat(existing.FeedbackTracks, incoming.FeedbackTracks));
                return;
            }

            output.Add(incoming);
        }

        private static IReadOnlyList<T> Concat<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
        {
            if (first == null || first.Count == 0)
            {
                return second == null || second.Count == 0 ? Array.Empty<T>() : new List<T>(second).ToArray();
            }

            if (second == null || second.Count == 0)
            {
                return new List<T>(first).ToArray();
            }

            var result = new T[first.Count + second.Count];
            for (int i = 0; i < first.Count; i++)
            {
                result[i] = first[i];
            }

            for (int i = 0; i < second.Count; i++)
            {
                result[first.Count + i] = second[i];
            }

            return result;
        }

        private static string BehaviorPlanId(ClientPresentationFact fact, long serverTick)
        {
            if (fact.SourceActionId != 0 && fact.StartTick != 0 && fact.EndTick != 0)
            {
                return fact.SourceActionId + "|" + fact.StartTick + "|" + fact.EndTick;
            }

            long subject = fact.PrimarySubjectEntityId;
            long factId = fact.FactId == 0 ? subject : fact.FactId;
            return serverTick + "|" + factId;
        }

        private static string ConnectionKey(long first, long second)
        {
            return first <= second ? first + ":" + second : second + ":" + first;
        }

        private static string RotateGroupId(long serverTick, long factId, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, PresentationFactResultKind resultKind)
        {
            return serverTick + "|" + factId + "|" + pivotEntityId + "|" + pivotCoord.x + "," + pivotCoord.y + "|" + rotateDirection + "|" + resultKind;
        }

        private static string TranslateGroupId(long serverTick, long factId, long sourceActionId)
        {
            return "translate|" + serverTick + "|" + factId + "|" + sourceActionId;
        }
    }

    internal static class ClientPresentationHelpers
    {
        public static EntitySnapshot EmptySnapshot(long entityId, Vector2Int coord, long serverTick)
        {
            return new EntitySnapshot(entityId, 0, 0, 0, coord.x, coord.y, Direction.None, false, false, false, false, 1, false, false, DirectionMask.None, false, true, true, serverTick);
        }
    }

    public sealed class ClientAnimationLayer
    {
        private readonly Queue<ClientAnimationEvent> pendingEvents = new();
        private readonly Queue<RotatePivotGroupPlaybackPlan> pendingRotateGroups = new();
        private readonly Queue<TranslateGroupPlaybackPlan> pendingTranslateGroups = new();
        private readonly List<ClientAnimationEvent> recentEvents = new();
        private readonly ClientPresentationPlaybackPlanner playbackPlanner;
        private readonly int recentLimit;

        public ClientAnimationLayer(ClientAnimationStyleProvider styleProvider, int recentLimit = 16)
        {
            ClientAnimationStyleProvider resolvedStyleProvider = styleProvider ?? ClientAnimationStyleProvider.Fallback();
            playbackPlanner = new ClientPresentationPlaybackPlanner(resolvedStyleProvider);
            this.recentLimit = Mathf.Max(1, recentLimit);
            Enabled = true;
        }

        public bool Enabled { get; set; }
        public int PendingCount => pendingEvents.Count;
        public int PendingRotateGroupCount => pendingRotateGroups.Count + pendingTranslateGroups.Count;
        public IReadOnlyList<ClientAnimationEvent> RecentEvents => recentEvents;
        public string RecentSummary { get; private set; } = string.Empty;

        public void Clear()
        {
            pendingEvents.Clear();
            pendingRotateGroups.Clear();
            pendingTranslateGroups.Clear();
            recentEvents.Clear();
            playbackPlanner.Clear();
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

        public bool TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan plan)
        {
            if (!Enabled || pendingRotateGroups.Count == 0)
            {
                plan = default;
                return false;
            }

            plan = pendingRotateGroups.Dequeue();
            return true;
        }

        public bool TryDequeueTranslateGroup(out TranslateGroupPlaybackPlan plan)
        {
            if (!Enabled || pendingTranslateGroups.Count == 0)
            {
                plan = default;
                return false;
            }

            plan = pendingTranslateGroups.Dequeue();
            return true;
        }

        public void CaptureSnapshotApplyFacts(long serverTick, IReadOnlyList<EntitySnapshot> before, IReadOnlyList<EntitySnapshot> after, IReadOnlyList<ClientPresentationFact> presentationFacts)
        {
            if (!Enabled)
            {
                return;
            }

            var beforeByEntity = MapSnapshots(before);
            var afterByEntity = MapSnapshots(after);
            ClientPresentationPlaybackPlan playbackPlan = playbackPlanner.Plan(serverTick, beforeByEntity, afterByEntity, presentationFacts);
            for (int i = 0; i < playbackPlan.EntityEvents.Count; i++)
            {
                Enqueue(playbackPlan.EntityEvents[i]);
            }

            for (int i = 0; i < playbackPlan.RotateGroups.Count; i++)
            {
                pendingRotateGroups.Enqueue(playbackPlan.RotateGroups[i]);
            }

            for (int i = 0; i < playbackPlan.BehaviorPlans.Count; i++)
            {
                BehaviorPlaybackPlan behaviorPlan = playbackPlan.BehaviorPlans[i];
                for (int trackIndex = 0; trackIndex < behaviorPlan.GroupTracks.Count; trackIndex++)
                {
                    RigidBodyGroupTrackPlaybackPlan groupTrack = behaviorPlan.GroupTracks[trackIndex];
                    if (groupTrack.MotionKind == RigidBodyGroupMotionKind.Translate)
                    {
                        pendingTranslateGroups.Enqueue(groupTrack.Translate);
                    }
                }
            }
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

    }
}
