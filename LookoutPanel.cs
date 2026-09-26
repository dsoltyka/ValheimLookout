using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Lookout
{
    /// <summary>
    /// The in-game panel: category toggles at the top, a filter box, and one row per item the world can yield
    /// (icon, name, where it comes from, how many are loaded nearby, and an on/off toggle). Built with Jötunn's
    /// wood-panel helpers so it matches the game. Opened with the configured key or the button on the large map.
    /// </summary>
    internal sealed class LookoutPanel : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 680f;
        private const float RowHeight = 40f;

        private static Settings S => Plugin.Settings;

        private GameObject _panel;
        private InputField _filter;
        private Transform _content;
        private Text _summary;
        private Toggle _dungeons, _locations, _buried, _explored, _discovered;
        private readonly List<Row> _rows = new List<Row>();
        private bool _dirty = true;
        private bool _syncRequested;
        private bool _builtDiscoveredOnly;
        private float _nextCountRefresh;
        private GameObject _mapButton;

        private sealed class Row
        {
            public CatalogEntry Entry;
            public GameObject Root;
            public Toggle Toggle;
            public Text Nearby;
        }

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void OnEnable()
        {
            Catalog.Changed += MarkDirty;
            S.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            Catalog.Changed -= MarkDirty;
            S.Changed -= OnSettingsChanged;
            if (_panel != null)
            {
                Destroy(_panel);
            }
            if (_mapButton != null)
            {
                Destroy(_mapButton);
            }
            GUIManager.BlockInput(false);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // Leaving the world, or the game's own menus taking over, closes the panel.
            if (Player.m_localPlayer == null || (Menu.instance != null && Menu.IsVisible()))
            {
                SetOpen(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetOpen(false);
                return;
            }

            if (_dirty)
            {
                _dirty = false;
                _syncRequested = false;
                RebuildRows();
            }
            else if (_syncRequested)
            {
                _syncRequested = false;
                SyncRows();
            }

            if (Time.unscaledTime >= _nextCountRefresh)
            {
                _nextCountRefresh = Time.unscaledTime + 0.5f;
                foreach (var row in _rows)
                {
                    row.Nearby.text = row.Entry.Nearby > 0 ? row.Entry.Nearby.ToString() : string.Empty;
                }
            }
        }

        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open)
            {
                if (GUIManager.CustomGUIFront == null || GUIManager.Instance == null)
                {
                    Plugin.Log.LogWarning("GUI not ready yet; cannot open the Lookout panel.");
                    return;
                }
                if (_panel == null)
                {
                    try
                    {
                        BuildPanel();
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogError($"Failed to build the Lookout panel: {e}");
                        return;
                    }
                }
                Catalog.EnsureScanned();
                _dirty = true;
                _panel.SetActive(true);
                SyncCategoryToggles();
                GUIManager.BlockInput(true);
            }
            else if (_panel != null && _panel.activeSelf)
            {
                _panel.SetActive(false);
                GUIManager.BlockInput(false);
            }
        }

        /// <summary>Adds a "Lookout" button to the large map once per Minimap instance.</summary>
        public void EnsureMapButton(Minimap map)
        {
            if (!S.MapButton.Value)
            {
                if (_mapButton != null)
                {
                    Destroy(_mapButton);
                    _mapButton = null;
                }
                return;
            }

            if (_mapButton != null || map == null || map.m_largeRoot == null || GUIManager.Instance == null)
            {
                return;
            }

            try
            {
                _mapButton = GUIManager.Instance.CreateButton("Lookout", map.m_largeRoot.transform,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -28f), 150f, 36f);
                _mapButton.name = "Lookout.MapButton";
                _mapButton.GetComponent<Button>().onClick.AddListener(Toggle);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not add the map button: {e.Message}");
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// A setting changed. Only the discovered-only switch alters which rows exist, so everything else is synced in
        /// place; rebuilding on every item toggle would recreate all toggles and make them flash.
        /// </summary>
        private void OnSettingsChanged()
        {
            if (S.OnlyDiscoveredItems.Value != _builtDiscoveredOnly)
            {
                _dirty = true;
            }
            else
            {
                _syncRequested = true;
            }
        }

        /// <summary>Refreshes toggles and the summary line without touching the row objects.</summary>
        private void SyncRows()
        {
            SyncCategoryToggles();
            foreach (var row in _rows)
            {
                row.Toggle.SetIsOnWithoutNotify(S.IsItemEnabled(row.Entry.Key));
            }
            UpdateSummary();
        }

        // ---- construction ----

        private void BuildPanel()
        {
            var gui = GUIManager.Instance;

            _panel = gui.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, PanelWidth, PanelHeight, draggable: true);
            _panel.name = "Lookout.Panel";

            // Title, centered on the panel
            var titleGo = gui.CreateText("Lookout", _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -34f), gui.NorseBold, 26, gui.ValheimOrange, true, Color.black, PanelWidth, 40f, false);
            titleGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            titleGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // Close
            var close = gui.CreateButton("X", _panel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -30f), 36f, 36f);
            close.GetComponent<Button>().onClick.AddListener(() => SetOpen(false));

            // Category toggles, two per line
            float y = -76f;
            _dungeons = CreateLabeledToggle("Dungeon Entrances", 30f, y, v => S.ShowDungeons.Value = v);
            _locations = CreateLabeledToggle("Other Locations", PanelWidth / 2f + 10f, y, v => S.ShowOtherLocations.Value = v);
            y -= 34f;
            _buried = CreateLabeledToggle("Buried Deposits", 30f, y, v => S.ShowBuried.Value = v);
            _explored = CreateLabeledToggle("Only Explored Map", PanelWidth / 2f + 10f, y, v => S.OnlyExploredAreas.Value = v);
            y -= 34f;
            _discovered = CreateLabeledToggle("Only Discovered Items", 30f, y, v => S.OnlyDiscoveredItems.Value = v);

            // Filter
            y -= 44f;
            var filterGo = gui.CreateInputField(_panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f + (PanelWidth - 60f) / 2f, y), InputField.ContentType.Standard, "Filter items...", 16, PanelWidth - 60f, 32f);
            _filter = filterGo.GetComponent<InputField>();
            _filter.onValueChanged.AddListener(_ => MarkDirty());

            // Summary line
            y -= 30f;
            var summaryGo = gui.CreateText("", _panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, y), gui.AveriaSerif, 14, gui.ValheimBeige, true, Color.black, PanelWidth - 60f, 20f, false);
            AlignLeft(summaryGo);
            _summary = summaryGo.GetComponent<Text>();

            // Item list
            y -= 14f;
            float listHeight = PanelHeight + y - 30f;
            var scroll = gui.CreateScrollView(_panel.transform, false, true, 10f, 6f,
                ColorBlock.defaultColorBlock, new Color(0f, 0f, 0f, 0.35f), PanelWidth - 60f, listHeight);
            var scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 1f);
            scrollRect.anchorMax = new Vector2(0f, 1f);
            scrollRect.pivot = new Vector2(0f, 1f);
            scrollRect.anchoredPosition = new Vector2(30f, y);
            _content = scroll.GetComponentInChildren<ScrollRect>().content;
            var layout = _content.GetComponent<VerticalLayoutGroup>() ?? _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 2f;
            layout.padding = new RectOffset(4, 4, 4, 4);

            _panel.SetActive(false);
        }

        private Toggle CreateLabeledToggle(string label, float x, float y, Action<bool> onChanged)
        {
            var gui = GUIManager.Instance;
            var toggleGo = gui.CreateToggle(_panel.transform, 24f, 24f);
            var rect = toggleGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            var toggle = toggleGo.GetComponent<Toggle>();
            QuietSelection(toggle);
            toggle.onValueChanged.AddListener(v => onChanged(v));

            // The label's pivot is its vertical middle; the toggle's is its top, so aim at the toggle's centre line.
            var text = gui.CreateText(label, _panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x + 32f, y - 12f), gui.AveriaSerif, 16, gui.ValheimBeige, true, Color.black, 220f, 24f, false);
            AlignLeft(text);
            return toggle;
        }

        private void SyncCategoryToggles()
        {
            _dungeons.SetIsOnWithoutNotify(S.ShowDungeons.Value);
            _locations.SetIsOnWithoutNotify(S.ShowOtherLocations.Value);
            _buried.SetIsOnWithoutNotify(S.ShowBuried.Value);
            _explored.SetIsOnWithoutNotify(S.OnlyExploredAreas.Value);
            _discovered.SetIsOnWithoutNotify(S.OnlyDiscoveredItems.Value);
        }

        private void RebuildRows()
        {
            SyncCategoryToggles();

            foreach (var row in _rows)
            {
                Destroy(row.Root);
            }
            _rows.Clear();

            string filter = _filter != null ? _filter.text.Trim() : string.Empty;
            var player = Player.m_localPlayer;
            _builtDiscoveredOnly = S.OnlyDiscoveredItems.Value;
            bool discoveredOnly = _builtDiscoveredOnly && player != null;

            var visible = Catalog.Entries
                .Where(e => !discoveredOnly || player.IsKnownMaterial(e.Key))
                .ToList();
            var entries = visible
                .Where(e => filter.Length == 0 || e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(e => S.IsItemEnabled(e.Key))
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var entry in entries)
            {
                _rows.Add(CreateRow(entry));
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var player = Player.m_localPlayer;
            bool discoveredOnly = S.OnlyDiscoveredItems.Value && player != null;
            int total = 0, visible = 0, enabled = 0;
            foreach (var entry in Catalog.Entries)
            {
                total++;
                if (discoveredOnly && !player.IsKnownMaterial(entry.Key))
                {
                    continue;
                }
                visible++;
                if (S.IsItemEnabled(entry.Key))
                {
                    enabled++;
                }
            }
            int hidden = total - visible;
            _summary.text = hidden > 0
                ? $"{enabled} of {visible} items pinned ({hidden} undiscovered hidden). Right column = loaded nearby."
                : $"{enabled} of {visible} items pinned. Right column = loaded nearby.";
        }

        private Row CreateRow(CatalogEntry entry)
        {
            var gui = GUIManager.Instance;

            // The row lays its columns out itself, so it adapts to whatever width the list ends up with:
            // [icon 32] [name: takes the rest] [source 80] [nearby 56] [toggle 26]
            var root = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(_content, false);
            var rowSize = root.GetComponent<LayoutElement>();
            rowSize.preferredHeight = RowHeight;
            rowSize.minHeight = RowHeight;
            var rowLayout = root.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 8f;
            rowLayout.padding = new RectOffset(6, 10, 0, 0);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(root.transform, false);
            Fixed(iconGo, 32f, 32f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = entry.Icon != null ? entry.Icon : Icons.Generic;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var nameGo = gui.CreateText(entry.Name, root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, gui.AveriaSerif, 17, Color.white, true, Color.black, 100f, RowHeight, false);
            AlignLeft(nameGo, clip: true);
            var nameSize = nameGo.AddComponent<LayoutElement>();
            nameSize.flexibleWidth = 1f;
            nameSize.minWidth = 60f;
            nameSize.preferredHeight = RowHeight;

            string source = SourceLabel(entry.Sources);
            var sourceGo = gui.CreateText(source, root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, gui.AveriaSerif, 13, new Color(0.75f, 0.7f, 0.6f), false, Color.black, 80f, RowHeight, false);
            AlignLeft(sourceGo, clip: true);
            Fixed(sourceGo, 80f, RowHeight);

            var nearbyGo = gui.CreateText("", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, gui.AveriaSerifBold, 16, gui.ValheimOrange, true, Color.black, 56f, RowHeight, false);
            AlignLeft(nearbyGo, clip: true);
            Fixed(nearbyGo, 56f, RowHeight);
            var nearby = nearbyGo.GetComponent<Text>();
            nearby.alignment = TextAnchor.MiddleRight;

            var toggleGo = gui.CreateToggle(root.transform, 26f, 26f);
            Fixed(toggleGo, 26f, 26f);
            var toggle = toggleGo.GetComponent<Toggle>();
            QuietSelection(toggle);
            toggle.SetIsOnWithoutNotify(S.IsItemEnabled(entry.Key));
            toggle.onValueChanged.AddListener(v => S.SetItemEnabled(entry.Key, v));

            return new Row { Entry = entry, Root = root, Toggle = toggle, Nearby = nearby };
        }

        /// <summary>
        /// Unity keeps a clicked control "selected" until something else is clicked. A selected control shows the
        /// selected tint and never the hover tint, which reads as a stuck highlight. Releasing the selection right
        /// after each click keeps hover behaving like a normal checkbox. The fade is dropped so rebuilt rows appear
        /// in their final state.
        /// </summary>
        private static void QuietSelection(Toggle toggle)
        {
            var colors = toggle.colors;
            colors.fadeDuration = 0f;
            toggle.colors = colors;
            toggle.onValueChanged.AddListener(_ =>
            {
                var events = UnityEngine.EventSystems.EventSystem.current;
                if (events != null && events.currentSelectedGameObject == toggle.gameObject)
                {
                    events.SetSelectedGameObject(null);
                }
            });
        }

        private static string SourceLabel(PoiSource sources)
        {
            switch (sources)
            {
                case PoiSource.Deposit: return "deposit";
                case PoiSource.Pickable: return "pickable";
                case PoiSource.Breakable: return "breakable";
                case PoiSource.None: return string.Empty;
                default: return "mixed";
            }
        }

        /// <summary>Gives a row child a fixed size in the row's horizontal layout.</summary>
        private static void Fixed(GameObject go, float width, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleWidth = 0f;
        }

        /// <summary>Left-aligns a Jötunn text and makes its position mean its left edge. Clipped texts stay inside their column.</summary>
        private static void AlignLeft(GameObject textGo, bool clip = false)
        {
            var text = textGo.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = clip ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
            var rect = textGo.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0f, 0.5f);
        }
    }
}
