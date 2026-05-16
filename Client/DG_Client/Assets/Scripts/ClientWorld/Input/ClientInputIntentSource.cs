using DG.GameCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DG.Map
{
    public sealed class ClientInputIntentSource : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private bool submitOnPerformed = true;

        private InputAction runtimeMoveAction;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            runtimeMoveAction = moveAction != null ? moveAction.action : CreateDefaultMoveAction();
        }

        private void OnEnable()
        {
            if (runtimeMoveAction == null)
            {
                runtimeMoveAction = CreateDefaultMoveAction();
            }

            runtimeMoveAction.Enable();
            runtimeMoveAction.performed += OnMovePerformed;
        }

        private void OnDisable()
        {
            if (runtimeMoveAction == null)
            {
                return;
            }

            runtimeMoveAction.performed -= OnMovePerformed;
            runtimeMoveAction.Disable();
        }

        public ClientDeclaredInputIntent CreateMoveIntent(Vector2 value, long clientInputId, long beatTick, long clientTick)
        {
            Direction direction = ResolveDirection(value);
            long entityId = ResolveLocalEntityId();
            return new ClientDeclaredInputIntent(clientInputId, entityId, ClientInputKind.Move, ClientInputSourceKind.Player, direction, beatTick, ClientRhythmJudge.None, clientTick);
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (!submitOnPerformed || networkSubmitter == null || !networkSubmitter.ServerAuthoritative || !networkSubmitter.HasJoined)
            {
                return;
            }

            Direction direction = ResolveDirection(context.ReadValue<Vector2>());
            if (direction == Direction.None)
            {
                return;
            }

            long clientTick = runner != null && runner.Context != null ? runner.Context.TickIndex : 0;
            networkSubmitter.SubmitDirection(networkSubmitter.LocalEntityId, direction, clientTick, 0);
        }

        private long ResolveLocalEntityId()
        {
            if (networkSubmitter != null && networkSubmitter.HasJoined)
            {
                return networkSubmitter.LocalEntityId;
            }

            return ClientMoveNetworkRuntime.HasLocalEntity ? ClientMoveNetworkRuntime.LocalEntityId : 0;
        }

        private static Direction ResolveDirection(Vector2 value)
        {
            if (Mathf.Abs(value.x) >= Mathf.Abs(value.y))
            {
                if (value.x > 0.5f)
                {
                    return Direction.Right;
                }

                if (value.x < -0.5f)
                {
                    return Direction.Left;
                }
            }

            if (value.y > 0.5f)
            {
                return Direction.Up;
            }

            if (value.y < -0.5f)
            {
                return Direction.Down;
            }

            return Direction.None;
        }

        private static InputAction CreateDefaultMoveAction()
        {
            var action = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            return action;
        }
    }

    public enum ClientRhythmJudge
    {
        None = 0,
        Perfect = 1,
        Good = 2,
        Late = 3,
        Miss = 4
    }

    public readonly struct ClientDeclaredInputIntent
    {
        public ClientDeclaredInputIntent(long clientInputId, long actorEntityId, ClientInputKind inputKind, ClientInputSourceKind sourceKind, Direction direction, long beatTick, ClientRhythmJudge rhythmJudge, long clientTick)
        {
            ClientInputId = clientInputId;
            ActorEntityId = actorEntityId;
            InputKind = inputKind;
            SourceKind = sourceKind;
            Direction = direction;
            BeatTick = beatTick;
            RhythmJudge = rhythmJudge;
            ClientTick = clientTick;
        }

        public long ClientInputId { get; }
        public long ActorEntityId { get; }
        public ClientInputKind InputKind { get; }
        public ClientInputSourceKind SourceKind { get; }
        public Direction Direction { get; }
        public long BeatTick { get; }
        public ClientRhythmJudge RhythmJudge { get; }
        public long ClientTick { get; }
    }
}
