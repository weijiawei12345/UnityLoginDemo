using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARPG.Networking.Lobby
{
    /// <summary>Provides the room exit command inside the networked Play scene.</summary>
    public sealed class FusionRoomLeaveView : MonoBehaviour
    {
        private Button _leaveButton;

        private void Start()
        {
            BuildButton();
            _leaveButton.gameObject.SetActive(FusionSessionCoordinator.Instance != null);
        }

        private void BuildButton()
        {
            GameObject root = new GameObject("LeaveRoomButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(176f, 48f);
            root.GetComponent<Image>().color = new Color32(42, 47, 53, 240);

            _leaveButton = root.GetComponent<Button>();
            _leaveButton.onClick.AddListener(LeaveRoom);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "Leave room";
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(235, 239, 242, 255);
        }

        private async void LeaveRoom()
        {
            FusionSessionCoordinator coordinator = FusionSessionCoordinator.Instance;
            if (coordinator == null)
            {
                return;
            }

            _leaveButton.interactable = false;
            await coordinator.LeaveToLobbyAsync();
        }
    }
}
