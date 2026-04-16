using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TimeLoop.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TimeLoop.Core.UI
{
    public static class HtmlUiBuilder
    {
        private sealed class MenuBuildContext
        {
            public UIHandler Handler;
            public Font Font;
            public Color TextColor;
            public Color PanelColor;
            public Color ButtonColor;
        }

        // ── Public API ───────────────────────────────────────────────────────

        public static RectTransform Build(string markup, UIHandler handler, RectTransform parent, string generatedRootName)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (string.IsNullOrWhiteSpace(markup))
                throw new ArgumentException("Markup cannot be empty.", nameof(markup));

            EnsureEventSystem();

            var rootName = string.IsNullOrWhiteSpace(generatedRootName) ? "GeneratedHtmlUi" : generatedRootName;
            var rootGo = new GameObject(rootName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var xml = new XmlDocument();
            xml.LoadXml(markup);

            var documentNode = xml.DocumentElement;
            if (documentNode == null)
                return rootRt;

            var children = documentNode.Name.Equals("ui", StringComparison.OrdinalIgnoreCase)
                ? documentNode.ChildNodes
                : xml.ChildNodes;

            foreach (XmlNode node in children)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;
                BuildNode(node, rootRt, handler, font);
            }

            return rootRt;
        }

        // ── Node dispatch ────────────────────────────────────────────────────

        private static void BuildNode(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            switch (node.Name.ToLowerInvariant())
            {
                case "panel":
                case "div":
                    BuildPanel(node, parent, handler, font);
                    break;

                case "text":
                case "label":
                    BuildText(node, parent, handler, font);
                    break;

                case "button":
                    BuildButton(node, parent, handler, font, null);
                    break;

                case "scrollview":
                case "scroll":
                    BuildScrollView(node, parent, handler, font);
                    break;

                case "row":
                case "hstack":
                    BuildRow(node, parent, handler, font);
                    break;

                case "inputfield":
                case "input":
                    BuildInputField(node, parent, handler, font);
                    break;

                case "progressbar":
                case "progress":
                    BuildProgressBar(node, parent, handler);
                    break;

                case "separator":
                    BuildSeparator(node, parent, handler);
                    break;

                case "menu":
                    BuildMenu(node, parent, handler, font);
                    break;
            }
        }

        // ── Element builders ─────────────────────────────────────────────────

        private static RectTransform BuildPanel(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "Panel");
            var panelGo = new GameObject(id, typeof(RectTransform), typeof(Image));
            var rt = panelGo.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            ApplyBackground(node, panelGo.GetComponent<Image>());
            ApplyLayout(node, rt, new Vector2(400f, 240f));
            ApplyFlex(node, panelGo);

            handler.RegisterElement(id, panelGo);

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;
                BuildNode(child, rt, handler, font);
            }

            return rt;
        }

        private static void BuildText(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "Text");
            var go = new GameObject(id, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);

            ApplyLayout(node, rt, new Vector2(320f, 80f));
            ApplyFlex(node, go);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = GetAttribute(node, "value", node.InnerText ?? string.Empty);
            text.color = ParseColor(GetAttribute(node, "color", "#FFFFFFFF"), Color.white);
            text.fontSize = ParseInt(GetAttribute(node, "fontSize", "28"), 28);
            text.alignment = ParseTextAnchor(GetAttribute(node, "align", "middle-center"));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            handler.RegisterElement(id, go);
        }

        private static Button BuildButton(XmlNode node, RectTransform parent, UIHandler handler, Font font, Action onClickOverride)
        {
            var id = GetAttribute(node, "id", "Button");
            var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            ApplyLayout(node, rt, new Vector2(240f, 72f));
            ApplyFlex(node, go);

            go.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "buttonColor", "#2A2A2AE6"), new Color(0.16f, 0.16f, 0.16f, 0.9f));

            var btn = go.GetComponent<Button>();
            var iconPath = GetAttribute(node, "icon", string.Empty);
            var labelText = GetAttribute(node, "text", string.Empty);
            if (string.IsNullOrWhiteSpace(labelText) && string.IsNullOrWhiteSpace(iconPath))
                labelText = id;

            var label = CreateCenteredLabel(go.transform, font, labelText);
            label.color = ParseColor(GetAttribute(node, "textColor", "#FFFFFFFF"), Color.white);

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                var iconSprite = Resources.Load<Sprite>(iconPath);
                if (iconSprite != null)
                {
                    var iconAlign = GetAttribute(node, "iconAlign", "left").Trim().ToLowerInvariant();
                    var iconSize = ParseFloat(GetAttribute(node, "iconSize", "24"), 24f);
                    var iconColor = ParseColor(GetAttribute(node, "iconColor", "#FFFFFFFF"), Color.white);

                    var icon = CreateButtonIcon(go.transform, iconSprite, iconColor, iconSize, iconAlign);

                    if (!string.IsNullOrWhiteSpace(labelText))
                    {
                        var horizontalPadding = iconSize + 24f;
                        if (iconAlign == "left")
                        {
                            label.alignment = TextAnchor.MiddleLeft;
                            label.rectTransform.offsetMin = new Vector2(horizontalPadding, 0f);
                        }
                        else if (iconAlign == "right")
                        {
                            label.alignment = TextAnchor.MiddleRight;
                            label.rectTransform.offsetMax = new Vector2(-horizontalPadding, 0f);
                        }
                    }
                    else
                    {
                        // Pure icon button: center icon and keep label empty.
                        icon.rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
            }

            var eventId = GetAttribute(node, "event", string.Empty);

            btn.onClick.AddListener(() =>
            {
                onClickOverride?.Invoke();
                if (!string.IsNullOrWhiteSpace(eventId))
                {
                    var arg = new AppEventArg()
                        .Add("elementId", id)
                        .Add("eventId", eventId);
                    AppEvents.Publish(eventId, arg);
                }
            });

            handler.RegisterElement(id, go);
            return btn;
        }

        private static Image CreateButtonIcon(Transform parent, Sprite sprite, Color color, float size, string align)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.SetParent(parent, false);

            var normalizedAlign = (align ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedAlign == "right")
            {
                iconRt.anchorMin = new Vector2(1f, 0.5f);
                iconRt.anchorMax = new Vector2(1f, 0.5f);
                iconRt.pivot = new Vector2(1f, 0.5f);
                iconRt.anchoredPosition = new Vector2(-12f, 0f);
            }
            else
            {
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(12f, 0f);
            }

            iconRt.sizeDelta = new Vector2(size, size);

            var image = iconGo.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void BuildScrollView(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "ScrollView");

            // Root ScrollRect
            var scrollGo = new GameObject(id, typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            ApplyLayout(node, scrollRt, new Vector2(400f, 400f));

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            // Viewport with mask
            var viewportGo = new GameObject($"{id}_Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;

            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            // Content panel with VerticalLayoutGroup + ContentSizeFitter
            var contentGo = new GameObject($"{id}_Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = ParseFloat(GetAttribute(node, "spacing", "8"), 8f);
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;

            handler.RegisterElement(id, scrollGo);
            handler.RegisterElement($"{id}_Content", contentGo);
            handler.RegisterElement($"{id}_Viewport", viewportGo);

            // Build any static child nodes into content
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;
                BuildNode(child, contentRt, handler, font);
            }
        }

        private static void BuildRow(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "Row");
            var go = new GameObject(id, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            ApplyLayout(node, rt, new Vector2(400f, 64f));
            ApplyFlex(node, go);

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = ParseFloat(GetAttribute(node, "spacing", "8"), 8f);
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childControlHeight = true;
            hlg.childControlWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;

            var bg = GetAttribute(node, "background", "transparent");
            if (!bg.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                go.AddComponent<Image>().color = ParseColor(bg, new Color(0f, 0f, 0f, 0.2f));
            }

            handler.RegisterElement(id, go);

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;
                BuildNode(child, rt, handler, font);
            }
        }

        private static void BuildInputField(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "InputField");
            var placeholder = GetAttribute(node, "placeholder", "Enter text...");
            var defaultValue = GetAttribute(node, "value", string.Empty);

            var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(InputField));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            ApplyLayout(node, rt, new Vector2(300f, 60f));
            ApplyFlex(node, go);

            go.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "background", "#24243EFF"), new Color(0.14f, 0.14f, 0.24f));

            // Text child
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 6f);
            textRt.offsetMax = new Vector2(-12f, -6f);

            var textComp = textGo.GetComponent<Text>();
            textComp.font = font;
            textComp.fontSize = ParseInt(GetAttribute(node, "fontSize", "26"), 26);
            textComp.color = ParseColor(GetAttribute(node, "color", "#FFFFFFFF"), Color.white);
            textComp.supportRichText = false;

            // Placeholder child
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.SetParent(rt, false);
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(12f, 6f);
            phRt.offsetMax = new Vector2(-12f, -6f);

            var phText = phGo.GetComponent<Text>();
            phText.font = font;
            phText.fontSize = ParseInt(GetAttribute(node, "fontSize", "26"), 26);
            phText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            phText.fontStyle = FontStyle.Italic;
            phText.text = placeholder;

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = textComp;
            inputField.placeholder = phText;
            inputField.text = defaultValue;

            handler.RegisterElement(id, go);
        }

        private static void BuildProgressBar(XmlNode node, RectTransform parent, UIHandler handler)
        {
            var id = GetAttribute(node, "id", "ProgressBar");

            // Background track
            var go = new GameObject(id, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            ApplyLayout(node, rt, new Vector2(400f, 28f));
            ApplyFlex(node, go);

            go.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "backgroundColor", "#2A2A4AFF"), new Color(0.16f, 0.16f, 0.29f));

            // Fill image (uses fillAmount for easy progress updates)
            var fillGo = new GameObject($"{id}_Fill", typeof(RectTransform), typeof(Image));
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.SetParent(rt, false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);

            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = ParseColor(GetAttribute(node, "fillColor", "#3B6FF5FF"), new Color(0.23f, 0.44f, 0.96f));
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = ParseFloat(GetAttribute(node, "value", "0"), 0f);

            handler.RegisterElement(id, go);
            handler.RegisterElement($"{id}_Fill", fillGo);
        }

        private static void BuildSeparator(XmlNode node, RectTransform parent, UIHandler handler)
        {
            var id = GetAttribute(node, "id", "Separator");
            var thickness = ParseFloat(GetAttribute(node, "thickness", "2"), 2f);

            var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            ApplyLayout(node, rt, new Vector2(0f, thickness));

            go.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "color", "#FFFFFF33"), new Color(1f, 1f, 1f, 0.2f));

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = thickness;
            le.flexibleWidth = 1f;

            handler.RegisterElement(id, go);
        }

        // ── Menu builder ─────────────────────────────────────────────────────

        private static void BuildMenu(XmlNode node, RectTransform parent, UIHandler handler, Font font)
        {
            var id = GetAttribute(node, "id", "Menu");
            var menuRootGo = new GameObject(id, typeof(RectTransform));
            var menuRoot = menuRootGo.GetComponent<RectTransform>();
            menuRoot.SetParent(parent, false);
            ApplyLayout(node, menuRoot, new Vector2(360f, 700f));

            handler.RegisterElement(id, menuRootGo);

            var toggleButtonGo = new GameObject($"{id}_Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
            var toggleRt = toggleButtonGo.GetComponent<RectTransform>();
            toggleRt.SetParent(menuRoot, false);
            toggleRt.anchorMin = new Vector2(0f, 1f);
            toggleRt.anchorMax = new Vector2(0f, 1f);
            toggleRt.pivot = new Vector2(0f, 1f);
            toggleRt.anchoredPosition = Vector2.zero;
            toggleRt.sizeDelta = new Vector2(
                ParseFloat(GetAttribute(node, "buttonWidth", "160"), 160f),
                ParseFloat(GetAttribute(node, "buttonHeight", "64"), 64f));

            toggleButtonGo.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "buttonColor", "#243A5AE6"), new Color(0.14f, 0.23f, 0.35f, 0.9f));

            var toggleButton = toggleButtonGo.GetComponent<Button>();
            CreateCenteredLabel(toggleButtonGo.transform, font, GetAttribute(node, "buttonText", "Menu"));

            var dropdownGo = new GameObject($"{id}_Dropdown",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var dropdownRt = dropdownGo.GetComponent<RectTransform>();
            dropdownRt.SetParent(menuRoot, false);
            dropdownRt.anchorMin = new Vector2(0f, 1f);
            dropdownRt.anchorMax = new Vector2(0f, 1f);
            dropdownRt.pivot = new Vector2(0f, 1f);
            dropdownRt.anchoredPosition = new Vector2(0f, -toggleRt.sizeDelta.y - 8f);
            dropdownRt.sizeDelta = new Vector2(ParseFloat(GetAttribute(node, "menuWidth", "320"), 320f), 0f);

            dropdownGo.GetComponent<Image>().color =
                ParseColor(GetAttribute(node, "menuColor", "#000000B3"), new Color(0f, 0f, 0f, 0.7f));

            var layout = dropdownGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = dropdownGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            dropdownGo.SetActive(false);
            toggleButton.onClick.AddListener(() => dropdownGo.SetActive(!dropdownGo.activeSelf));

            handler.RegisterElement($"{id}_Toggle", toggleButtonGo);
            handler.RegisterElement($"{id}_Dropdown", dropdownGo);

            var ctx = new MenuBuildContext
            {
                Handler = handler,
                Font = font,
                TextColor = Color.white,
                PanelColor = new Color(0f, 0f, 0f, 0.6f),
                ButtonColor = new Color(0.2f, 0.2f, 0.2f, 0.95f)
            };

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element ||
                    !child.Name.Equals("item", StringComparison.OrdinalIgnoreCase))
                    continue;
                BuildMenuItem(child, dropdownRt, ctx, 0);
            }
        }

        private static void BuildMenuItem(XmlNode itemNode, RectTransform parent, MenuBuildContext ctx, int depth)
        {
            var id = GetAttribute(itemNode, "id", "Item");
            var text = GetAttribute(itemNode, "text", id);
            var eventId = GetAttribute(itemNode, "event", string.Empty);

            var containerGo = new GameObject($"{id}_Container",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.SetParent(parent, false);

            var vertical = containerGo.GetComponent<VerticalLayoutGroup>();
            vertical.spacing = 6f;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.padding = new RectOffset(18 * depth, 0, 0, 0);

            containerGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var buttonGo = new GameObject(id,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.SetParent(containerRt, false);
            buttonRt.sizeDelta = new Vector2(0f, 54f);

            buttonGo.GetComponent<LayoutElement>().preferredHeight = 54f;
            buttonGo.GetComponent<Image>().color = ctx.ButtonColor;

            var button = buttonGo.GetComponent<Button>();
            var label = CreateCenteredLabel(buttonGo.transform, ctx.Font, text);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = ctx.TextColor;
            label.rectTransform.offsetMin = new Vector2(18f, 0f);

            ctx.Handler.RegisterElement(id, buttonGo);

            var childItems = new List<XmlNode>();
            foreach (XmlNode child in itemNode.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element &&
                    child.Name.Equals("item", StringComparison.OrdinalIgnoreCase))
                    childItems.Add(child);
            }

            if (childItems.Count == 0)
            {
                button.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrWhiteSpace(eventId))
                    {
                        var arg = new AppEventArg()
                            .Add("elementId", id)
                            .Add("eventId", eventId)
                            .Add("text", text);
                        AppEvents.Publish(eventId, arg);
                    }
                });
                return;
            }

            label.text = $"> {text}";

            var childPanelGo = new GameObject($"{id}_Children",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var childPanelRt = childPanelGo.GetComponent<RectTransform>();
            childPanelRt.SetParent(containerRt, false);

            childPanelGo.GetComponent<Image>().color = ctx.PanelColor;

            var childLayout = childPanelGo.GetComponent<VerticalLayoutGroup>();
            childLayout.spacing = 5f;
            childLayout.padding = new RectOffset(8, 8, 8, 8);
            childLayout.childControlHeight = true;
            childLayout.childControlWidth = true;
            childLayout.childForceExpandHeight = false;

            childPanelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            childPanelGo.SetActive(false);
            button.onClick.AddListener(() => childPanelGo.SetActive(!childPanelGo.activeSelf));

            ctx.Handler.RegisterElement($"{id}_Children", childPanelGo);

            foreach (var child in childItems)
                BuildMenuItem(child, childPanelRt, ctx, depth + 1);
        }

        // ── Layout helpers ───────────────────────────────────────────────────

        private static void ApplyBackground(XmlNode node, Image image)
        {
            var background = GetAttribute(node, "background", "transparent");
            if (background.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                image.color = new Color(0f, 0f, 0f, 0f);
            else
                image.color = ParseColor(background, new Color(0f, 0f, 0f, 0.35f));

            var imagePath = GetAttribute(node, "backgroundImage", string.Empty);
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                var sprite = Resources.Load<Sprite>(imagePath);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = false;
                    image.type = Image.Type.Simple;
                }
            }
        }

        private static void ApplyLayout(XmlNode node, RectTransform rt, Vector2 defaultSize)
        {
            // stretch="true" — fill parent with optional per-edge insets
            var stretchText = GetAttribute(node, "stretch", string.Empty);
            if (stretchText.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(
                    ParseFloat(GetAttribute(node, "left",   "0"), 0f),
                    ParseFloat(GetAttribute(node, "bottom", "0"), 0f));
                rt.offsetMax = new Vector2(
                    -ParseFloat(GetAttribute(node, "right", "0"), 0f),
                    -ParseFloat(GetAttribute(node, "top",   "0"), 0f));
                return;
            }

            var anchor = GetAttribute(node, "anchor", "middle-center");
            var anchorPoint = ParseAnchor(anchor);
            rt.anchorMin = anchorPoint;
            rt.anchorMax = anchorPoint;
            rt.pivot = anchorPoint;

            var widthPercentText = GetAttribute(node, "widthPercent", string.Empty);
            if (!string.IsNullOrWhiteSpace(widthPercentText))
            {
                var pct = Mathf.Clamp01(ParseFloat(widthPercentText, 1f));
                rt.anchorMin = new Vector2(0.5f - pct * 0.5f, anchorPoint.y);
                rt.anchorMax = new Vector2(0.5f + pct * 0.5f, anchorPoint.y);
                rt.pivot = new Vector2(0.5f, anchorPoint.y);
                rt.sizeDelta = new Vector2(0f,
                    ParseFloat(GetAttribute(node, "height",
                        defaultSize.y.ToString(CultureInfo.InvariantCulture)), defaultSize.y));
            }
            else
            {
                rt.sizeDelta = new Vector2(
                    ParseFloat(GetAttribute(node, "width",
                        defaultSize.x.ToString(CultureInfo.InvariantCulture)), defaultSize.x),
                    ParseFloat(GetAttribute(node, "height",
                        defaultSize.y.ToString(CultureInfo.InvariantCulture)), defaultSize.y));
            }

            rt.anchoredPosition = new Vector2(
                ParseFloat(GetAttribute(node, "x", "0"), 0f),
                ParseFloat(GetAttribute(node, "y", "0"), 0f));
        }

        /// <summary>Adds LayoutElement so this element participates in layout groups.</summary>
        private static void ApplyFlex(XmlNode node, GameObject go)
        {
            var flex            = GetAttribute(node, "flex",            string.Empty);
            var preferredHeight = GetAttribute(node, "preferredHeight", string.Empty);
            var preferredWidth  = GetAttribute(node, "preferredWidth",  string.Empty);

            if (string.IsNullOrWhiteSpace(flex) &&
                string.IsNullOrWhiteSpace(preferredHeight) &&
                string.IsNullOrWhiteSpace(preferredWidth))
                return;

            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();

            if (!string.IsNullOrWhiteSpace(flex))
                le.flexibleWidth = ParseFloat(flex, 1f);
            if (!string.IsNullOrWhiteSpace(preferredHeight))
                le.preferredHeight = ParseFloat(preferredHeight, 0f);
            if (!string.IsNullOrWhiteSpace(preferredWidth))
                le.preferredWidth = ParseFloat(preferredWidth, 0f);
        }

        // ── Widget helpers ───────────────────────────────────────────────────

        private static Text CreateCenteredLabel(Transform parent, Font font, string value)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(parent, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var text = labelGo.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureEventSystem()
        {
            var existingSystems = UnityEngine.Object.FindObjectsByType<EventSystem>();
            if (existingSystems.Length > 0)
            {
                var primarySystem = existingSystems[0];

                for (var index = 1; index < existingSystems.Length; index++)
                {
                    if (existingSystems[index] != null)
                        UnityEngine.Object.Destroy(existingSystems[index].gameObject);
                }

                var currentGameObject = primarySystem.gameObject;
                var legacyModule = currentGameObject.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                    UnityEngine.Object.Destroy(legacyModule);

                if (currentGameObject.GetComponent<InputSystemUIInputModule>() == null)
                    currentGameObject.AddComponent<InputSystemUIInputModule>();

                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        // ── Parsers ──────────────────────────────────────────────────────────

        private static Vector2 ParseAnchor(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "top-left":      return new Vector2(0f,   1f);
                case "top-center":    return new Vector2(0.5f, 1f);
                case "top-right":     return new Vector2(1f,   1f);
                case "middle-left":   return new Vector2(0f,   0.5f);
                case "middle-right":  return new Vector2(1f,   0.5f);
                case "bottom-left":   return new Vector2(0f,   0f);
                case "bottom-center": return new Vector2(0.5f, 0f);
                case "bottom-right":  return new Vector2(1f,   0f);
                default:              return new Vector2(0.5f, 0.5f);
            }
        }

        private static TextAnchor ParseTextAnchor(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "upper-left":    return TextAnchor.UpperLeft;
                case "upper-center":  return TextAnchor.UpperCenter;
                case "upper-right":   return TextAnchor.UpperRight;
                case "middle-left":   return TextAnchor.MiddleLeft;
                case "middle-right":  return TextAnchor.MiddleRight;
                case "lower-left":    return TextAnchor.LowerLeft;
                case "lower-center":  return TextAnchor.LowerCenter;
                case "lower-right":   return TextAnchor.LowerRight;
                default:              return TextAnchor.MiddleCenter;
            }
        }

        private static string GetAttribute(XmlNode node, string key, string defaultValue)
        {
            var value = node.Attributes?[key]?.Value;
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static int ParseInt(string raw, int defaultValue) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        private static float ParseFloat(string raw, float defaultValue) =>
            float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

        private static bool ParseBool(string raw, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            return raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static Color ParseColor(string raw, Color defaultColor)
        {
            if (ColorUtility.TryParseHtmlString(raw, out var color))
                return color;
            return defaultColor;
        }
    }
}
