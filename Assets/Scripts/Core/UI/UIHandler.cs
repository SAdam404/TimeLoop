using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimeLoop.Core.UI
{
    public class UIHandler : MonoBehaviour
    {
        [Header("HTML Source")]
        [SerializeField] private TextAsset htmlDefinition;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool clearExistingOnBuild = true;

        [Header("Build Target")]
        [SerializeField] private RectTransform targetRoot;

        [Header("Generated")]
        [SerializeField] private string generatedRootName = "GeneratedHtmlUi";

        private readonly Dictionary<string, GameObject> _elements = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private RectTransform _generatedRoot;

        private void Start()
        {
            if (buildOnStart)
                RebuildFromHtml(clearExistingOnBuild);
        }

        public void RebuildFromHtml(bool clearOld = true)
        {
            if (htmlDefinition == null || string.IsNullOrWhiteSpace(htmlDefinition.text))
                return;

            var root = ResolveTargetRoot();
            if (root == null)
                return;

            if (clearOld)
                ClearGeneratedUi();

            _elements.Clear();
            _generatedRoot = HtmlUiBuilder.Build(htmlDefinition.text, this, root, generatedRootName);
        }

        public void ChangeHtml(TextAsset newHtml, bool clearOld = true)
        {
            htmlDefinition = newHtml;
            RebuildFromHtml(clearOld);
        }

        public void RegisterElement(string id, GameObject element)
        {
            if (string.IsNullOrWhiteSpace(id) || element == null)
                return;

            _elements[id.Trim()] = element;
        }

        public GameObject GetElement(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            _elements.TryGetValue(id.Trim(), out var element);
            return element;
        }

        public bool IsVisible(string id)
        {
            var element = GetElement(id);
            return element != null && element.activeSelf;
        }

        public void SetVisible(string id, bool isVisible)
        {
            var element = GetElement(id);
            if (element != null)
                element.SetActive(isVisible);
        }

        public void ToggleVisible(string id)
        {
            var element = GetElement(id);
            if (element != null)
                element.SetActive(!element.activeSelf);
        }

        public void SetText(string id, string value)
        {
            var element = GetElement(id);
            if (element == null)
                return;

            var text = element.GetComponent<Text>() ?? element.GetComponentInChildren<Text>(true);
            if (text != null)
                text.text = value ?? string.Empty;
        }

        public string GetText(string id)
        {
            var element = GetElement(id);
            if (element == null)
                return null;

            var text = element.GetComponent<Text>() ?? element.GetComponentInChildren<Text>(true);
            return text != null ? text.text : null;
        }

        /// <summary>Sets the fillAmount (0-1) on a progressbar's fill image registered as id_Fill.</summary>
        public void SetProgress(string id, float value)
        {
            var fillGo = GetElement($"{id}_Fill");
            if (fillGo == null)
                return;

            var img = fillGo.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.fillAmount = Mathf.Clamp01(value);
        }

        /// <summary>Sets the text of an InputField element.</summary>
        public void SetInputText(string id, string value)
        {
            var element = GetElement(id);
            if (element == null)
                return;

            var inputField = element.GetComponent<UnityEngine.UI.InputField>();
            if (inputField != null)
                inputField.text = value ?? string.Empty;
        }

        /// <summary>Gets the current text of an InputField element.</summary>
        public string GetInputText(string id)
        {
            var element = GetElement(id);
            if (element == null)
                return null;

            var inputField = element.GetComponent<UnityEngine.UI.InputField>();
            return inputField != null ? inputField.text : null;
        }

        public void ClearGeneratedUi()
        {
            if (_generatedRoot != null)
            {
                Destroy(_generatedRoot.gameObject);
                _generatedRoot = null;
            }

            _elements.Clear();
        }

        private RectTransform ResolveTargetRoot()
        {
            if (targetRoot != null)
            {
                var targetCanvas = targetRoot.GetComponentInParent<Canvas>();
                if (targetCanvas != null)
                {
                    var existingScaler = targetCanvas.GetComponent<CanvasScaler>();
                    if (existingScaler != null)
                    {
                        existingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        existingScaler.referenceResolution = new Vector2(1080f, 1920f);
                        existingScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                        existingScaler.matchWidthOrHeight = 1f;
                    }
                }

                return targetRoot;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var existingScaler = canvas.GetComponent<CanvasScaler>();
                if (existingScaler != null)
                {
                    existingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    existingScaler.referenceResolution = new Vector2(1080f, 1920f);
                    existingScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    existingScaler.matchWidthOrHeight = 1f;
                }

                targetRoot = canvas.GetComponent<RectTransform>();
                return targetRoot;
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var generatedCanvas = canvasGo.GetComponent<Canvas>();
            generatedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            targetRoot = canvasGo.GetComponent<RectTransform>();
            return targetRoot;
        }
    }
}
