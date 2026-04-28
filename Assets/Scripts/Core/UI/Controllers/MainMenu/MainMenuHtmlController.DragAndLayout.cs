using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class MainMenuHtmlController
{
    private static readonly Color DragHandleIdleColor = new Color(0f, 0f, 0f, 0f);
    private static readonly Color DragHandleHoverColor = new Color(0f, 0f, 0f, 0.18f);
    private static readonly Color DragHandlePressedColor = new Color(0f, 0f, 0f, 0.28f);

    private void WireEntryDragSurface(GameObject dragSurface, int loopIndex, int index)
    {
        if (dragSurface == null)
            return;

        var surfaceImage = dragSurface.GetComponent<Image>();
        if (surfaceImage != null)
        {
            surfaceImage.raycastTarget = true;
            SetDragHandleVisualState(dragSurface, false, false);
        }

        var relay = dragSurface.GetComponent<EntryDragRelay>() ?? dragSurface.AddComponent<EntryDragRelay>();
        relay.Owner = this;
        relay.LoopIndex = loopIndex;
        relay.EntryIndex = index;
    }

    private void WireLoopDragTarget(LoopUiRefs loopUi, int loopIndex)
    {
        if (loopUi == null || loopUi.LoopDragHandle == null)
            return;

        var surfaceImage = loopUi.LoopDragHandle.GetComponent<Image>();
        if (surfaceImage != null)
        {
            surfaceImage.raycastTarget = true;
            SetDragHandleVisualState(loopUi.LoopDragHandle, false, false);
        }

        var relay = loopUi.LoopDragHandle.GetComponent<LoopDragRelay>() ?? loopUi.LoopDragHandle.AddComponent<LoopDragRelay>();
        relay.Owner = this;
        relay.LoopIndex = loopIndex;

        var images = loopUi.LoopDragHandle.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                continue;

            images[i].raycastTarget = images[i].gameObject == loopUi.LoopDragHandle;
        }
    }

    private void SetDragHandleVisualState(GameObject dragHandle, bool isHovered, bool isPressed)
    {
        if (dragHandle == null)
            return;

        var image = dragHandle.GetComponent<Image>();
        if (image == null)
            return;

        image.color = isPressed ? DragHandlePressedColor : (isHovered ? DragHandleHoverColor : DragHandleIdleColor);
    }

    private void OnLoopDragBegin(int loopIndex, PointerEventData eventData)
    {
        if (_isDraggingEntry || _isDraggingLoop)
            return;

        EnsureWorkingData();
        SyncUiToWorkingData();

        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count || loopIndex >= _workingPreset.loops.Count)
            return;

        _entryDragSpace = _creationScrollContent;
        if (_entryDragSpace == null)
        {
            var loopRoot = _loopUiSections[loopIndex].SectionRoot;
            _entryDragSpace = loopRoot != null ? loopRoot.transform.parent as RectTransform : null;
        }

        _isDraggingLoop = true;
        _loopDragCurrentIndex = loopIndex;
        _loopDragStartPointerY = GetPointerYInDragSpace(eventData);
        _loopDragLastPointerY = _loopDragStartPointerY;
        _loopDragBaseSectionY = GetAnchoredY(_loopUiSections[_loopDragCurrentIndex].SectionRoot);
        _nextLoopSwapAllowedTime = 0f;
        _lastLoopSwapDirection = 0;

        SetCreationScrollEnabled(false);
        BeginLoopDragVisual(_loopUiSections[_loopDragCurrentIndex].SectionRoot, elevateSorting: false);
    }

    private void OnLoopDragMove(PointerEventData eventData)
    {
        if (!_isDraggingLoop)
            return;

        _loopDragLastPointerY = GetPointerYInDragSpace(eventData);
        var dragDeltaY = _loopDragLastPointerY - _loopDragStartPointerY;
        ApplyDraggedLoopOffset(_loopDragCurrentIndex, dragDeltaY);
        TryReorderLoopDuringDrag(_loopDragLastPointerY);
    }

    private void OnLoopDragEnd(PointerEventData eventData)
    {
        if (!_isDraggingLoop)
            return;

        _loopDragLastPointerY = GetPointerYInDragSpace(eventData);
        EndLoopDrag();
    }

    private void EndLoopDrag()
    {
        if (_loopDragCurrentIndex >= 0 && _loopDragCurrentIndex < _loopUiSections.Count)
            ApplyDraggedLoopOffset(_loopDragCurrentIndex, 0f);

        _isDraggingLoop = false;
        _loopDragCurrentIndex = -1;
        _loopDragStartPointerY = 0f;
        _loopDragLastPointerY = 0f;
        _loopDragBaseSectionY = 0f;
        _nextLoopSwapAllowedTime = 0f;
        _lastLoopSwapDirection = 0;
        _entryDragSpace = null;
        EndLoopDragVisual();

        SetCreationScrollEnabled(true);
        RebuildLoopSections();
    }

    private void TryReorderLoopDuringDrag(float pointerY)
    {
        if (!_isDraggingLoop || _loopDragCurrentIndex < 0 || _loopDragCurrentIndex >= _loopUiSections.Count)
            return;

        if (Time.unscaledTime < _nextLoopSwapAllowedTime)
            return;

        var fromIndex = _loopDragCurrentIndex;
        var toIndex = CalculateLoopTargetIndex(pointerY);
        if (toIndex < 0 || toIndex >= _loopUiSections.Count || toIndex == fromIndex)
            return;

        MoveLoopData(fromIndex, toIndex);
        MoveLoopUiRefs(fromIndex, toIndex);
        ApplyLoopSectionSiblingOrder();

        // Reset drag baseline after each swap to avoid oscillation between neighbors.
        _loopDragStartPointerY = pointerY;
        _loopDragLastPointerY = pointerY;
        _lastLoopSwapDirection = Math.Sign(toIndex - fromIndex);
        _loopDragCurrentIndex = toIndex;
        _loopDragBaseSectionY = GetAnchoredY(_loopUiSections[_loopDragCurrentIndex].SectionRoot);
        _nextLoopSwapAllowedTime = Time.unscaledTime + 0.12f;

        var dragDeltaY = _loopDragLastPointerY - _loopDragStartPointerY;
        ApplyDraggedLoopOffset(_loopDragCurrentIndex, dragDeltaY);
    }

    private int CalculateLoopTargetIndex(float pointerY)
    {
        if (_loopDragCurrentIndex < 0 || _loopDragCurrentIndex >= _loopUiSections.Count)
            return _loopDragCurrentIndex;

        var deltaY = pointerY - _loopDragStartPointerY;
        var swapThreshold = Mathf.Clamp(GetLoopDragStep(_loopDragCurrentIndex) * 0.42f, 120f, 260f);

        var direction = 0; // +1 => move down, -1 => move up
        if (deltaY <= -swapThreshold)
            direction = 1;
        else if (deltaY >= swapThreshold)
            direction = -1;

        if (direction == 0)
            return _loopDragCurrentIndex;

        if (_lastLoopSwapDirection != 0 && direction != _lastLoopSwapDirection)
        {
            var reverseThreshold = swapThreshold * 1.75f;
            if (Mathf.Abs(deltaY) < reverseThreshold)
                return _loopDragCurrentIndex;
        }

        if (direction > 0 && _loopDragCurrentIndex < _loopUiSections.Count - 1)
            return _loopDragCurrentIndex + 1;

        if (direction < 0 && _loopDragCurrentIndex > 0)
            return _loopDragCurrentIndex - 1;

        return _loopDragCurrentIndex;
    }

    private float GetLoopDragStep(int loopIndex)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return 410f;

        var sectionRoot = _loopUiSections[loopIndex].SectionRoot;
        var rt = sectionRoot != null ? sectionRoot.GetComponent<RectTransform>() : null;
        var height = rt != null ? Mathf.Max(320f, rt.rect.height) : 398f;

        return height + 12f;
    }

    private void ApplyDraggedLoopOffset(int loopIndex, float deltaY)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return;

        var sectionRoot = _loopUiSections[loopIndex].SectionRoot;
        if (sectionRoot == null)
            return;

        SetAnchoredY(sectionRoot, _loopDragBaseSectionY + deltaY);
    }

    private void MoveLoopData(int fromIndex, int toIndex)
    {
        if (_workingPreset == null || _workingPreset.loops == null)
            return;

        if (fromIndex < 0 || fromIndex >= _workingPreset.loops.Count || toIndex < 0 || toIndex >= _workingPreset.loops.Count || fromIndex == toIndex)
            return;

        var moved = _workingPreset.loops[fromIndex] ?? new Loop();
        _workingPreset.loops.RemoveAt(fromIndex);
        _workingPreset.loops.Insert(toIndex, moved);
    }

    private void MoveLoopUiRefs(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _loopUiSections.Count || toIndex < 0 || toIndex >= _loopUiSections.Count || fromIndex == toIndex)
            return;

        var moved = _loopUiSections[fromIndex];
        _loopUiSections.RemoveAt(fromIndex);
        _loopUiSections.Insert(toIndex, moved);
    }

    private void ApplyLoopSectionSiblingOrder()
    {
        var addLoopPanel = uiHandler != null ? uiHandler.GetElement("AddLoopPanel") : null;
        if (addLoopPanel == null)
            return;

        var addLoopIndex = addLoopPanel.transform.GetSiblingIndex();
        var firstLoopIndex = Mathf.Max(0, addLoopIndex - _loopUiSections.Count);

        for (var i = 0; i < _loopUiSections.Count; i++)
        {
            var sectionRoot = _loopUiSections[i].SectionRoot;
            if (sectionRoot != null)
                sectionRoot.transform.SetSiblingIndex(firstLoopIndex + i);
        }

        addLoopPanel.transform.SetAsLastSibling();
    }

    private void BeginLoopDragVisual(GameObject sectionRoot, bool elevateSorting = true)
    {
        EndLoopDragVisual();

        if (sectionRoot == null)
            return;

        if (elevateSorting)
        {
            _activeLoopDragCanvas = sectionRoot.GetComponent<Canvas>();
            if (_activeLoopDragCanvas == null)
            {
                _activeLoopDragCanvas = sectionRoot.AddComponent<Canvas>();
                _activeLoopDragCanvasAdded = true;
            }
            else
            {
                _activeLoopDragCanvasAdded = false;
            }

            _activeLoopDragCanvasOriginalOverride = _activeLoopDragCanvas.overrideSorting;
            _activeLoopDragCanvasOriginalOrder = _activeLoopDragCanvas.sortingOrder;
            _activeLoopDragCanvas.overrideSorting = true;
            _activeLoopDragCanvas.sortingOrder = 500;
        }
        else
        {
            _activeLoopDragCanvas = null;
            _activeLoopDragCanvasAdded = false;
            _activeLoopDragCanvasOriginalOverride = false;
            _activeLoopDragCanvasOriginalOrder = 0;
        }

        _activeLoopDragCanvasGroup = sectionRoot.GetComponent<CanvasGroup>();
        if (_activeLoopDragCanvasGroup == null)
        {
            _activeLoopDragCanvasGroup = sectionRoot.AddComponent<CanvasGroup>();
            _activeLoopDragCanvasGroupAdded = true;
            _activeLoopDragCanvasGroupOriginalAlpha = 1f;
        }
        else
        {
            _activeLoopDragCanvasGroupAdded = false;
            _activeLoopDragCanvasGroupOriginalAlpha = _activeLoopDragCanvasGroup.alpha;
        }

        // Keep underlying loops visible while dragging a full loop block.
        _activeLoopDragCanvasGroup.alpha = 0.58f;
    }

    private void EndLoopDragVisual()
    {
        if (_activeLoopDragCanvasGroup != null)
        {
            if (_activeLoopDragCanvasGroupAdded)
            {
                Destroy(_activeLoopDragCanvasGroup);
            }
            else
            {
                _activeLoopDragCanvasGroup.alpha = _activeLoopDragCanvasGroupOriginalAlpha;
            }

            _activeLoopDragCanvasGroup = null;
            _activeLoopDragCanvasGroupAdded = false;
            _activeLoopDragCanvasGroupOriginalAlpha = 1f;
        }

        if (_activeLoopDragCanvas == null)
            return;

        if (_activeLoopDragCanvasAdded)
        {
            Destroy(_activeLoopDragCanvas);
        }
        else
        {
            _activeLoopDragCanvas.overrideSorting = _activeLoopDragCanvasOriginalOverride;
            _activeLoopDragCanvas.sortingOrder = _activeLoopDragCanvasOriginalOrder;
        }

        _activeLoopDragCanvas = null;
        _activeLoopDragCanvasAdded = false;
        _activeLoopDragCanvasOriginalOverride = false;
        _activeLoopDragCanvasOriginalOrder = 0;
    }

    private void OnEntryDragBegin(int loopIndex, int index, PointerEventData eventData)
    {
        if (_isDraggingLoop)
            return;

        EnsureWorkingData();
        SyncUiToWorkingData();

        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return;

        var loopUi = _loopUiSections[loopIndex];
        if (index < 0 || index >= loopUi.EntryRows.Count)
            return;

        _entryDragSpace = _creationScrollContent;
        if (_entryDragSpace == null)
        {
            var dragSpaceGo = loopUi.EntryRows[index].NameRow;
            _entryDragSpace = dragSpaceGo != null ? dragSpaceGo.transform.parent as RectTransform : null;
            if (_entryDragSpace == null)
                _entryDragSpace = loopUi.SectionRoot != null ? loopUi.SectionRoot.GetComponent<RectTransform>() : null;
        }

        _dragLoopIndex = loopIndex;
        _dragStartIndex = index;
        _dragCurrentIndex = index;
        _dragStartPointerY = GetPointerYInDragSpace(eventData);
        _dragLastPointerY = _dragStartPointerY;
        _dragPointerDeltaY = 0f;
        _isDraggingEntry = true;
        _nextSwapAllowedTime = 0f;
        _lastSwapDirection = 0;
        _previewLoopIndex = -1;
        _previewInsertIndex = -1;
        SetCreationScrollEnabled(false);
        BeginLoopDragVisual(_loopUiSections[loopIndex].SectionRoot);
        BringDraggedEntryToFront(loopIndex, index);
    }

    private void OnEntryDragMove(PointerEventData eventData)
    {
        if (!_isDraggingEntry)
            return;

        if (_dragLoopIndex < 0 || _dragLoopIndex >= _workingPreset.loops.Count || _dragLoopIndex >= _loopUiSections.Count)
            return;

        var loop = _workingPreset.loops[_dragLoopIndex];
        var loopUi = _loopUiSections[_dragLoopIndex];

        _dragLastPointerY = GetPointerYInDragSpace(eventData);
        _dragPointerDeltaY = _dragLastPointerY - _dragStartPointerY;

        // Reset all entries to their natural (clean) positions first.
        // TryReorderEntryDuringDrag relies on reading clean natural Y — no offset applied yet.
        ApplyEntryLayoutForCount(loopUi, loop != null && loop.entries != null ? loop.entries.Count : loopUi.EntryRows.Count);

        TryReorderEntryDuringDrag(_dragLastPointerY);

        // After any swap, _dragPointerDeltaY and _dragStartPointerY are updated; recompute delta.
        _dragPointerDeltaY = _dragLastPointerY - _dragStartPointerY;

        // Apply offset to dragged entry so it follows the finger.
        ApplyDraggedBlockOffset(_dragCurrentIndex, _dragPointerDeltaY);
        BringDraggedEntryToFront(_dragLoopIndex, _dragCurrentIndex);
        ApplyCrossLoopPreview(_dragLastPointerY);
    }

    private void OnEntryDragEnd(PointerEventData eventData)
    {
        if (!_isDraggingEntry)
            return;

        _dragLastPointerY = GetPointerYInDragSpace(eventData);
        EndEntryDrag(_dragLastPointerY);
    }

    private float GetPointerYInDragSpace(PointerEventData eventData)
    {
        if (_entryDragSpace != null && eventData != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_entryDragSpace, eventData.position, eventData.pressEventCamera, out var localPoint))
                return localPoint.y;
        }

        return eventData != null ? eventData.position.y : 0f;
    }

    private int CalculateDragTargetIndex(float pointerY)
    {
        if (_dragLoopIndex < 0 || _dragLoopIndex >= _workingPreset.loops.Count || _dragLoopIndex >= _loopUiSections.Count)
            return _dragCurrentIndex;

        var loop = _workingPreset.loops[_dragLoopIndex];
        var loopUi = _loopUiSections[_dragLoopIndex];
        if (_dragCurrentIndex < 0 || loop == null || loop.entries == null || loop.entries.Count == 0)
            return _dragCurrentIndex;

        var deltaY = pointerY - _dragStartPointerY;
        var currentBlockHeight = GetEntryBlockHeightForIndex(loopUi, loop, _dragCurrentIndex);
        var swapThreshold = Mathf.Clamp(currentBlockHeight * 0.42f, 72f, 220f);

        if (Time.unscaledTime < _nextSwapAllowedTime)
            return _dragCurrentIndex;

        var direction = 0; // +1 => move down in list, -1 => move up in list

        if (deltaY <= -swapThreshold)
            direction = 1;
        else if (deltaY >= swapThreshold)
            direction = -1;

        if (direction == 0)
            return _dragCurrentIndex;

        if (_lastSwapDirection != 0 && direction != _lastSwapDirection)
        {
            var reverseThreshold = swapThreshold * 1.8f;
            if (Mathf.Abs(deltaY) < reverseThreshold)
                return _dragCurrentIndex;
        }

        if (direction > 0 && _dragCurrentIndex < loop.entries.Count - 1)
            return _dragCurrentIndex + 1;

        if (direction < 0 && _dragCurrentIndex > 0)
            return _dragCurrentIndex - 1;

        return _dragCurrentIndex;
    }

    private void TryReorderEntryDuringDrag(float pointerY)
    {
        if (!_isDraggingEntry || _dragLoopIndex < 0 || _dragLoopIndex >= _workingPreset.loops.Count || _dragLoopIndex >= _loopUiSections.Count)
            return;

        var loop = _workingPreset.loops[_dragLoopIndex];
        var loopUi = _loopUiSections[_dragLoopIndex];
        if (loop == null || loop.entries == null || loop.entries.Count == 0)
            return;

        var fromIndex = _dragCurrentIndex;
        var toIndex = CalculateDragTargetIndex(pointerY);
        if (fromIndex < 0 || fromIndex >= loop.entries.Count || toIndex == fromIndex)
            return;

        // At this point ApplyEntryLayoutForCount was already called in OnEntryDragMove with NO offset
        // applied yet, so anchoredPosition values are clean natural positions.
        var oldNaturalY = GetDraggedEntryHeaderNaturalY(fromIndex);

        MoveEntry(_dragLoopIndex, fromIndex, toIndex);
        MoveEntryUiRefs(_dragLoopIndex, fromIndex, toIndex);

        _lastSwapDirection = Math.Sign(toIndex - fromIndex);
        _nextSwapAllowedTime = Time.unscaledTime + 0.11f;
        _dragCurrentIndex = toIndex;

        // Layout again so the entries are at new natural positions.
        ApplyEntryLayoutForCount(loopUi, loop.entries.Count);

        // Also clean natural Y — layout was just called, no offset applied.
        var newNaturalY = GetDraggedEntryHeaderNaturalY(_dragCurrentIndex);

        // Shift the baseline so the finger-to-entry visual offset is preserved.
        _dragStartPointerY += newNaturalY - oldNaturalY;
        _dragLastPointerY = pointerY;
        // _dragPointerDeltaY will be recomputed in OnEntryDragMove after we return.
    }

    private static float GetEntryBlockHeightForIndex(LoopUiRefs loopUi, Loop loop, int entryIndex)
    {
        if (loopUi == null)
            return 510f;

        var repsBlockHeight = Mathf.Max(240f, loopUi.EntryBlockYOffset);
        var compactTimeReduction = 122f;
        var timeBlockHeight = Mathf.Max(240f, repsBlockHeight - compactTimeReduction);

        if (loop == null || loop.entries == null || entryIndex < 0 || entryIndex >= loop.entries.Count)
            return repsBlockHeight;

        var entry = loop.entries[entryIndex];
        return entry != null && entry.mode == EntryMode.TIME ? timeBlockHeight : repsBlockHeight;
    }

    private void EndEntryDrag(float pointerY)
    {
        if (!_isDraggingEntry)
            return;

        var sourceLoopIndex = _dragLoopIndex;
        var sourceEntryIndex = _dragCurrentIndex;

        if (sourceLoopIndex >= 0 && sourceLoopIndex < _workingPreset.loops.Count)
        {
            ResolveDropTarget(pointerY, out var targetLoopIndex, out var targetEntryIndex);
            if (targetLoopIndex >= 0)
                MoveEntryToLoop(sourceLoopIndex, sourceEntryIndex, targetLoopIndex, targetEntryIndex);
        }

        ClearCrossLoopPreview();

        _dragLastPointerY = pointerY;
        _isDraggingEntry = false;
        _dragLoopIndex = -1;
        _dragStartIndex = -1;
        _dragCurrentIndex = -1;
        _dragPointerDeltaY = 0f;
        _entryDragSpace = null;
        _lastSwapDirection = 0;
        _nextSwapAllowedTime = 0f;
        EndLoopDragVisual();
        SetCreationScrollEnabled(true);

        SyncUiToWorkingData();
        RebuildLoopSections();
    }

    private void ResolveDropTarget(float pointerY, out int targetLoopIndex, out int targetEntryIndex)
    {
        targetLoopIndex = -1;
        targetEntryIndex = -1;

        if (_loopUiSections.Count == 0)
            return;

        var closestLoopDistance = float.MaxValue;
        for (var loopIndex = 0; loopIndex < _loopUiSections.Count; loopIndex++)
        {
            var loopUi = _loopUiSections[loopIndex];
            if (loopUi == null || loopUi.SectionRoot == null)
                continue;

            var sectionCenterY = GetRectCenterYInDragSpace(loopUi.SectionRoot);
            var loopDistance = Mathf.Abs(pointerY - sectionCenterY);
            if (loopDistance < closestLoopDistance)
            {
                closestLoopDistance = loopDistance;
                targetLoopIndex = loopIndex;
            }
        }

        if (targetLoopIndex < 0 || targetLoopIndex >= _loopUiSections.Count)
            return;

        var targetLoopUi = _loopUiSections[targetLoopIndex];
        if (targetLoopUi.EntryRows.Count == 0)
        {
            targetEntryIndex = 0;
            return;
        }

        targetEntryIndex = targetLoopUi.EntryRows.Count;
        for (var entryIndex = 0; entryIndex < targetLoopUi.EntryRows.Count; entryIndex++)
        {
            var row = targetLoopUi.EntryRows[entryIndex].NameRow;
            var centerY = GetRectCenterYInDragSpace(row);
            if (pointerY >= centerY)
            {
                targetEntryIndex = entryIndex;
                return;
            }
        }
    }

    private float GetRectCenterYInDragSpace(GameObject go)
    {
        var rt = go != null ? go.GetComponent<RectTransform>() : null;
        if (rt == null || _entryDragSpace == null)
            return 0f;

        var worldCenter = rt.TransformPoint(rt.rect.center);
        var localCenter = _entryDragSpace.InverseTransformPoint(worldCenter);
        return localCenter.y;
    }

    private void MoveEntryToLoop(int sourceLoopIndex, int sourceEntryIndex, int targetLoopIndex, int targetEntryIndex)
    {
        if (sourceLoopIndex < 0 || sourceLoopIndex >= _workingPreset.loops.Count)
            return;
        if (targetLoopIndex < 0 || targetLoopIndex >= _workingPreset.loops.Count)
            return;

        var sourceLoop = _workingPreset.loops[sourceLoopIndex];
        var targetLoop = _workingPreset.loops[targetLoopIndex];
        if (sourceLoop == null || targetLoop == null || sourceLoop.entries == null || targetLoop.entries == null)
            return;
        if (sourceEntryIndex < 0 || sourceEntryIndex >= sourceLoop.entries.Count)
            return;

        if (sourceLoopIndex == targetLoopIndex)
        {
            // Same-loop order is already updated continuously during drag.
            // Reordering again on pointer-up can produce an incorrect extra jump.
            return;
        }

        var moved = sourceLoop.entries[sourceEntryIndex] ?? new Entry();
        sourceLoop.entries.RemoveAt(sourceEntryIndex);

        var insertIndex = Mathf.Clamp(targetEntryIndex, 0, targetLoop.entries.Count);
        targetLoop.entries.Insert(insertIndex, moved);
    }

    private void ApplyCrossLoopPreview(float pointerY)
    {
        if (!_isDraggingEntry)
            return;

        ResolveDropTarget(pointerY, out var targetLoopIndex, out var targetEntryIndex);
        if (targetLoopIndex < 0 || targetLoopIndex >= _loopUiSections.Count || targetLoopIndex == _dragLoopIndex)
        {
            ClearCrossLoopPreview();
            return;
        }

        var targetLoop = _workingPreset.loops[targetLoopIndex];
        var targetCount = targetLoop != null && targetLoop.entries != null ? targetLoop.entries.Count : 0;
        var clampedInsertIndex = Mathf.Clamp(targetEntryIndex, 0, targetCount);

        if (_previewLoopIndex == targetLoopIndex && _previewInsertIndex == clampedInsertIndex)
            return;

        ClearCrossLoopPreview();

        var targetLoopUi = _loopUiSections[targetLoopIndex];
        _previewLoopIndex = targetLoopIndex;
        _previewInsertIndex = clampedInsertIndex;

        ApplyEntryLayoutForCount(targetLoopUi, targetCount);
        ApplyCrossLoopGapPreview(targetLoopUi, clampedInsertIndex);
    }

    private void ClearCrossLoopPreview()
    {
        if (_previewLoopIndex < 0 || _previewLoopIndex >= _loopUiSections.Count)
        {
            _previewLoopIndex = -1;
            _previewInsertIndex = -1;
            return;
        }

        var previewLoop = _workingPreset.loops[_previewLoopIndex];
        var previewCount = previewLoop != null && previewLoop.entries != null ? previewLoop.entries.Count : 0;
        ApplyEntryLayoutForCount(_loopUiSections[_previewLoopIndex], previewCount);

        _previewLoopIndex = -1;
        _previewInsertIndex = -1;
    }

    private void ApplyCrossLoopGapPreview(LoopUiRefs loopUi, int insertIndex)
    {
        if (loopUi == null || loopUi.EntryRows.Count == 0)
            return;

        for (var i = insertIndex; i < loopUi.EntryRows.Count; i++)
        {
            var refs = loopUi.EntryRows[i];
            var previewOffset = -loopUi.EntryBlockYOffset * 0.55f;
            ApplyOffsetToRow(refs.HeaderRow, previewOffset);
            ApplyOffsetToRow(refs.ModeRow, previewOffset);
            ApplyOffsetToRow(refs.NameRow, previewOffset);
            ApplyOffsetToRow(refs.DurationHeaderRow, previewOffset);
            ApplyOffsetToRow(refs.DurationRow, previewOffset);
            ApplyOffsetToRow(refs.RepCountHeaderRow, previewOffset);
            ApplyOffsetToRow(refs.RepCountRow, previewOffset);
            ApplyOffsetToRow(refs.ControlsRow, previewOffset);
        }
    }

    private float GetDraggedEntryHeaderNaturalY(int index)
    {
        if (_dragLoopIndex < 0 || _dragLoopIndex >= _loopUiSections.Count)
            return 0f;

        var entryRows = _loopUiSections[_dragLoopIndex].EntryRows;
        if (index < 0 || index >= entryRows.Count)
            return 0f;

        var headerRow = entryRows[index].HeaderRow;
        if (headerRow == null)
            return 0f;

        var rt = headerRow.GetComponent<RectTransform>();
        return rt != null ? rt.anchoredPosition.y : 0f;
    }

    private void ApplyDraggedBlockOffset(int index, float deltaY)
    {
        if (_dragLoopIndex < 0 || _dragLoopIndex >= _loopUiSections.Count)
            return;

        var entryRows = _loopUiSections[_dragLoopIndex].EntryRows;
        if (index < 0 || index >= entryRows.Count)
            return;

        var refs = entryRows[index];
        ApplyOffsetToRow(refs.HeaderRow, deltaY);
        ApplyOffsetToRow(refs.ModeRow, deltaY);
        ApplyOffsetToRow(refs.NameRow, deltaY);
        ApplyOffsetToRow(refs.DurationHeaderRow, deltaY);
        ApplyOffsetToRow(refs.DurationRow, deltaY);
        ApplyOffsetToRow(refs.RepCountHeaderRow, deltaY);
        ApplyOffsetToRow(refs.RepCountRow, deltaY);
        ApplyOffsetToRow(refs.ControlsRow, deltaY);
        BringDraggedEntryToFront(_dragLoopIndex, index);
    }

    private void BringDraggedEntryToFront(int loopIndex, int entryIndex)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return;

        var entryRows = _loopUiSections[loopIndex].EntryRows;
        if (entryIndex < 0 || entryIndex >= entryRows.Count)
            return;

        var refs = entryRows[entryIndex];
        BringRowToFront(refs.HeaderRow);
        BringRowToFront(refs.ModeRow);
        BringRowToFront(refs.NameRow);
        BringRowToFront(refs.DurationHeaderRow);
        BringRowToFront(refs.DurationRow);
        BringRowToFront(refs.RepCountHeaderRow);
        BringRowToFront(refs.RepCountRow);
        BringRowToFront(refs.ControlsRow);
    }

    private void BringLoopSectionInFrontOfAddLoop(GameObject sectionRoot)
    {
        if (sectionRoot == null)
            return;

        var addLoopPanel = uiHandler != null ? uiHandler.GetElement("AddLoopPanel") : null;
        if (addLoopPanel == null || addLoopPanel.transform.parent != sectionRoot.transform.parent)
        {
            sectionRoot.transform.SetAsLastSibling();
            return;
        }

        var addLoopIndex = addLoopPanel.transform.GetSiblingIndex();
        var targetIndex = Mathf.Max(0, addLoopIndex - 1);
        sectionRoot.transform.SetSiblingIndex(targetIndex);
        addLoopPanel.transform.SetAsLastSibling();
    }

    private static void BringRowToFront(GameObject row)
    {
        if (row == null)
            return;

        row.transform.SetAsLastSibling();
    }

    private static void ApplyOffsetToRow(GameObject row, float deltaY)
    {
        if (row == null)
            return;

        var rt = row.GetComponent<RectTransform>();
        if (rt == null)
            return;

        var pos = rt.anchoredPosition;
        pos.y += deltaY;
        rt.anchoredPosition = pos;
    }

    private void MoveEntryUiRefs(int loopIndex, int fromIndex, int toIndex)
    {
        if (loopIndex < 0 || loopIndex >= _loopUiSections.Count)
            return;

        var entryRows = _loopUiSections[loopIndex].EntryRows;
        if (fromIndex < 0 || fromIndex >= entryRows.Count || toIndex < 0 || toIndex >= entryRows.Count || fromIndex == toIndex)
            return;

        var moved = entryRows[fromIndex];
        entryRows.RemoveAt(fromIndex);
        entryRows.Insert(toIndex, moved);
    }

    private void SetCreationScrollEnabled(bool enabled)
    {
        if (_creationScrollRect != null)
            _creationScrollRect.enabled = enabled;
    }

    private void MoveEntry(int loopIndex, int fromIndex, int toIndex)
    {
        if (loopIndex < 0 || loopIndex >= _workingPreset.loops.Count)
            return;

        var loop = _workingPreset.loops[loopIndex];
        if (loop == null || loop.entries == null)
            return;

        if (fromIndex < 0 || fromIndex >= loop.entries.Count || toIndex < 0 || toIndex >= loop.entries.Count || fromIndex == toIndex)
            return;

        var moved = loop.entries[fromIndex] ?? new Entry();
        loop.entries.RemoveAt(fromIndex);

        loop.entries.Insert(toIndex, moved);
    }

    private void ApplyEntryLayoutForCount(LoopUiRefs loopUi, int count)
    {
        if (loopUi == null || loopUi.SectionRoot == null || loopUi.AddButtonsRow == null)
            return;

        var effectiveCount = Mathf.Max(0, count);
        var loopIndex = _loopUiSections.IndexOf(loopUi);
        var loop = loopIndex >= 0 && loopIndex < _workingPreset.loops.Count ? _workingPreset.loops[loopIndex] : null;

        const float compactTimeReduction = 122f;
        var repsBlockHeight = loopUi.EntryBlockYOffset;
        var timeBlockHeight = repsBlockHeight - compactTimeReduction;

        var cumulativeOffset = 0f;
        var totalBlocksHeight = 0f;

        for (var i = 0; i < loopUi.EntryRows.Count; i++)
        {
            var refs = loopUi.EntryRows[i];
            var isRepsMode = loop != null && loop.entries != null && i < loop.entries.Count && loop.entries[i] != null && loop.entries[i].mode == EntryMode.REPS;

            SetAnchoredY(refs.HeaderRow, loopUi.BaseEntryRowHeaderY - cumulativeOffset);
            SetAnchoredY(refs.ModeRow, loopUi.BaseEntryModeRowY - cumulativeOffset);
            SetAnchoredY(refs.NameRow, loopUi.BaseEntryRowY - cumulativeOffset);

            if (isRepsMode)
            {
                SetAnchoredY(refs.RepCountHeaderRow, loopUi.BaseEntryRepCountHeaderY - cumulativeOffset);
                SetAnchoredY(refs.RepCountRow, loopUi.BaseEntryRepCountRowY - cumulativeOffset);
                SetAnchoredY(refs.DurationHeaderRow, loopUi.BaseEntryDurationHeaderY - cumulativeOffset);
                SetAnchoredY(refs.DurationRow, loopUi.BaseEntryDurationRowY - cumulativeOffset);
                SetAnchoredY(refs.ControlsRow, loopUi.BaseEntryControlsRowY - cumulativeOffset);
                cumulativeOffset += repsBlockHeight;
                totalBlocksHeight += repsBlockHeight;
            }
            else
            {
                SetAnchoredY(refs.DurationHeaderRow, loopUi.BaseEntryRepCountHeaderY - cumulativeOffset);
                SetAnchoredY(refs.DurationRow, loopUi.BaseEntryRepCountRowY - cumulativeOffset);
                SetAnchoredY(refs.RepCountHeaderRow, loopUi.BaseEntryRepCountHeaderY - cumulativeOffset);
                SetAnchoredY(refs.RepCountRow, loopUi.BaseEntryRepCountRowY - cumulativeOffset);
                SetAnchoredY(refs.ControlsRow, loopUi.BaseEntryDurationRowY - cumulativeOffset);
                cumulativeOffset += timeBlockHeight;
                totalBlocksHeight += timeBlockHeight;
            }
        }

        var baselineHeight = repsBlockHeight;
        var heightDelta = totalBlocksHeight - baselineHeight;

        // Keep previous behavior for empty state where template rows are hidden.
        if (effectiveCount == 0)
            heightDelta = -baselineHeight;

        SetAnchoredY(loopUi.AddButtonsRow, loopUi.BaseAddButtonsY - heightDelta);

        // Keep section height tightly wrapped to the Add Entry row to avoid dead space below it.
        const float bottomPadding = 40f;
        var addButtonsRt = loopUi.AddButtonsRow != null ? loopUi.AddButtonsRow.GetComponent<RectTransform>() : null;
        var addButtonsHeight = addButtonsRt != null ? addButtonsRt.rect.height : 110f;
        var addButtonsCenterY = Mathf.Abs(GetAnchoredY(loopUi.AddButtonsRow));
        var requiredSectionHeight = addButtonsCenterY + (addButtonsHeight * 0.5f) + bottomPadding;

        var loopSectionRt = loopUi.SectionRoot.GetComponent<RectTransform>();
        if (loopSectionRt != null)
        {
            var size = loopSectionRt.sizeDelta;
            size.y = requiredSectionHeight;
            loopSectionRt.sizeDelta = size;
        }

        var loopSectionLe = loopUi.SectionRoot.GetComponent<LayoutElement>();
        if (loopSectionLe != null)
            loopSectionLe.preferredHeight = requiredSectionHeight;
    }

    private static float GetAnchoredY(GameObject go)
    {
        var rt = go != null ? go.GetComponent<RectTransform>() : null;
        return rt != null ? rt.anchoredPosition.y : 0f;
    }

    private static void SetAnchoredY(GameObject go, float y)
    {
        var rt = go != null ? go.GetComponent<RectTransform>() : null;
        if (rt == null)
            return;

        var pos = rt.anchoredPosition;
        pos.y = y;
        rt.anchoredPosition = pos;
    }
}
