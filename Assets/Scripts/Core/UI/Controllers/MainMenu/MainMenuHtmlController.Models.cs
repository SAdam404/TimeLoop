using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private sealed class LoopUiRefs
    {
        public GameObject SectionRoot;
        public Text LoopNameLabel;
        public Text LoopTotalDurationLabel;
        public GameObject LoopDragHandle;
        public InputField RepeatInput;
        public Button RepeatMinusButton;
        public Button RepeatPlusButton;
        public Button AddEntryButton;
        public GameObject AddButtonsRow;

        public GameObject EntryHeaderRow;
        public GameObject EntryModeRowTemplate;
        public GameObject EntryRowTemplate;
        public GameObject DurationHeaderRow;
        public GameObject DurationRowTemplate;
        public GameObject RepCountHeaderRow;
        public GameObject RepCountRowTemplate;
        public GameObject ControlsRowTemplate;

        public float BaseLoopSectionHeight;
        public float BaseLoopSectionPreferredHeight;
        public float BaseAddButtonsY;
        public float BaseEntryModeRowY;
        public float BaseEntryRowHeaderY;
        public float BaseEntryRowY;
        public float BaseEntryDurationHeaderY;
        public float BaseEntryDurationRowY;
        public float BaseEntryRepCountHeaderY;
        public float BaseEntryRepCountRowY;
        public float BaseEntryControlsRowY;
        public float EntryBlockYOffset;

        public readonly List<EntryUiRefs> EntryRows = new List<EntryUiRefs>();
        public readonly List<GameObject> DynamicEntryObjects = new List<GameObject>();
    }

    private sealed class EntryUiRefs
    {
        public GameObject HeaderRow;
        public GameObject ModeRow;
        public GameObject NameRow;
        public GameObject DurationHeaderRow;
        public GameObject DurationRow;
        public GameObject RepCountHeaderRow;
        public GameObject RepCountRow;
        public GameObject ControlsRow;
        public GameObject DragHandle;

        public InputField NameInput;
        public InputField DurationInput;
        public InputField RepCountInput;
        
        public Button ModeTimeButton;
        public Button ModeRepsButton;
        public Button DurationMinusButton;
        public Button DurationPlusButton;
        public Button RepCountMinusButton;
        public Button RepCountPlusButton;
        public Button ColorButton;
        public Button DuplicateButton;
        public Button DeleteButton;
    }

    private sealed class EntryDragRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public MainMenuHtmlController Owner;
        public int LoopIndex;
        public int EntryIndex;

        private bool _isHovered;
        private bool _isPressed;

        private void RefreshVisual()
        {
            Owner?.SetDragHandleVisualState(gameObject, _isHovered, _isPressed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            RefreshVisual();
            Owner?.OnEntryDragBegin(LoopIndex, EntryIndex, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Owner?.OnEntryDragBegin(LoopIndex, EntryIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.OnEntryDragMove(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isPressed = false;
            RefreshVisual();
            Owner?.OnEntryDragEnd(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            RefreshVisual();
            Owner?.OnEntryDragEnd(eventData);
        }
    }

    private sealed class LoopDragRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public MainMenuHtmlController Owner;
        public int LoopIndex;

        private bool _isHovered;
        private bool _isPressed;

        private void RefreshVisual()
        {
            Owner?.SetDragHandleVisualState(gameObject, _isHovered, _isPressed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            RefreshVisual();
            Owner?.OnLoopDragBegin(LoopIndex, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Owner?.OnLoopDragBegin(LoopIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.OnLoopDragMove(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isPressed = false;
            RefreshVisual();
            Owner?.OnLoopDragEnd(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            RefreshVisual();
            Owner?.OnLoopDragEnd(eventData);
        }
    }

    private sealed class InputFieldScrollRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IScrollHandler
    {
        public MainMenuHtmlController Owner;
        public InputField TargetInput;

        private Vector2 _startPointerPos;
        private bool _isScrollDrag;

        public void OnPointerDown(PointerEventData eventData)
        {
            _startPointerPos = eventData.position;
            _isScrollDrag = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _startPointerPos;
            _isScrollDrag = Mathf.Abs(delta.y) >= 8f && Mathf.Abs(delta.y) >= Mathf.Abs(delta.x);
            if (!_isScrollDrag)
                return;

            if (TargetInput != null && TargetInput.isFocused)
                TargetInput.DeactivateInputField();

            Owner?.OnInputFieldScrollBegin(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollMove(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollEnd(eventData);

            _isScrollDrag = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollEnd(eventData);

            _isScrollDrag = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            Owner?.OnCreationScrollWheel(eventData);
        }
    }

    private sealed class ButtonScrollRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IScrollHandler
    {
        public MainMenuHtmlController Owner;

        private Vector2 _startPointerPos;
        private bool _isScrollDrag;

        public void OnPointerDown(PointerEventData eventData)
        {
            _startPointerPos = eventData.position;
            _isScrollDrag = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var delta = eventData.position - _startPointerPos;
            _isScrollDrag = Mathf.Abs(delta.y) >= 8f && Mathf.Abs(delta.y) >= Mathf.Abs(delta.x);
            if (!_isScrollDrag)
                return;

            Owner?.OnInputFieldScrollBegin(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollMove(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollEnd(eventData);

            _isScrollDrag = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isScrollDrag)
                Owner?.OnInputFieldScrollEnd(eventData);

            _isScrollDrag = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            Owner?.OnCreationScrollWheel(eventData);
        }
    }
}
