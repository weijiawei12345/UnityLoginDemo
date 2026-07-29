using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARPG.Networking.Lobby
{
    /// <summary>
    /// Runtime-built lobby UI. The view never starts or stops a runner directly.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class FusionLobbyView : MonoBehaviour
    {
        private static readonly Color Background = new Color32(22, 25, 29, 255);
        private static readonly Color Surface = new Color32(31, 35, 40, 255);
        private static readonly Color SurfaceRaised = new Color32(42, 47, 53, 255);
        private static readonly Color Accent = new Color32(72, 170, 112, 255);
        private static readonly Color TextPrimary = new Color32(235, 239, 242, 255);
        private static readonly Color TextMuted = new Color32(158, 167, 175, 255);

        private readonly List<Button> _commandButtons = new List<Button>();
        private FusionSessionCoordinator _coordinator;
        private TMP_InputField _roomNameInput;
        private TMP_Text _statusText;
        private TMP_Text _roomCountText;
        private RectTransform _roomContent;
        private Button _refreshButton;

        private void Awake()
        {
            ConfigureCanvas();
            BuildInterface();
        }

        private void Start()
        {
            _coordinator = FusionSessionCoordinator.Instance;
            if (_coordinator == null)
            {
                _statusText.text = "Session coordinator is unavailable.";
                SetCommandsInteractable(false);
                return;
            }

            _coordinator.StateChanged += HandleStateChanged;
            _coordinator.RoomsChanged += RenderRooms;
            RenderRooms(_coordinator.Rooms);
            HandleStateChanged(_coordinator.State, _coordinator.StatusMessage);
        }

        private void OnDestroy()
        {
            if (_coordinator == null)
            {
                return;
            }

            _coordinator.StateChanged -= HandleStateChanged;
            _coordinator.RoomsChanged -= RenderRooms;
        }

        private void ConfigureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildInterface()
        {
            RectTransform root = transform as RectTransform;
            CreateImage("Background", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);

            RectTransform header = CreateImage(
                "Header",
                root,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, -76f),
                Vector2.zero,
                Surface);
            CreateText("Title", header, "ARPG Rooms", 30, FontStyles.Bold,
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(32f, 0f), Vector2.zero,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            _statusText = CreateText("Status", header, "Connecting...", 18, FontStyles.Normal,
                new Vector2(0.5f, 0f), Vector2.one, Vector2.zero, new Vector2(-32f, 0f),
                TextAlignmentOptions.MidlineRight, TextMuted);

            RectTransform controls = CreateImage(
                "Controls",
                root,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(28f, 28f),
                new Vector2(468f, -104f),
                Surface);
            CreateText("ControlsTitle", controls, "ROOM ACCESS", 16, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -64f), new Vector2(-24f, -22f),
                TextAlignmentOptions.BottomLeft, TextMuted);
            CreateText("RoomNameLabel", controls, "Room name", 18, FontStyles.Normal,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -118f), new Vector2(-24f, -82f),
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            _roomNameInput = CreateInputField(
                "RoomNameInput",
                controls,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(24f, -174f),
                new Vector2(-24f, -124f),
                "team-01");
            _roomNameInput.characterLimit = FusionRoomRules.MaxRoomNameLength;

            Button createButton = CreateButton("CreateButton", controls, "Create room", Accent,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -238f), new Vector2(-24f, -188f));
            Button joinButton = CreateButton("JoinButton", controls, "Join by name", SurfaceRaised,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -302f), new Vector2(-24f, -252f));
            Button quickButton = CreateButton("QuickButton", controls, "Quick match", SurfaceRaised,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -366f), new Vector2(-24f, -316f));
            _refreshButton = CreateButton("RefreshButton", controls, "Refresh", SurfaceRaised,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -430f), new Vector2(-24f, -380f));

            createButton.onClick.AddListener(CreateRoom);
            joinButton.onClick.AddListener(JoinRoom);
            quickButton.onClick.AddListener(QuickMatch);
            _refreshButton.onClick.AddListener(RefreshRooms);
            _commandButtons.Add(createButton);
            _commandButtons.Add(joinButton);
            _commandButtons.Add(quickButton);

            RectTransform rooms = CreateRect(
                "Rooms",
                root,
                Vector2.zero,
                Vector2.one,
                new Vector2(496f, 28f),
                new Vector2(-28f, -104f));
            CreateText("RoomsTitle", rooms, "AVAILABLE ROOMS", 16, FontStyles.Bold,
                new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(0f, -38f), Vector2.zero,
                TextAlignmentOptions.MidlineLeft, TextMuted);
            _roomCountText = CreateText("RoomCount", rooms, "0 rooms", 16, FontStyles.Normal,
                new Vector2(0.7f, 1f), Vector2.one, new Vector2(0f, -38f), Vector2.zero,
                TextAlignmentOptions.MidlineRight, TextMuted);

            RectTransform viewport = CreateImage(
                "Viewport",
                rooms,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 0f),
                new Vector2(0f, -52f),
                new Color(0f, 0f, 0f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            _roomContent = CreateRect("Content", viewport, new Vector2(0f, 1f), Vector2.one,
                Vector2.zero, Vector2.zero);
            _roomContent.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = _roomContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _roomContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = rooms.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _roomContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private async void CreateRoom()
        {
            await _coordinator.CreateRoomAsync(_roomNameInput.text);
        }

        private async void JoinRoom()
        {
            await _coordinator.JoinRoomAsync(_roomNameInput.text);
        }

        private async void QuickMatch()
        {
            await _coordinator.QuickMatchAsync();
        }

        private async void RefreshRooms()
        {
            if (_coordinator.State == FusionSessionState.Lobby)
            {
                _coordinator.RefreshRooms();
            }
            else
            {
                await _coordinator.JoinLobbyAsync();
            }
        }

        private void HandleStateChanged(FusionSessionState state, string message)
        {
            _statusText.text = message;
            SetCommandsInteractable(state == FusionSessionState.Lobby);
            _refreshButton.interactable = state == FusionSessionState.Lobby || state == FusionSessionState.Error;
        }

        private void SetCommandsInteractable(bool interactable)
        {
            for (int i = 0; i < _commandButtons.Count; i++)
            {
                _commandButtons[i].interactable = interactable;
            }

            if (_refreshButton != null)
            {
                _refreshButton.interactable = interactable;
            }
        }

        private void RenderRooms(IReadOnlyList<LobbyRoomSnapshot> rooms)
        {
            for (int i = _roomContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_roomContent.GetChild(i).gameObject);
            }

            int count = rooms == null ? 0 : rooms.Count;
            _roomCountText.text = count == 1 ? "1 room" : $"{count} rooms";
            if (count == 0)
            {
                TMP_Text empty = CreateText("Empty", _roomContent, "No public rooms", 20, FontStyles.Normal,
                    new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -72f), new Vector2(-18f, 0f),
                    TextAlignmentOptions.MidlineLeft, TextMuted);
                AddLayoutHeight(empty.gameObject, 72f);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                LobbyRoomSnapshot room = rooms[i];
                string availability = room.CanJoin ? "OPEN" : "UNAVAILABLE";
                string label = $"{room.Name}    {room.PlayerCount}/{room.MaxPlayers}    {room.Difficulty.ToUpperInvariant()}    {availability}";
                Button row = CreateButton("Room_" + room.Name, _roomContent, label,
                    room.CanJoin ? SurfaceRaised : Surface,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                AddLayoutHeight(row.gameObject, 64f);
                row.interactable = room.CanJoin;
                string selectedName = room.Name;
                row.onClick.AddListener(() => JoinRoomFromList(selectedName));
            }
        }

        private async void JoinRoomFromList(string roomName)
        {
            _roomNameInput.text = roomName;
            await _coordinator.JoinRoomAsync(roomName);
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            TextAlignmentOptions alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static TMP_InputField CreateInputField(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            string placeholderText)
        {
            RectTransform root = CreateImage(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, SurfaceRaised);
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            RectTransform textArea = CreateRect("Text Area", root, Vector2.zero, Vector2.one,
                new Vector2(16f, 6f), new Vector2(-16f, -6f));
            textArea.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = CreateText("Placeholder", textArea, placeholderText, 19, FontStyles.Normal,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, TextMuted);
            TMP_Text text = CreateText("Text", textArea, string.Empty, 19, FontStyles.Normal,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.MidlineLeft, TextPrimary);
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string text,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateImage(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, color);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
            button.colors = colors;
            CreateText("Label", rect, text, 18, FontStyles.Bold,
                Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f),
                TextAlignmentOptions.Center, TextPrimary);
            return button;
        }

        private static void AddLayoutHeight(GameObject target, float height)
        {
            LayoutElement element = target.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }
    }
}
