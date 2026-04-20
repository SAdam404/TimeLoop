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
        public GameObject LoopDragHandle;
        public InputField RepeatInput;
        public Button RepeatMinusButton;
        public Button RepeatPlusButton;
        public Button AddEntryButton;
        public GameObject AddButtonsRow;

        public GameObject EntryHeaderRow;
        public GameObject EntryRowTemplate;
        public GameObject DurationHeaderRow;
        public GameObject DurationRowTemplate;
        public GameObject ControlsRowTemplate;

        public float BaseLoopSectionHeight;
        public float BaseLoopSectionPreferredHeight;
        public float BaseAddButtonsY;
        public float BaseEntryRowHeaderY;
        public float BaseEntryRowY;
        public float BaseEntryDurationHeaderY;
        public float BaseEntryDurationRowY;
        public float BaseEntryControlsRowY;
        public float EntryBlockYOffset;

        public readonly List<EntryUiRefs> EntryRows = new List<EntryUiRefs>();
        public readonly List<GameObject> DynamicEntryObjects = new List<GameObject>();
    }

    private sealed class EntryUiRefs
    {
        public GameObject HeaderRow;
        public GameObject NameRow;
        public GameObject DurationHeaderRow;
        public GameObject DurationRow;
        public GameObject ControlsRow;
        public GameObject DragHandle;

        public InputField NameInput;
        public InputField DurationInput;
        public Button DurationMinusButton;
        public Button DurationPlusButton;
        public Button ColorButton;
        public Button DuplicateButton;
        public Button DeleteButton;
    }

    private sealed class EntryDragRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public MainMenuHtmlController Owner;
        public int LoopIndex;
        public int EntryIndex;

        public void OnPointerDown(PointerEventData eventData)
        {
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
            Owner?.OnEntryDragEnd(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Owner?.OnEntryDragEnd(eventData);
        }
    }

    private sealed class LoopDragRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public MainMenuHtmlController Owner;
        public int LoopIndex;

        public void OnPointerDown(PointerEventData eventData)
        {
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
            Owner?.OnLoopDragEnd(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Owner?.OnLoopDragEnd(eventData);
        }
    }
}
